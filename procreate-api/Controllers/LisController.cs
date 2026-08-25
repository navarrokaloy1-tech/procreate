using ProCreateApi.Data;
using ProCreateApi.Services.Lis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LisController : ControllerBase
{
    private readonly ILisService _lis;
    private readonly AppDbContext _db;

    public LisController(ILisService lis, AppDbContext db)
    {
        _lis = lis;
        _db = db;
    }

    /// <summary>Returns LIS configuration and integration status summary.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var info = _lis.GetConnectionInfo();
        var pendingCount = await _db.LabOrders.CountAsync(o => o.LisStatus == null && o.Status == "Ordered");
        var failedCount = await _db.LabOrders.CountAsync(o => o.LisStatus == "Failed");
        var sentCount = await _db.LabOrders.CountAsync(o => o.LisStatus == "Acknowledged");

        return Ok(new
        {
            info.Enabled,
            info.Host,
            info.Port,
            info.ListenPort,
            orders = new { pending = pendingCount, failed = failedCount, acknowledged = sentCount }
        });
    }

    /// <summary>Returns all lab orders with their LIS status.</summary>
    [HttpGet("orders")]
    public async Task<IActionResult> GetLisOrders([FromQuery] string? lisStatus)
    {
        var query = _db.LabOrders
            .Include(o => o.Visit).ThenInclude(v => v.Patient)
            .Include(o => o.LabTest)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(lisStatus))
            query = lisStatus == "pending"
                ? query.Where(o => o.LisStatus == null)
                : query.Where(o => o.LisStatus == lisStatus);

        var orders = await query.OrderByDescending(o => o.Id).ToListAsync();
        return Ok(orders.Select(o => new
        {
            o.Id,
            barcode = o.SpecimenBarcode,
            patient = $"{o.Visit.Patient.FirstName} {o.Visit.Patient.LastName}".Trim(),
            test = o.LabTest.Name,
            o.Status,
            o.LisStatus,
            o.LisOrderId,
            o.LisSentAt,
            o.LisAckedAt,
            o.LisError
        }));
    }

    /// <summary>Manually retry sending a failed order to the LIS.</summary>
    [HttpPost("orders/{id}/retry")]
    public async Task<IActionResult> Retry(int id, CancellationToken ct)
    {
        try
        {
            await _lis.RetryOrderAsync(id, ct);
            return Ok(new { message = "Retry sent." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Manually re-sync a patient's ADT to the LIS.</summary>
    [HttpPost("patients/{id}/sync")]
    public async Task<IActionResult> SyncPatient(int id, CancellationToken ct)
    {
        var patient = await _db.Patients.FindAsync([id], ct);
        if (patient is null) return NotFound();

        await _lis.SendPatientAsync(patient, isUpdate: true, ct);
        return Ok(new { message = "ADT^A08 sent.", sentAt = patient.LisRegisteredAt });
    }
}
