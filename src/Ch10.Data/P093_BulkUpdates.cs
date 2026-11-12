using Microsoft.EntityFrameworkCore;

namespace MasteringModernCSharp.Ch10;

public static class OrderMaintenance
{
    // One UPDATE statement for any number of rows; no entities
    // are loaded or tracked.
    public static Task<int> ArchiveOldAsync(
        ShopDb db, DateTime before, bool stampDate,
        CancellationToken ct = default) =>
        db.Orders
            .Where(o => o.PlacedAt < before && o.Status != "Archived")
            .ExecuteUpdateAsync(set =>
            {
                // EF 10: a normal lambda, so setters can be
                // chosen with ordinary C# logic.
                set.SetProperty(o => o.Status, "Archived");
                if (stampDate)
                {
                    set.SetProperty(o => o.ArchivedAt, DateTime.UtcNow);
                }
            }, ct);

    // One DELETE statement. Query filters still apply, so this
    // only touches the current tenant's soft-deleted orders.
    public static Task<int> PurgeDeletedAsync(
        ShopDb db, CancellationToken ct = default) =>
        db.Orders
            .IgnoreQueryFilters([ShopDb.SoftDelete])
            .Where(o => o.IsDeleted)
            .ExecuteDeleteAsync(ct);
}
