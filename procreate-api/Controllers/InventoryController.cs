using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Controllers;

/// <summary>
/// Stock the clinic consumes or owns: medicines, consumables and assets,
/// with the categories and suppliers they are filed under.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly AppDbContext _db;
    public InventoryController(AppDbContext db) => _db = db;

    // ----------------------------------------------------------
    // Dashboard
    // ----------------------------------------------------------

    /// <summary>
    /// The counts behind the dashboard tiles, plus the items driving each
    /// alert so the panel can name them rather than only counting.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var items = await _db.InventoryItems
            .Where(i => i.IsActive)
            .Include(i => i.Category)
            .ToListAsync();

        // Out of stock is its own tile, so low stock deliberately excludes it —
        // otherwise every empty item would be counted under both.
        var outOfStock = items.Where(i => i.CurrentStock <= 0).ToList();
        var lowStock = items
            .Where(i => i.CurrentStock > 0 && i.CurrentStock <= i.ReorderLevel)
            .ToList();

        return Ok(new
        {
            totalItems = items.Count,
            lowStock = lowStock.Count,
            outOfStock = outOfStock.Count,
            byType = ItemTypes.All.Select(t => new
            {
                type = t,
                count = items.Count(i => i.ItemType == t)
            }),
            outOfStockItems = outOfStock.OrderBy(i => i.Name).Select(Brief),
            lowStockItems = lowStock.OrderBy(i => i.CurrentStock).Select(Brief)
        });
    }

    // ----------------------------------------------------------
    // Items
    // ----------------------------------------------------------

    [HttpGet("items")]
    public async Task<IActionResult> GetItems(
        [FromQuery] string? search,
        [FromQuery] string? itemType,
        [FromQuery] int? categoryId,
        [FromQuery] string? stock,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _db.InventoryItems
            .Include(i => i.Category)
            .Include(i => i.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(itemType))
            query = query.Where(i => i.ItemType == itemType);

        if (categoryId is > 0)
            query = query.Where(i => i.CategoryId == categoryId);

        query = stock switch
        {
            "Out of Stock" => query.Where(i => i.CurrentStock <= 0),
            "Low Stock" => query.Where(i => i.CurrentStock > 0 && i.CurrentStock <= i.ReorderLevel),
            "In Stock" => query.Where(i => i.CurrentStock > i.ReorderLevel),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            // LIKE rather than Contains: SQLite translates Contains to a
            // case-sensitive instr(), so "gel" would miss "Ultrasound Gel".
            var term = $"%{search.Trim()}%";
            query = query.Where(i =>
                EF.Functions.Like(i.Name, term) ||
                EF.Functions.Like(i.BrandName, term) ||
                EF.Functions.Like(i.Sku, term));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(i => i.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, data = items.Select(Project) });
    }

    [HttpGet("items/{id:int}")]
    public async Task<IActionResult> GetItem(int id)
    {
        var item = await _db.InventoryItems
            .Include(i => i.Category)
            .Include(i => i.Supplier)
            .FirstOrDefaultAsync(i => i.Id == id);

        return item is null ? NotFound() : Ok(Project(item));
    }

    [HttpPost("items")]
    public async Task<IActionResult> CreateItem([FromBody] InventoryItemRequest request)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(new { message = error });

        var item = new InventoryItem();
        Apply(item, request);

        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, Project(item));
    }

    [HttpPut("items/{id:int}")]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] InventoryItemRequest request)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();

        var error = Validate(request);
        if (error is not null) return BadRequest(new { message = error });

        Apply(item, request);
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(Project(item));
    }

    /// <summary>
    /// Adjusts stock on hand. The delta is signed, so a correction downwards
    /// is the same call. Stock is never allowed below zero.
    /// </summary>
    [HttpPost("items/{id:int}/stock")]
    public async Task<IActionResult> AdjustStock(int id, [FromBody] StockAdjustmentRequest request)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();

        var updated = item.CurrentStock + request.Delta;
        if (updated < 0)
            return BadRequest(new { message = $"That would leave {item.Name} at {updated}. Only {item.CurrentStock} in stock." });

        item.CurrentStock = updated;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(Project(item));
    }

    /// <summary>
    /// Retires an item, or deletes it when nothing references it. An item a
    /// service depends on is kept so the recipe does not lose a line.
    /// </summary>
    [HttpDelete("items/{id:int}")]
    public async Task<IActionResult> DeleteItem(int id)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();

        if (await _db.ServiceInventoryItems.AnyAsync(l => l.InventoryItemId == id))
        {
            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { message = "The item is used by a service, so it was deactivated instead of deleted.", deactivated = true });
        }

        _db.InventoryItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ----------------------------------------------------------
    // Categories and suppliers
    // ----------------------------------------------------------

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _db.InventoryCategories.OrderBy(c => c.Name).ToListAsync();
        var counts = await _db.InventoryItems
            .Where(i => i.CategoryId != null)
            .GroupBy(i => i.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(categories.Select(c => new
        {
            c.Id,
            c.Name,
            c.Description,
            itemCount = counts.FirstOrDefault(x => x.CategoryId == c.Id)?.Count ?? 0
        }));
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] NamedRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "A category name is required." });

        var name = request.Name.Trim();
        if (await _db.InventoryCategories.AnyAsync(c => c.Name == name))
            return BadRequest(new { message = $"\"{name}\" already exists." });

        var category = new InventoryCategory { Name = name, Description = request.Description?.Trim() ?? string.Empty };
        _db.InventoryCategories.Add(category);
        await _db.SaveChangesAsync();

        return Ok(new { category.Id, category.Name, category.Description, itemCount = 0 });
    }

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.InventoryCategories.FirstOrDefaultAsync(c => c.Id == id);
        if (category is null) return NotFound();

        // Items survive: the FK is configured to null out rather than cascade.
        _db.InventoryCategories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers()
    {
        var suppliers = await _db.Suppliers
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

        // Counted here rather than in the client, which only ever holds one
        // page of items and would undercount.
        var counts = await _db.InventoryItems
            .Where(i => i.SupplierId != null)
            .GroupBy(i => i.SupplierId!.Value)
            .Select(g => new { SupplierId = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(suppliers.Select(s => new
        {
            s.Id,
            s.Name,
            s.ContactPerson,
            s.ContactNumber,
            s.Email,
            s.Address,
            itemCount = counts.FirstOrDefault(c => c.SupplierId == s.Id)?.Count ?? 0
        }));
    }

    [HttpPost("suppliers")]
    public async Task<IActionResult> CreateSupplier([FromBody] SupplierRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "A supplier name is required." });

        var supplier = new Supplier
        {
            Name = request.Name.Trim(),
            ContactPerson = request.ContactPerson?.Trim() ?? string.Empty,
            ContactNumber = request.ContactNumber?.Trim() ?? string.Empty,
            Email = request.Email?.Trim() ?? string.Empty,
            Address = request.Address?.Trim() ?? string.Empty
        };

        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            supplier.Id, supplier.Name, supplier.ContactPerson,
            supplier.ContactNumber, supplier.Email, supplier.Address,
            itemCount = 0
        });
    }

    // ----------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------

    private static string? Validate(InventoryItemRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "An item name is required.";
        if (!ItemTypes.All.Contains(r.ItemType)) return "Choose a valid item type.";
        if (string.IsNullOrWhiteSpace(r.UnitOfMeasure)) return "A unit of measure is required.";
        if (r.CostPrice < 0 || r.SellingPrice < 0) return "Prices cannot be negative.";
        if (r.CurrentStock < 0) return "Stock cannot be negative.";

        // A conversion factor without a sub-unit describes nothing, and a
        // sub-unit without a factor cannot be converted.
        if (!string.IsNullOrWhiteSpace(r.SubUnit) && (r.ConversionFactor is null or < 1))
            return "Set how many sub-units make up one unit.";

        return null;
    }

    private static void Apply(InventoryItem item, InventoryItemRequest r)
    {
        item.ItemType = r.ItemType;
        item.CategoryId = r.CategoryId is > 0 ? r.CategoryId : null;
        item.Name = r.Name.Trim();
        item.BrandName = r.BrandName?.Trim() ?? string.Empty;
        item.Dosage = r.Dosage?.Trim() ?? string.Empty;
        item.SupplierId = r.SupplierId is > 0 ? r.SupplierId : null;
        item.UnitOfMeasure = r.UnitOfMeasure.Trim();
        item.SubUnit = r.SubUnit?.Trim() ?? string.Empty;
        item.ConversionFactor = string.IsNullOrWhiteSpace(item.SubUnit) ? null : r.ConversionFactor;
        item.Sku = r.Sku?.Trim() ?? string.Empty;
        item.CostPrice = r.CostPrice;
        item.SellingPrice = r.SellingPrice;
        item.CurrentStock = r.CurrentStock;
        item.ReorderLevel = Math.Max(0, r.ReorderLevel);
        item.MinOrderQty = Math.Max(1, r.MinOrderQty);
        item.Description = r.Description?.Trim() ?? string.Empty;
        item.IsActive = r.IsActive;
    }

    /// <summary>Where an item sits against its reorder level.</summary>
    private static string StockStateFor(InventoryItem i) =>
        i.CurrentStock <= 0 ? "Out of Stock"
        : i.CurrentStock <= i.ReorderLevel ? "Low Stock"
        : "In Stock";

    private static object Brief(InventoryItem i) => new
    {
        i.Id,
        i.Name,
        i.BrandName,
        i.CurrentStock,
        i.ReorderLevel,
        i.UnitOfMeasure,
        stockState = StockStateFor(i)
    };

    private static object Project(InventoryItem i) => new
    {
        i.Id,
        i.ItemType,
        i.CategoryId,
        categoryName = i.Category?.Name ?? string.Empty,
        i.Name,
        i.BrandName,
        i.Dosage,
        i.SupplierId,
        supplierName = i.Supplier?.Name ?? string.Empty,
        i.UnitOfMeasure,
        i.SubUnit,
        i.ConversionFactor,
        i.Sku,
        i.CostPrice,
        i.SellingPrice,
        i.CurrentStock,
        i.ReorderLevel,
        i.MinOrderQty,
        i.Description,
        i.IsActive,
        stockState = StockStateFor(i)
    };
}

public record NamedRequest(string Name, string? Description);

public record SupplierRequest(
    string Name,
    string? ContactPerson,
    string? ContactNumber,
    string? Email,
    string? Address);

public record StockAdjustmentRequest(int Delta, string? Reason);

public record InventoryItemRequest(
    string ItemType,
    int? CategoryId,
    string Name,
    string? BrandName,
    string? Dosage,
    int? SupplierId,
    string UnitOfMeasure,
    string? SubUnit,
    int? ConversionFactor,
    string? Sku,
    decimal CostPrice,
    decimal SellingPrice,
    int CurrentStock,
    int ReorderLevel,
    int MinOrderQty,
    string? Description,
    bool IsActive);
