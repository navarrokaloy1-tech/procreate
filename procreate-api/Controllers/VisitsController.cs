using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VisitsController : ControllerBase
{
    private readonly AppDbContext _db;
    public VisitsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = _db.Visits.Include(v => v.Patient).Include(v => v.LabOrders).ThenInclude(o => o.LabTest).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(v => v.Status == status);
        var total = await query.CountAsync();
        var visits = await query.OrderByDescending(v => v.VisitDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var data = visits.Select(v => new
        {
            v.Id,
            v.VisitCode,
            v.PatientId,
            patientName = $"{v.Patient.FirstName} {v.Patient.LastName}".Trim(),
            patientCode = v.Patient.PatientCode,
            v.VisitDate,
            v.ReferringPhysician,
            v.Purpose,
            v.Status,
            testsCount = v.LabOrders.Count,
            v.TotalAmount,
            v.AmountPaid,
            v.PaymentStatus
        });
        return Ok(new { total, page, pageSize, data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var visit = await _db.Visits
            .Include(v => v.Patient)
            .Include(v => v.LabOrders).ThenInclude(o => o.LabTest).ThenInclude(t => t.Parameters)
            .Include(v => v.LabOrders).ThenInclude(o => o.Results).ThenInclude(r => r.TestParameter)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (visit is null) return NotFound();

        var result = new
        {
            visit.Id,
            visit.VisitCode,
            visit.PatientId,
            patientName = $"{visit.Patient.FirstName} {visit.Patient.LastName}".Trim(),
            patientCode = visit.Patient.PatientCode,
            visit.VisitDate,
            visit.ReferringPhysician,
            visit.Purpose,
            visit.Status,
            visit.TotalAmount,
            visit.AmountPaid,
            visit.PaymentStatus,
            visit.PaymentMethod,
            tests = visit.LabOrders.Select(o => new { testName = o.LabTest.Name, o.LabTest.Code, price = o.LabTest.Price }),
            labOrders = visit.LabOrders
        };
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVisitRequest req)
    {
        var count = await _db.Visits.CountAsync();
        var visit = new Visit
        {
            VisitCode = $"VS-{DateTime.Now:yyyyMMdd}-{(count + 1):D4}",
            PatientId = req.PatientId,
            ReferringPhysician = req.ReferringPhysician,
            Purpose = req.PurposeOfVisit,
            Status = "Registered",
            VisitDate = DateTime.UtcNow
        };
        _db.Visits.Add(visit);
        await _db.SaveChangesAsync();

        decimal total = 0;
        foreach (var testId in req.TestIds)
        {
            var test = await _db.LabTests.FindAsync(testId);
            if (test is null) continue;
            var order = new LabOrder
            {
                VisitId = visit.Id,
                LabTestId = testId,
                Status = "Ordered",
                SpecimenBarcode = $"SPX-{DateTime.Now:yyyyMMddHHmmss}-{testId}"
            };
            _db.LabOrders.Add(order);
            total += test.Price;
        }
        visit.TotalAmount = total;
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = visit.Id }, visit);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest req)
    {
        var visit = await _db.Visits.FindAsync(id);
        if (visit is null) return NotFound();
        visit.Status = req.Status;
        await _db.SaveChangesAsync();
        return Ok(visit);
    }
}

public record CreateVisitRequest(int PatientId, string ReferringPhysician, string PurposeOfVisit, string? Notes, List<int> TestIds);
public record UpdateStatusRequest(string Status);
