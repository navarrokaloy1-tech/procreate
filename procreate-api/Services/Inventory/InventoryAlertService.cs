using ProCreateApi.Data;
using ProCreateApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ProCreateApi.Services.Inventory;

public record ExpiryAlertDto(
    int Id, int ItemId, string ItemName, string BatchNumber,
    DateTime? ExpiryDate, int QuantityRemaining, string Unit, int? DaysToExpiry);

public record StockAlertDto(
    int Id, string Name, string BrandName, int CurrentStock,
    int ReorderLevel, string UnitOfMeasure, string StockState);

public record AlertCounts(int Expired, int ExpiringSoon, int LowStock, int OutOfStock, int Total);

public record InventoryAlertReport(
    int ExpiryDays,
    AlertCounts Counts,
    List<ExpiryAlertDto> Expired,
    List<ExpiryAlertDto> ExpiringSoon,
    List<StockAlertDto> LowStock,
    List<StockAlertDto> OutOfStock);

/// <summary>
/// Works out what an administrator should be nudged about — lots expired or
/// expiring soon, and items low on or out of stock. One place so the alerts
/// endpoint, the header bell and the email digest can never disagree.
/// </summary>
public class InventoryAlertService
{
    private readonly AppDbContext _db;
    public InventoryAlertService(AppDbContext db) { _db = db; }

    public async Task<InventoryAlertReport> BuildAsync(int expiryDays)
    {
        expiryDays = Math.Clamp(expiryDays, 1, 365);
        var today = DateTime.Today;
        var horizon = today.AddDays(expiryDays);

        var batches = await _db.StockBatches
            .Include(b => b.InventoryItem)
            .Where(b => b.QuantityRemaining > 0 && b.ExpiryDate != null)
            .OrderBy(b => b.ExpiryDate)
            .ToListAsync();

        ExpiryAlertDto MapBatch(StockBatch b) => new(
            b.Id, b.InventoryItemId, b.InventoryItem.Name, b.BatchNumber,
            b.ExpiryDate, b.QuantityRemaining, b.InventoryItem.UnitOfMeasure,
            b.ExpiryDate.HasValue ? (int)Math.Floor((b.ExpiryDate.Value.Date - today).TotalDays) : null);

        var expired = batches.Where(b => b.ExpiryDate!.Value.Date < today).Select(MapBatch).ToList();
        var expiringSoon = batches
            .Where(b => b.ExpiryDate!.Value.Date >= today && b.ExpiryDate.Value.Date <= horizon)
            .Select(MapBatch).ToList();

        var items = await _db.InventoryItems.Where(i => i.IsActive).ToListAsync();
        static string State(InventoryItem i) =>
            i.CurrentStock <= 0 ? "Out of Stock"
            : i.CurrentStock <= i.ReorderLevel ? "Low Stock"
            : "In Stock";
        StockAlertDto MapItem(InventoryItem i) =>
            new(i.Id, i.Name, i.BrandName, i.CurrentStock, i.ReorderLevel, i.UnitOfMeasure, State(i));

        var outOfStock = items.Where(i => i.CurrentStock <= 0).OrderBy(i => i.Name).Select(MapItem).ToList();
        var lowStock = items
            .Where(i => i.CurrentStock > 0 && i.CurrentStock <= i.ReorderLevel)
            .OrderBy(i => i.CurrentStock).Select(MapItem).ToList();

        var counts = new AlertCounts(
            expired.Count, expiringSoon.Count, lowStock.Count, outOfStock.Count,
            expired.Count + expiringSoon.Count + lowStock.Count + outOfStock.Count);

        return new InventoryAlertReport(expiryDays, counts, expired, expiringSoon, lowStock, outOfStock);
    }
}
