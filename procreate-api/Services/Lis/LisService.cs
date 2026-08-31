using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

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

        var results = new List<LabResult>();
        var narrative = new StringBuilder();
        var abnormal = false;

        foreach (var obs in observations)
        {
            if (IsAbnormalFlag(obs.AbnormalFlag)) abnormal = true;

            // Match parameter by code first, then by name (case-insensitive)
            var param = order.LabTest.Parameters.FirstOrDefault(p =>
                string.Equals(p.Name, obs.Code, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, obs.Name, StringComparison.OrdinalIgnoreCase));

            if (param is null)
            {
                // Nothing to hang a measurement on. Imaging, ultrasound and
                // heart-station studies have no parameters at all and are
                // reported as prose, and even a bench panel can carry a
                // free-text comment. Either way it is clinical content, so it
                // goes to the findings rather than being dropped on the floor.
                AppendToNarrative(narrative, obs);
                continue;
            }

            string flag = MapAbnormalFlag(obs.AbnormalFlag);
            // If LIS sends a normal flag but we can compute ours, use our logic
            if (flag == "Normal" && double.TryParse(obs.Value, out var numVal))
            {
                if (double.TryParse(param.NormalMin, out var min) && numVal < min) flag = "Low";
                else if (double.TryParse(param.NormalMax, out var max) && numVal > max) flag = "High";
            }

            results.Add(new LabResult
            {
                LabOrderId = order.Id,
                TestParameterId = param.Id,
                Value = obs.Value,
                Flag = flag,
                Remarks = string.IsNullOrEmpty(obs.Units) ? null : $"Units: {obs.Units}"
            });
        }

        // A message we could make nothing of must not touch the order. Marking
        // it resulted would show an empty reading as ready, and clearing the
        // results first would destroy a good one already on file.
        if (results.Count == 0 && narrative.Length == 0)
        {
            _logger.LogWarning(
                "ORU^R01 for order {Id} carried {Count} OBX segment(s) but none could be interpreted — order left unchanged.",
                order.Id, observations.Count);
            return;
        }

        if (results.Count > 0)
        {
            _db.LabResults.RemoveRange(order.Results);
            _db.LabResults.AddRange(results);
        }

        if (narrative.Length > 0)
            order.NarrativeFindings = narrative.ToString().TrimEnd();

        // This message is the reading, so its flag replaces ours outright —
        // a corrected result that is now within limits has to clear it.
        order.IsAbnormal = abnormal;

        var interpreter = message.GetResultInterpreter();
        if (!string.IsNullOrWhiteSpace(interpreter))
            order.ResultedBy = interpreter;

        order.Status = "Resulted";
        order.ProcessedAt = DateTime.UtcNow;
        order.LisStatus = "Acknowledged";
        order.LisAckedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "ORU^R01 for order {Id} processed — {Results} result(s), {Narrative} char(s) of findings.",
            order.Id, results.Count, narrative.Length);
    }

    /// <summary>
    /// Adds one observation that had no matching parameter to the findings
    /// buffer. Report prose is concatenated as-is, since a write-up arrives
    /// split across OBX segments and must read as continuous text; anything
    /// else keeps its label so a stray value stays attributable.
    /// </summary>
    private static void AppendToNarrative(StringBuilder narrative, ObxObservation obs)
    {
        if (string.IsNullOrWhiteSpace(obs.Value)) return;

        if (narrative.Length > 0) narrative.Append('\n');

        if (obs.IsNarrative)
        {
            narrative.Append(obs.Value);
            return;
        }

        var label = !string.IsNullOrWhiteSpace(obs.Name) ? obs.Name : obs.Code;
        narrative.Append(string.IsNullOrWhiteSpace(label) ? obs.Value : $"{label}: {obs.Value}");
    }

    private static string MapAbnormalFlag(string lisFlag) => lisFlag.ToUpper() switch
    {
        "H" or "HH" or ">" => "High",
        "L" or "LL" or "<" => "Low",
        _ => "Normal"
    };

    /// <summary>Whether an OBX-8 flag marks the observation as out of limits.</summary>
    private static bool IsAbnormalFlag(string lisFlag) => lisFlag.ToUpper() switch
    {
        "A" or "AA" or "H" or "HH" or "L" or "LL" or ">" or "<" => true,
        _ => false
    };
}
