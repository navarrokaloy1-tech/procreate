using ProCreateApi.Data;
using ProCreateApi.Models;
using ProCreateApi.Services.Lis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VisitsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILisService _lis;
    public VisitsController(AppDbContext db, ILisService lis) { _db = db; _lis = lis; }

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
        var visit = new Visit
        {
            VisitCode = await NextVisitCodeAsync(),
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

        // Send each lab order to the LIS asynchronously (fire-and-forget with error isolation)
        var newOrders = await _db.LabOrders
            .Include(o => o.Visit).ThenInclude(v => v.Patient)
            .Include(o => o.LabTest)
            .Where(o => o.VisitId == visit.Id)
            .ToListAsync();
        foreach (var order in newOrders)
            _ = _lis.SendOrderAsync(order);

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

    /// <summary>
    /// Adds further tests to an existing consultation, so a patient needing
    /// extra work does not require a second visit.
    /// </summary>
    [HttpPost("{id}/tests")]
    public async Task<IActionResult> AddTests(int id, [FromBody] AddTestsRequest req)
    {
        var visit = await _db.Visits
            .Include(v => v.LabOrders)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (visit is null) return NotFound();

        if (visit.Status == "Cancelled")
            return BadRequest(new { message = "Cannot add tests to a cancelled consultation." });

        if (req.TestIds is null || req.TestIds.Count == 0)
            return BadRequest(new { message = "Select at least one test to add." });

        // Ignore tests already on the visit rather than duplicating an order.
        var existing = visit.LabOrders.Select(o => o.LabTestId).ToHashSet();
        var toAdd = req.TestIds.Distinct().Where(t => !existing.Contains(t)).ToList();

        if (toAdd.Count == 0)
            return BadRequest(new { message = "Those tests are already on this consultation." });

        var added = new List<LabOrder>();

        foreach (var testId in toAdd)
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
            added.Add(order);
            visit.TotalAmount += test.Price;
        }

        if (added.Count == 0)
            return BadRequest(new { message = "None of the selected tests exist." });

        // Adding billable work can un-settle a previously paid visit.
        visit.PaymentStatus = visit.AmountPaid >= visit.TotalAmount
            ? "Paid"
            : visit.AmountPaid > 0 ? "Partial" : "Unpaid";

        await _db.SaveChangesAsync();

        var newOrders = await _db.LabOrders
            .Include(o => o.Visit).ThenInclude(v => v.Patient)
            .Include(o => o.LabTest)
            .Where(o => added.Select(a => a.Id).Contains(o.Id))
            .ToListAsync();

        foreach (var order in newOrders)
            _ = _lis.SendOrderAsync(order);

        return Ok(new { added = added.Count, visit.TotalAmount, visit.PaymentStatus });
    }

    /// <summary>
    /// Removes a test from a consultation. Only allowed before the specimen is
    /// collected — after that the order is part of the lab's record.
    /// </summary>
    [HttpDelete("{id}/tests/{orderId}")]
    public async Task<IActionResult> RemoveTest(int id, int orderId)
    {
        var order = await _db.LabOrders
            .Include(o => o.LabTest)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.VisitId == id);

        if (order is null) return NotFound();

        if (order.Status != "Ordered" || order.CollectedAt is not null)
        {
            return BadRequest(new
            {
                message = "That test has already been collected and can no longer be removed."
            });
        }

        var visit = await _db.Visits.FindAsync(id);
        if (visit is null) return NotFound();

        visit.TotalAmount -= order.LabTest.Price;
        if (visit.TotalAmount < 0) visit.TotalAmount = 0;

        _db.LabOrders.Remove(order);
        await _db.SaveChangesAsync();

        return Ok(new { visit.TotalAmount });
    }

    /// <summary>
    /// Registers several consultations in one request — one per patient, each
    /// with its own tests. Used by the front desk when a group arrives
    /// together, e.g. company pre-employment screening.
    /// </summary>
    [HttpPost("batch")]
    public async Task<IActionResult> CreateBatch([FromBody] BatchVisitRequest req)
    {
        if (req.Entries is null || req.Entries.Count == 0)
            return BadRequest(new { message = "Add at least one patient to the batch." });

        var patientIds = req.Entries.Select(e => e.PatientId).ToList();

        var knownIds = await _db.Patients
            .Where(p => patientIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync();

        var missing = patientIds.Except(knownIds).ToList();
        if (missing.Count > 0)
            return BadRequest(new { message = $"Unknown patient id(s): {string.Join(", ", missing)}." });

        var duplicates = patientIds.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            return BadRequest(new { message = "The same patient appears more than once in the batch." });

        // Allocate codes from a single base: NextVisitCodeAsync would return
        // the same value for every entry until the batch is saved.
        var sequence = await NextVisitSequenceAsync();
        var prefix = $"VS-{DateTime.Now:yyyyMMdd}-";

        var created = new List<Visit>();

        foreach (var entry in req.Entries)
        {
            var visit = new Visit
            {
                VisitCode = prefix + (sequence++).ToString("D4"),
                PatientId = entry.PatientId,
                ReferringPhysician = entry.ReferringPhysician ?? req.ReferringPhysician ?? "",
                Purpose = entry.PurposeOfVisit ?? req.PurposeOfVisit ?? "",
                Status = "Registered",
                VisitDate = DateTime.UtcNow
            };

            _db.Visits.Add(visit);
            created.Add(visit);
        }

        await _db.SaveChangesAsync();

        for (var i = 0; i < req.Entries.Count; i++)
        {
            var entry = req.Entries[i];
            var visit = created[i];
            decimal total = 0;

            foreach (var testId in (entry.TestIds ?? new List<int>()).Distinct())
            {
                var test = await _db.LabTests.FindAsync(testId);
                if (test is null) continue;

                _db.LabOrders.Add(new LabOrder
                {
                    VisitId = visit.Id,
                    LabTestId = testId,
                    Status = "Ordered",
                    SpecimenBarcode = $"SPX-{DateTime.Now:yyyyMMddHHmmss}-{visit.Id}-{testId}"
                });

                total += test.Price;
            }

            visit.TotalAmount = total;
        }

        await _db.SaveChangesAsync();

        var orders = await _db.LabOrders
            .Include(o => o.Visit).ThenInclude(v => v.Patient)
            .Include(o => o.LabTest)
            .Where(o => created.Select(c => c.Id).Contains(o.VisitId))
            .ToListAsync();

        foreach (var order in orders)
            _ = _lis.SendOrderAsync(order);

        return Ok(new
        {
            created = created.Count,
            visits = created.Select(v => new { v.Id, v.VisitCode, v.PatientId, v.TotalAmount })
        });
    }

    /// <summary>Next sequence number for today's visit codes.</summary>
    private async Task<int> NextVisitSequenceAsync()
    {
        var prefix = $"VS-{DateTime.Now:yyyyMMdd}-";

        var latest = await _db.Visits
            .Where(v => v.VisitCode.StartsWith(prefix))
            .OrderByDescending(v => v.VisitCode)
            .Select(v => v.VisitCode)
            .FirstOrDefaultAsync();

        if (latest is not null && int.TryParse(latest[prefix.Length..], out var n))
            return n + 1;

        return 1;
    }

    /// <summary>
    /// Date-scoped visit code. The previous scheme numbered from the total
    /// visit count, which drifts from the date and collides once rows are
    /// deleted.
    /// </summary>
    private async Task<string> NextVisitCodeAsync()
    {
        var sequence = await NextVisitSequenceAsync();
        return $"VS-{DateTime.Now:yyyyMMdd}-{sequence:D4}";
    }
}

public record CreateVisitRequest(int PatientId, string ReferringPhysician, string PurposeOfVisit, string? Notes, List<int> TestIds);
public record UpdateStatusRequest(string Status);
public record AddTestsRequest(List<int> TestIds);
public record BatchVisitEntry(int PatientId, string? ReferringPhysician, string? PurposeOfVisit, List<int>? TestIds);
public record BatchVisitRequest(string? ReferringPhysician, string? PurposeOfVisit, List<BatchVisitEntry> Entries);
