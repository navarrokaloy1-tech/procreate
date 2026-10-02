using ProCreateApi.Data;
using ProCreateApi.Models;
using ProCreateApi.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
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
    private readonly InventoryLedger _ledger;
    public InventoryController(AppDbContext db, InventoryLedger ledger)
    {
        _db = db;
        _ledger = ledger;
    }

    /// <summary>Whoever is acting, for the stock ledger. Blank only off a request.</summary>
    private string ActingUser => User.Identity?.Name ?? "system";

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
    /// A manual signed correction to stock on hand — a recount, breakage or
    /// write-off. Downwards is the same call with a negative delta; stock is
    /// never allowed below zero. Recorded in the ledger with its reason.
    /// </summary>
    [HttpPost("items/{id:int}/stock")]
    public async Task<IActionResult> AdjustStock(int id, [FromBody] StockAdjustmentRequest request)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();
        if (request.Delta == 0)
            return BadRequest(new { message = "Enter a non-zero adjustment." });

        if (request.Delta < 0 && item.CurrentStock + request.Delta < 0)
            return BadRequest(new { message = $"That would leave {item.Name} below zero. Only {item.CurrentStock} in stock." });

        if (request.Delta > 0)
            _ledger.AdjustUp(item, request.Delta, request.Reason ?? "Manual adjustment", ActingUser);
        else
            _ledger.Issue(item, -request.Delta, StockMovementTypes.Adjustment,
                request.Reason ?? "Manual adjustment", string.Empty, ActingUser);

        await _db.SaveChangesAsync();
        return Ok(Project(item));
    }

    /// <summary>Receives stock in, as a dated lot carrying its own expiry.</summary>
    [HttpPost("items/{id:int}/stock-in")]
    public async Task<IActionResult> StockIn(int id, [FromBody] StockInRequest request)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();
        if (request.Quantity <= 0)
            return BadRequest(new { message = "Enter how many units are coming in." });

        _ledger.StockIn(item, request.Quantity, request.Reason ?? "Stock received",
            ActingUser, request.BatchNumber, request.ExpiryDate);

        await _db.SaveChangesAsync();
        return Ok(Project(item));
    }

    /// <summary>Issues stock out by hand — used, dispensed or discarded.</summary>
    [HttpPost("items/{id:int}/stock-out")]
    public async Task<IActionResult> StockOut(int id, [FromBody] StockOutRequest request)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound();
        if (request.Quantity <= 0)
            return BadRequest(new { message = "Enter how many units are going out." });
        if (item.CurrentStock < request.Quantity)
            return BadRequest(new { message = $"Only {item.CurrentStock} {item.UnitOfMeasure} of {item.Name} in stock." });

        _ledger.Issue(item, request.Quantity, StockMovementTypes.StockOut,
            request.Reason ?? "Stock issued", request.Reference ?? string.Empty, ActingUser);

        await _db.SaveChangesAsync();
        return Ok(Project(item));
    }

    // ----------------------------------------------------------
    // Stock ledger (audit trail) and batches
    // ----------------------------------------------------------

    /// <summary>The whole stock ledger, newest first — the movement audit trail.</summary>
    [HttpGet("movements")]
    public async Task<IActionResult> GetMovements(
        [FromQuery] int? itemId,
        [FromQuery] string? movementType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        var query = _db.StockMovements.Include(m => m.InventoryItem).AsQueryable();
        if (itemId is > 0) query = query.Where(m => m.InventoryItemId == itemId);
        if (!string.IsNullOrWhiteSpace(movementType)) query = query.Where(m => m.MovementType == movementType);

        var total = await query.CountAsync();
        var data = await query
            .OrderByDescending(m => m.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.InventoryItemId,
                itemName = m.InventoryItem.Name,
                unit = m.InventoryItem.UnitOfMeasure,
                m.MovementType,
                m.QuantityChange,
                m.BalanceAfter,
                m.Reason,
                m.Reference,
                m.PerformedBy,
                m.CreatedAt
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, data });
    }

    /// <summary>The ledger for one item.</summary>
    [HttpGet("items/{id:int}/movements")]
    public Task<IActionResult> GetItemMovements(int id) => GetMovements(id, null, 1, 100);

    /// <summary>Every open lot across all items, soonest-expiring first — the Batches tab.</summary>
    [HttpGet("batches")]
    public async Task<IActionResult> GetAllBatches()
    {
        var today = DateTime.Today;
        var batches = await _db.StockBatches
            .Include(b => b.InventoryItem)
            .Where(b => b.QuantityRemaining > 0)
            .ToListAsync();

        var data = batches
            .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
            .ThenBy(b => b.ExpiryDate ?? DateTime.MaxValue)
            .Select(b => new
            {
                b.Id,
                itemId = b.InventoryItemId,
                itemName = b.InventoryItem.Name,
                unit = b.InventoryItem.UnitOfMeasure,
                b.BatchNumber,
                b.ExpiryDate,
                b.QuantityReceived,
                b.QuantityRemaining,
                b.ReceivedAt,
                daysToExpiry = b.ExpiryDate.HasValue
                    ? (int)Math.Floor((b.ExpiryDate.Value.Date - today).TotalDays)
                    : (int?)null,
                status = !b.ExpiryDate.HasValue ? "OK"
                    : b.ExpiryDate.Value.Date < today ? "Expired"
                    : b.ExpiryDate.Value.Date <= today.AddDays(30) ? "Expiring"
                    : "OK"
            });
        return Ok(data);
    }

    /// <summary>The received lots held for an item, with what is left of each.</summary>
    [HttpGet("items/{id:int}/batches")]
    public async Task<IActionResult> GetBatches(int id)
    {
        var batches = await _db.StockBatches
            .Where(b => b.InventoryItemId == id)
            .OrderBy(b => b.ExpiryDate == null)
            .ThenBy(b => b.ExpiryDate)
            .Select(b => new
            {
                b.Id,
                b.BatchNumber,
                b.ExpiryDate,
                b.QuantityReceived,
                b.QuantityRemaining,
                b.ReceivedAt
            })
            .ToListAsync();

        return Ok(batches);
    }

    // ----------------------------------------------------------
    // Alerts — low / out of stock, and expiry
    // ----------------------------------------------------------

    /// <summary>
    /// Everything an administrator should be nudged about: lots already expired
    /// or expiring within <paramref name="expiryDays"/>, and items low on or out
    /// of stock. One call feeds both the alerts panel and the header count.
    /// </summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] int expiryDays = 30)
    {
        expiryDays = Math.Clamp(expiryDays, 1, 365);
        var today = DateTime.Today;
        var horizon = today.AddDays(expiryDays);

        var batches = await _db.StockBatches
            .Include(b => b.InventoryItem)
            .Where(b => b.QuantityRemaining > 0 && b.ExpiryDate != null)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync();

        var expired = batches
            .Where(b => b.ExpiryDate!.Value.Date < today)
            .Select(BatchAlert).ToList();
        var expiringSoon = batches
            .Where(b => b.ExpiryDate!.Value.Date >= today && b.ExpiryDate.Value.Date <= horizon)
            .Select(BatchAlert).ToList();

        var items = await _db.InventoryItems.Where(i => i.IsActive).ToListAsync();
        var outOfStock = items.Where(i => i.CurrentStock <= 0).OrderBy(i => i.Name).Select(Brief).ToList();
        var lowStock = items
            .Where(i => i.CurrentStock > 0 && i.CurrentStock <= i.ReorderLevel)
            .OrderBy(i => i.CurrentStock).Select(Brief).ToList();

        return Ok(new
        {
            expiryDays,
            counts = new
            {
                expired = expired.Count,
                expiringSoon = expiringSoon.Count,
                lowStock = lowStock.Count,
                outOfStock = outOfStock.Count,
                total = expired.Count + expiringSoon.Count + lowStock.Count + outOfStock.Count
            },
            expired,
            expiringSoon,
            lowStock,
            outOfStock
        });
    }

    private static object BatchAlert(StockBatch b) => new
    {
        b.Id,
        itemId = b.InventoryItemId,
        itemName = b.InventoryItem.Name,
        b.BatchNumber,
        b.ExpiryDate,
        b.QuantityRemaining,
        unit = b.InventoryItem.UnitOfMeasure,
        daysToExpiry = b.ExpiryDate.HasValue
            ? (int)Math.Floor((b.ExpiryDate.Value.Date - DateTime.Today).TotalDays)
            : (int?)null
    };

    // ----------------------------------------------------------
    // Test recipes — what each test consumes (drives auto-deduction)
    // ----------------------------------------------------------

    /// <summary>
    /// Every lab test with the supplies it draws down when ordered. Tests with
    /// no recipe are included (empty), so the editor can add one.
    /// </summary>
    [HttpGet("test-recipes")]
    public async Task<IActionResult> GetTestRecipes()
    {
        var tests = await _db.LabTests.Include(t => t.Category).OrderBy(t => t.Name).ToListAsync();
        var lines = await _db.TestInventoryItems.Include(l => l.InventoryItem).ToListAsync();

        var data = tests.Select(t => new
        {
            labTestId = t.Id,
            t.Code,
            testName = t.Name,
            categoryName = t.Category.Name,
            supplies = lines.Where(l => l.LabTestId == t.Id).Select(l => new
            {
                l.InventoryItemId,
                itemName = l.InventoryItem.Name,
                unit = l.InventoryItem.UnitOfMeasure,
                l.Quantity
            })
        });
        return Ok(data);
    }

    /// <summary>Replaces a test's recipe wholesale.</summary>
    [HttpPut("test-recipes/{labTestId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetTestRecipe(int labTestId, [FromBody] TestRecipeRequest request)
    {
        if (!await _db.LabTests.AnyAsync(t => t.Id == labTestId))
            return NotFound();

        var existing = await _db.TestInventoryItems.Where(l => l.LabTestId == labTestId).ToListAsync();
        _db.TestInventoryItems.RemoveRange(existing);

        foreach (var line in request.Supplies ?? new List<TestRecipeLine>())
        {
            if (line.Quantity <= 0) continue;
            _db.TestInventoryItems.Add(new TestInventoryItem
            {
                LabTestId = labTestId,
                InventoryItemId = line.InventoryItemId,
                Quantity = line.Quantity
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Recipe saved" });
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

        var usedByService = await _db.ServiceInventoryItems.AnyAsync(l => l.InventoryItemId == id);
        var usedByTest = await _db.TestInventoryItems.AnyAsync(l => l.InventoryItemId == id);
        if (usedByService || usedByTest)
        {
            item.IsActive = false;
            item.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { message = "The item is used by a service or test, so it was deactivated instead of deleted.", deactivated = true });
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

public record StockInRequest(int Quantity, string? Reason, string? BatchNumber, DateTime? ExpiryDate);

public record StockOutRequest(int Quantity, string? Reason, string? Reference);

public record TestRecipeLine(int InventoryItemId, int Quantity);

public record TestRecipeRequest(List<TestRecipeLine>? Supplies);

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
