using ProCreateApi.Data;
using ProCreateApi.Models;

namespace ProCreateApi.Services.Inventory;

/// <summary>
/// The one place stock levels move. Every stock-in, stock-out, manual correction
/// and automatic deduction goes through here, so the running balance, the batch
/// remainders and the audit ledger can never drift apart.
///
/// Changes are staged on the context but not saved — the caller commits, so a
/// run of deductions can be persisted in the same transaction as the visit that
/// triggered them.
/// </summary>
public class InventoryLedger
{
    private readonly AppDbContext _db;
    public InventoryLedger(AppDbContext db) { _db = db; }

    /// <summary>Receives stock into an item as a dated lot.</summary>
    public StockMovement StockIn(InventoryItem item, int quantity, string reason,
        string performedBy, string? batchNumber = null, DateTime? expiry = null)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Stock-in quantity must be positive.");

        var batch = new StockBatch
        {
            InventoryItemId = item.Id,
            BatchNumber = (batchNumber ?? string.Empty).Trim(),
            ExpiryDate = expiry,
            QuantityReceived = quantity,
            QuantityRemaining = quantity,
        };
        _db.StockBatches.Add(batch);

        item.CurrentStock += quantity;
        item.UpdatedAt = DateTime.UtcNow;

        var movement = new StockMovement
        {
            InventoryItemId = item.Id,
            MovementType = StockMovementTypes.StockIn,
            QuantityChange = quantity,
            BalanceAfter = item.CurrentStock,
            Reason = (reason ?? string.Empty).Trim(),
            Reference = batch.BatchNumber,
            PerformedBy = performedBy,
            Batch = batch,
        };
        _db.StockMovements.Add(movement);
        return movement;
    }

    /// <summary>
    /// Issues stock out, drawing the soonest-to-expire lots first. With
    /// <paramref name="allowShortfall"/> it takes whatever is on hand (down to
    /// zero) and records that; otherwise it refuses and returns null when there
    /// is not enough. Returns null when nothing actually moves.
    /// </summary>
    public StockMovement? Issue(InventoryItem item, int quantity, string movementType,
        string reason, string reference, string performedBy, bool allowShortfall = false)
    {
        if (quantity <= 0) return null;
        if (!allowShortfall && item.CurrentStock < quantity) return null;

        var actual = allowShortfall ? Math.Min(quantity, item.CurrentStock) : quantity;
        if (actual <= 0) return null;

        item.CurrentStock -= actual;
        item.UpdatedAt = DateTime.UtcNow;
        DrawDownBatches(item.Id, actual);

        var movement = new StockMovement
        {
            InventoryItemId = item.Id,
            MovementType = movementType,
            QuantityChange = -actual,
            BalanceAfter = item.CurrentStock,
            Reason = (reason ?? string.Empty).Trim(),
            Reference = reference ?? string.Empty,
            PerformedBy = performedBy,
        };
        _db.StockMovements.Add(movement);
        return movement;
    }

    /// <summary>A positive manual correction — a recount upwards, not a receipt,
    /// so it carries no batch or expiry.</summary>
    public StockMovement AdjustUp(InventoryItem item, int quantity, string reason, string performedBy)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Adjustment must be positive.");

        item.CurrentStock += quantity;
        item.UpdatedAt = DateTime.UtcNow;

        var movement = new StockMovement
        {
            InventoryItemId = item.Id,
            MovementType = StockMovementTypes.Adjustment,
            QuantityChange = quantity,
            BalanceAfter = item.CurrentStock,
            Reason = (reason ?? string.Empty).Trim(),
            PerformedBy = performedBy,
        };
        _db.StockMovements.Add(movement);
        return movement;
    }

    /// <summary>Spends a quantity across the item's dated lots, first-expiry-first-out.</summary>
    private void DrawDownBatches(int itemId, int quantity)
    {
        var remaining = quantity;
        // Ordered in memory: a boolean/null ordering does not translate cleanly
        // to SQLite, and an item has only a handful of open lots anyway.
        var batches = _db.StockBatches
            .Where(b => b.InventoryItemId == itemId && b.QuantityRemaining > 0)
            .ToList()
            .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)   // dated lots before undated
            .ThenBy(b => b.ExpiryDate ?? DateTime.MaxValue) // soonest expiry first
            .ToList();

        foreach (var batch in batches)
        {
            if (remaining <= 0) break;
            var take = Math.Min(batch.QuantityRemaining, remaining);
            batch.QuantityRemaining -= take;
            remaining -= take;
        }
        // A shortfall here means legacy stock with no lots recorded; CurrentStock
        // stays the source of truth and simply isn't tracked at batch level.
    }
}
