using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BillingController : ControllerBase
{
    private readonly AppDbContext _db;
    public BillingController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = _db.Bills.Include(b => b.Visit).ThenInclude(v => v.Patient).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(b => b.Status == status);
        var total = await query.CountAsync();
        var bills = await query.OrderByDescending(b => b.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var data = bills.Select(b => new
        {
            b.Id,
            b.BillNumber,
            visitCode = b.Visit.VisitCode,
            patientName = $"{b.Visit.Patient.FirstName} {b.Visit.Patient.LastName}".Trim(),
            totalAmount = b.Total,
            discountAmount = b.Discount,
            netAmount = b.Total,
            b.AmountPaid,
            changeAmount = b.Change,
            b.PaymentMethod,
            b.Status,
            b.CreatedAt
        });
        return Ok(new { total, page, pageSize, data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var bill = await _db.Bills.Include(b => b.Visit).ThenInclude(v => v.Patient)
            .Include(b => b.Visit).ThenInclude(v => v.LabOrders).ThenInclude(o => o.LabTest)
            .FirstOrDefaultAsync(b => b.Id == id);
        return bill is null ? NotFound() : Ok(bill);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBillRequest req)
    {
        var visit = await _db.Visits.Include(v => v.LabOrders).ThenInclude(o => o.LabTest).FirstOrDefaultAsync(v => v.Id == req.VisitId);
        if (visit is null) return NotFound("Visit not found");

        var count = await _db.Bills.CountAsync();
        var subTotal = visit.LabOrders.Sum(o => o.LabTest.Price);
        var discount = req.DiscountAmount;
        var total = subTotal - discount;

        var bill = new Bill
        {
            BillNumber = $"BL-{DateTime.Now:yyyyMMdd}-{(count + 1):D4}",
            VisitId = req.VisitId,
            SubTotal = subTotal,
            Discount = discount,
            Total = total,
            AmountPaid = req.AmountPaid,
            Change = req.AmountPaid - total,
            PaymentMethod = req.PaymentMethod,
            Status = req.AmountPaid >= total ? "Paid" : "Partial",
            CreatedAt = DateTime.UtcNow
        };

        visit.AmountPaid = req.AmountPaid;
        visit.PaymentStatus = bill.Status;
        visit.PaymentMethod = req.PaymentMethod;
        visit.TotalAmount = total;

        _db.Bills.Add(bill);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = bill.Id }, bill);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var bills = await _db.Bills.Where(b => b.CreatedAt >= fromDate && b.CreatedAt <= toDate && b.Status == "Paid").ToListAsync();

        return Ok(new
        {
            totalRevenue = bills.Sum(b => b.Total),
            totalBills = bills.Count,
            averageBill = bills.Count > 0 ? bills.Average(b => b.Total) : 0,
            byMethod = bills.GroupBy(b => b.PaymentMethod).Select(g => new { method = g.Key, amount = g.Sum(b => b.Total), count = g.Count() })
        });
    }
}

public record CreateBillRequest(int VisitId, decimal DiscountAmount, decimal AmountPaid, string PaymentMethod, string? Notes);
