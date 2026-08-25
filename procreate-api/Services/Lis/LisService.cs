using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ProCreateApi.Services.Lis;

public class LisService : ILisService
{
    private readonly AppDbContext _db;
    private readonly LisSettings _settings;
    private readonly Hl7Builder _builder;
    private readonly MllpClient _mllpClient;
    private readonly ILogger<LisService> _logger;

    public LisService(AppDbContext db, LisSettings settings, Hl7Builder builder, MllpClient mllpClient, ILogger<LisService> logger)
    {
        _db = db;
        _settings = settings;
        _builder = builder;
        _mllpClient = mllpClient;
        _logger = logger;
    }

    public LisConnectionInfo GetConnectionInfo()
        => new(_settings.Enabled, _settings.Host, _settings.Port, _settings.MllpListenPort);

    public async Task SendOrderAsync(LabOrder order, CancellationToken ct = default)
    {
        if (!_settings.Enabled) return;

        // Ensure navigation properties are loaded
        if (order.Visit?.Patient is null || order.LabTest is null)
        {
            order = await _db.LabOrders
                .Include(o => o.Visit).ThenInclude(v => v.Patient)
                .Include(o => o.LabTest)
                .FirstAsync(o => o.Id == order.Id, ct);
        }

        try
        {
            var ormMsg = _builder.BuildOrmO01(order);
            var ack = await _mllpClient.SendAsync(ormMsg, ct);

            var ackParser = Hl7Parser.Parse(ack);
            var ackCode = ackParser.GetField("MSA", 0, 1); // AA = accepted, AE = error, AR = reject

            order.LisSentAt = DateTime.UtcNow;
            order.LisStatus = ackCode == "AA" ? "Acknowledged" : "Failed";
            order.LisError = ackCode == "AA" ? null : $"LIS rejected with {ackCode}";
            // Filler order ID (assigned by LIS) is in MSA-3 or ORC-3 depending on vendor;
            // capture it if provided
            var fillerOrderId = ackParser.GetField("MSA", 0, 3);
            if (!string.IsNullOrEmpty(fillerOrderId))
                order.LisOrderId = fillerOrderId;

            _logger.LogInformation("ORM^O01 for order {Id} sent. ACK={Ack}", order.Id, ackCode);
        }
        catch (Exception ex)
        {
            order.LisStatus = "Failed";
            order.LisError = ex.Message;
            _logger.LogError(ex, "Failed to send ORM^O01 for order {Id}", order.Id);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task RetryOrderAsync(int labOrderId, CancellationToken ct = default)
    {
        var order = await _db.LabOrders
            .Include(o => o.Visit).ThenInclude(v => v.Patient)
            .Include(o => o.LabTest)
            .FirstOrDefaultAsync(o => o.Id == labOrderId, ct)
            ?? throw new KeyNotFoundException($"LabOrder {labOrderId} not found.");

        order.LisStatus = null;
        order.LisError = null;
        await SendOrderAsync(order, ct);
    }

    public async Task SendPatientAsync(Patient patient, bool isUpdate = false, CancellationToken ct = default)
    {
        if (!_settings.Enabled) return;

        try
        {
            var adtMsg = isUpdate ? _builder.BuildAdtA08(patient) : _builder.BuildAdtA01(patient);
            var ack = await _mllpClient.SendAsync(adtMsg, ct);

            patient.LisRegisteredAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("ADT^{Type} for patient {Id} sent.", isUpdate ? "A08" : "A01", patient.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ADT for patient {Id}", patient.Id);
            // Non-fatal: patient sync failure should not block the primary operation
        }
    }

    public async Task ProcessInboundResultAsync(Hl7Parser message, CancellationToken ct = default)
    {
        var placerOrderId = message.GetPlacerOrderId(); // should be our SpecimenBarcode
        if (string.IsNullOrEmpty(placerOrderId))
        {
            _logger.LogWarning("ORU^R01 received with no placer order ID — skipping.");
            return;
        }

        var order = await _db.LabOrders
            .Include(o => o.LabTest).ThenInclude(t => t.Parameters)
            .Include(o => o.Results)
            .FirstOrDefaultAsync(o => o.SpecimenBarcode == placerOrderId || o.LisOrderId == placerOrderId, ct);

        if (order is null)
        {
            _logger.LogWarning("ORU^R01 references unknown order '{Id}' — no matching SpecimenBarcode or LisOrderId.", placerOrderId);
            return;
        }

        var observations = message.GetObservations();
        if (observations.Count == 0)
        {
            _logger.LogWarning("ORU^R01 for order {Id} has no OBX segments.", order.Id);
            return;
        }

        // Replace existing results
        _db.LabResults.RemoveRange(order.Results);

        foreach (var obs in observations)
        {
            // Match parameter by code first, then by name (case-insensitive)
            var param = order.LabTest.Parameters.FirstOrDefault(p =>
                string.Equals(p.Name, obs.Code, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, obs.Name, StringComparison.OrdinalIgnoreCase));

            if (param is null)
            {
                _logger.LogWarning("ORU^R01 OBX '{Code}/{Name}' has no matching TestParameter in order {Id} — skipped.", obs.Code, obs.Name, order.Id);
                continue;
            }

            string flag = MapAbnormalFlag(obs.AbnormalFlag);
            // If LIS sends a normal flag but we can compute ours, use our logic
            if (flag == "Normal" && double.TryParse(obs.Value, out var numVal))
            {
                if (double.TryParse(param.NormalMin, out var min) && numVal < min) flag = "Low";
                else if (double.TryParse(param.NormalMax, out var max) && numVal > max) flag = "High";
            }

            _db.LabResults.Add(new LabResult
            {
                LabOrderId = order.Id,
                TestParameterId = param.Id,
                Value = obs.Value,
                Flag = flag,
                Remarks = string.IsNullOrEmpty(obs.Units) ? null : $"Units: {obs.Units}"
            });
        }

        order.Status = "Resulted";
        order.ProcessedAt = DateTime.UtcNow;
        order.LisStatus = "Acknowledged";
        order.LisAckedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("ORU^R01 for order {Id} processed — {Count} results saved.", order.Id, observations.Count);
    }

    private static string MapAbnormalFlag(string lisFlag) => lisFlag.ToUpper() switch
    {
        "H" or "HH" or ">" => "High",
        "L" or "LL" or "<" => "Low",
        _ => "Normal"
    };
}
