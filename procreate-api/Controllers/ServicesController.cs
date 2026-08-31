using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

/// <summary>
/// The billable service catalogue. Backed by Product, which is what the
/// cashier sells through Order/OrderItem — Service Management is the same
/// records seen from the clinic-setup side, so there is one price list.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ServicesController(AppDbContext db) => _db = db;

    [HttpGet("categories")]
    public IActionResult GetCategories() => Ok(ServiceCategories.All);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _db.Products
            .Include(p => p.RequiredItems).ThenInclude(r => r.InventoryItem)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        // Unlike the cashier list this one shows retired services too, so the
        // setup screen can bring one back.
        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => p.IsActive);
        else if (string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => !p.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // LIKE rather than Contains, which SQLite makes case-sensitive.
            var term = $"%{search.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Name, term) || EF.Functions.Like(p.Code, term));
        }

        var total = await query.CountAsync();
        var services = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, data = services.Select(Project) });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var service = await _db.Products
            .Include(p => p.RequiredItems).ThenInclude(r => r.InventoryItem)
            .FirstOrDefaultAsync(p => p.Id == id);

        return service is null ? NotFound() : Ok(Project(service));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServiceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "A service name is required." });

        var code = string.IsNullOrWhiteSpace(request.Code)
            ? await NextCodeAsync(request.Category)
            : request.Code.Trim();

        if (await _db.Products.AnyAsync(p => p.Code == code))
            return BadRequest(new { message = $"The code \"{code}\" is already in use." });

        var service = new Product
        {
            Code = code,
            Name = request.Name.Trim(),
            Category = request.Category?.Trim() ?? "Others",
            Price = request.Price,
            Description = request.Description?.Trim() ?? string.Empty,
            SeniorPwdDiscount = request.SeniorPwdDiscount,
            PhilHealthCovered = request.PhilHealthCovered,
            IsActive = request.IsActive
        };

        _db.Products.Add(service);
        await _db.SaveChangesAsync();
        await ReplaceRequiredItemsAsync(service.Id, request.RequiredItems);

        return CreatedAtAction(nameof(GetById), new { id = service.Id }, Project(service));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ServiceRequest request)
    {
        var service = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (service is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "A service name is required." });

        var code = string.IsNullOrWhiteSpace(request.Code) ? service.Code : request.Code.Trim();
        if (await _db.Products.AnyAsync(p => p.Code == code && p.Id != id))
            return BadRequest(new { message = $"The code \"{code}\" is already in use." });

        service.Code = code;
        service.Name = request.Name.Trim();
        service.Category = request.Category?.Trim() ?? "Others";
        service.Price = request.Price;
        service.Description = request.Description?.Trim() ?? string.Empty;
        service.SeniorPwdDiscount = request.SeniorPwdDiscount;
        service.PhilHealthCovered = request.PhilHealthCovered;
        service.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        await ReplaceRequiredItemsAsync(id, request.RequiredItems);

        return Ok(Project(service));
    }

    /// <summary>
    /// Retires a service, or deletes it outright when nothing has been sold.
    /// A service named on a past order is kept so old bills still resolve.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var service = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (service is null) return NotFound();

        if (await _db.OrderItems.AnyAsync(i => i.ProductId == id))
        {
            service.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok(new { message = "The service has past orders, so it was deactivated instead of deleted.", deactivated = true });
        }

        _db.Products.Remove(service);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ----------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------

    private async Task ReplaceRequiredItemsAsync(int productId, List<RequiredItemInput>? items)
    {
        var existing = await _db.ServiceInventoryItems
            .Where(l => l.ProductId == productId).ToListAsync();
        _db.ServiceInventoryItems.RemoveRange(existing);

        // Same item twice would violate the unique index and only ever means
        // "use more of it", so the quantities are folded together.
        var merged = (items ?? new List<RequiredItemInput>())
            .Where(i => i.InventoryItemId > 0)
            .GroupBy(i => i.InventoryItemId)
            .Select(g => new ServiceInventoryItem
            {
                ProductId = productId,
                InventoryItemId = g.Key,
                Quantity = Math.Max(1, g.Sum(i => i.Quantity))
            });

        _db.ServiceInventoryItems.AddRange(merged);
        await _db.SaveChangesAsync();
    }

    /// <summary>Next free code for a category, e.g. US-005.</summary>
    private async Task<string> NextCodeAsync(string? category)
    {
        var prefix = (category ?? "Others") switch
        {
            "Consultation" => "CON",
            "Laboratory" => "LAB",
            "Imaging" => "XR",
            "Ultrasound" => "US",
            "Heart Station" => "HS",
            "Procedure" => "PRO",
            "Vaccination" => "VAX",
            "Dental" => "DEN",
            "Physical Therapy" => "PT",
            _ => "MISC"
        };

        var codes = await _db.Products
            .Where(p => p.Code.StartsWith(prefix + "-"))
            .Select(p => p.Code)
            .ToListAsync();

        var highest = codes
            .Select(c => int.TryParse(c[(prefix.Length + 1)..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}-{highest + 1:D3}";
    }

    private static object Project(Product p) => new
    {
        p.Id,
        p.Code,
        p.Name,
        p.Category,
        p.Price,
        p.Description,
        p.SeniorPwdDiscount,
        p.PhilHealthCovered,
        p.IsActive,
        requiredItems = p.RequiredItems.Select(r => new
        {
            r.InventoryItemId,
            r.Quantity,
            name = r.InventoryItem?.Name ?? string.Empty,
            unit = r.InventoryItem?.UnitOfMeasure ?? string.Empty
        })
    };
}

public record RequiredItemInput(int InventoryItemId, int Quantity);

public record ServiceRequest(
    string Name,
    string? Code,
    string? Category,
    decimal Price,
    string? Description,
    bool SeniorPwdDiscount,
    bool PhilHealthCovered,
    bool IsActive,
    List<RequiredItemInput>? RequiredItems);
