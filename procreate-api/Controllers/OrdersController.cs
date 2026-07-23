using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    public OrdersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = _db.Orders.Include(o => o.Patient).Include(o => o.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(o => o.OrderCode.Contains(search) ||
                                     o.Patient.FirstName.Contains(search) ||
                                     o.Patient.LastName.Contains(search) ||
                                     o.Patient.PatientCode.Contains(search));

        var total = await query.CountAsync();
        var orders = await query.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var data = orders.Select(o => new
        {
            o.Id,
            o.OrderCode,
            o.PatientId,
            patientName = $"{o.Patient.LastName}, {o.Patient.FirstName} {o.Patient.MiddleName}".Trim(),
            patientCode = o.Patient.PatientCode,
            o.Status,
            itemsCount = o.Items.Sum(i => i.Quantity),
            o.SubTotal,
            o.Total,
            o.CreatedAt,
            o.UpdatedAt
        });
        return Ok(new { total, page, pageSize, data });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Patient)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();
        return Ok(MapDetail(order));
    }

    [HttpGet("patient/{patientId}")]
    public async Task<IActionResult> GetByPatient(int patientId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = _db.Orders.Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.PatientId == patientId);

        var total = await query.CountAsync();
        var orders = await query.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // One row per line item, matching the History table (Product / Updates / Status).
        var data = orders.SelectMany(o => o.Items.Select(i => new
        {
            orderId = o.Id,
            o.OrderCode,
            productName = i.Product.Name,
            quantity = i.Quantity,
            o.Status,
            updatedAt = o.UpdatedAt,
            createdAt = o.CreatedAt
        }));

        return Ok(new { total, page, pageSize, data });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req)
    {
        var patient = await _db.Patients.FindAsync(req.PatientId);
        if (patient is null) return NotFound("Patient not found");

        var count = await _db.Orders.CountAsync();
        var status = string.Equals(req.Status, "Draft", StringComparison.OrdinalIgnoreCase) ? "Draft" : "Ordered";

        var order = new Order
        {
            OrderCode = $"OR-{DateTime.Now:yyyyMMdd}-{(count + 1):D4}",
            PatientId = req.PatientId,
            Status = status,
            Referrer = req.Referrer ?? string.Empty,
            Tags = req.Tags ?? string.Empty,
            Branch = req.Branch ?? string.Empty,
            ContactNumber = req.ContactNumber ?? string.Empty,
            ContactEmail = req.ContactEmail ?? string.Empty,
            Notes = req.Notes ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var line in req.Items ?? new List<OrderLineRequest>())
        {
            var product = await _db.Products.FindAsync(line.ProductId);
            if (product is null) continue;
            var qty = line.Quantity < 1 ? 1 : line.Quantity;
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = qty,
                UnitPrice = product.Price,
                LineTotal = product.Price * qty
            });
        }

        order.SubTotal = order.Items.Sum(i => i.LineTotal);
        order.Total = order.SubTotal;

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var saved = await _db.Orders
            .Include(o => o.Patient)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstAsync(o => o.Id == order.Id);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, MapDetail(saved));
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest req)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order is null) return NotFound();
        order.Status = req.Status;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(order);
    }

    private static object MapDetail(Order o) => new
    {
        o.Id,
        o.OrderCode,
        o.PatientId,
        patientName = $"{o.Patient.LastName}, {o.Patient.FirstName} {o.Patient.MiddleName}".Trim(),
        patientCode = o.Patient.PatientCode,
        o.Status,
        o.Referrer,
        o.Tags,
        o.Branch,
        o.ContactNumber,
        o.ContactEmail,
        o.Notes,
        o.SubTotal,
        o.Total,
        o.CreatedAt,
        o.UpdatedAt,
        items = o.Items.Select(i => new
        {
            i.Id,
            i.ProductId,
            productName = i.Product.Name,
            productCode = i.Product.Code,
            i.Quantity,
            i.UnitPrice,
            i.LineTotal
        })
    };
}

public record CreateOrderRequest(
    int PatientId,
    string Status,
    string? Referrer,
    string? Tags,
    string? Branch,
    string? ContactNumber,
    string? ContactEmail,
    string? Notes,
    List<OrderLineRequest>? Items);

public record OrderLineRequest(int ProductId, int Quantity);
public record UpdateOrderStatusRequest(string Status);
