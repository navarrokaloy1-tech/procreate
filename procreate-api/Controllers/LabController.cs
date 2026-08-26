using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LabController : ControllerBase
{
    private readonly AppDbContext _db;
    public LabController(AppDbContext db) => _db = db;

    [HttpGet("tests")]
    public async Task<IActionResult> GetTests()
    {
        var tests = await _db.LabTests.Include(t => t.Category).Include(t => t.Parameters).ToListAsync();
        return Ok(tests);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var cats = await _db.TestCategories.Include(c => c.Tests).ToListAsync();
        return Ok(cats);
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders([FromQuery] string? status)
    {
        var query = _db.LabOrders
            .Include(o => o.Visit).ThenInclude(v => v.Patient)
            .Include(o => o.LabTest)
            .Include(o => o.Results)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status);
        var orders = await query.OrderByDescending(o => o.Id).ToListAsync();
        var data = orders.Select(o => new
        {
            o.Id,
            orderCode = $"ORD-{o.Id:D5}",
            visitCode = o.Visit.VisitCode,
            patientName = $"{o.Visit.Patient.FirstName} {o.Visit.Patient.LastName}".Trim(),
            patientCode = o.Visit.Patient.PatientCode,
            testName = o.LabTest.Name,
            testCode = o.LabTest.Code,
            o.SpecimenBarcode,
            o.Status,
            orderedDate = o.Visit.VisitDate,
            collectedDate = o.CollectedAt
        });
        return Ok(data);
    }

    [HttpGet("orders/{id}")]
    public async Task<IActionResult> GetOrder(int id)
    {
        var order = await _db.LabOrders
            .Include(o => o.Visit).ThenInclude(v => v.Patient)
            .Include(o => o.LabTest).ThenInclude(t => t.Parameters)
            .Include(o => o.Results).ThenInclude(r => r.TestParameter)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        static decimal? ParseNullable(string? s) => decimal.TryParse(s, out var d) ? d : (decimal?)null;

        var result = new
        {
            order.Id,
            orderCode = $"ORD-{order.Id:D5}",
            patientName = $"{order.Visit.Patient.FirstName} {order.Visit.Patient.LastName}".Trim(),
            testName = order.LabTest.Name,
            order.Status,
            parameters = order.LabTest.Parameters.Select(p =>
            {
                var existing = order.Results.FirstOrDefault(r => r.TestParameterId == p.Id);
                return new
                {
                    id = p.Id,
                    parameterName = p.Name,
                    unit = p.Unit,
                    referenceRangeMin = ParseNullable(p.NormalMin),
                    referenceRangeMax = ParseNullable(p.NormalMax),
                    referenceRangeText = p.ReferenceRange,
                    value = existing?.Value ?? "",
                    flag = existing?.Flag ?? ""
                };
            })
        };
        return Ok(result);
    }

    [HttpPost("orders/{id}/collect")]
    public async Task<IActionResult> CollectSpecimen(int id)
    {
        var order = await _db.LabOrders.FindAsync(id);
        if (order is null) return NotFound();
        order.Status = "Collected";
        order.CollectedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(order);
    }

    [HttpPost("orders/{id}/results")]
    public async Task<IActionResult> EncodeResults(int id, [FromBody] EncodeResultsRequest request)
    {
        var order = await _db.LabOrders
            .Include(o => o.LabTest).ThenInclude(t => t.Parameters)
            .Include(o => o.Results)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        _db.LabResults.RemoveRange(order.Results);
        foreach (var entry in request.Results)
        {
            var param = order.LabTest.Parameters.FirstOrDefault(p => p.Id == entry.ParameterId);
            string flag = "Normal";
            if (param != null && double.TryParse(entry.Value, out var val))
            {
                if (double.TryParse(param.NormalMin, out var min) && val < min) flag = "Low";
                else if (double.TryParse(param.NormalMax, out var max) && val > max) flag = "High";
            }
            _db.LabResults.Add(new LabResult
            {
                LabOrderId = id,
                TestParameterId = entry.ParameterId,
                Value = entry.Value,
                Flag = flag,
                Remarks = entry.Remarks
            });
        }
        order.Status = "Resulted";
        order.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Results saved" });
    }

    [HttpPost("orders/{id}/release")]
    public async Task<IActionResult> ReleaseResult(int id)
    {
        var order = await _db.LabOrders.FindAsync(id);
        if (order is null) return NotFound();
        order.Status = "Released";
        order.ReleasedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(order);
    }

    [HttpGet("orders/{id}/qr")]
    public async Task<IActionResult> GetQrCode(int id)
    {
        var order = await _db.LabOrders.Include(o => o.Visit).ThenInclude(v => v.Patient).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        using var qrGenerator = new QRCoder.QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode($"PRO-CREATE|ORDER:{id}|PATIENT:{order.Visit.PatientId}|BARCODE:{order.SpecimenBarcode}", QRCoder.QRCodeGenerator.ECCLevel.Q);
        var qrCode = new QRCoder.PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(10);
        return File(qrBytes, "image/png");
    }
}

public record ResultEntry(int ParameterId, string Value, string? Remarks);
public record EncodeResultsRequest(List<ResultEntry> Results, string? Remarks);
