using Microsoft.EntityFrameworkCore;

namespace MasteringModernCSharp.Ch10;

public partial class ShopDb
{
    // Set once per request, for example from the signed-in user.
    public int TenantId { get; set; }

    public const string SoftDelete = nameof(SoftDelete);
    public const string Tenant = nameof(Tenant);

    private void ConfigureOrderFilters(ModelBuilder model) =>
        model.Entity<Order>()
            // EF 10: several filters per entity, each with a name,
            // so a query can switch off just one of them.
            .HasQueryFilter(SoftDelete, o => !o.IsDeleted)
            .HasQueryFilter(Tenant, o => o.TenantId == TenantId);
            // TenantId here is read from the context instance
            // each time a query runs.

    // Turn Remove() into a soft delete for every order.
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<Order>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
            }
        }
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }
}

public static class RecycleBin
{
    // Deleted orders for the current tenant only: the tenant
    // filter stays on.
    public static Task<List<Order>> DeletedOrdersAsync(
        ShopDb db, CancellationToken ct = default) =>
        db.Orders
            .IgnoreQueryFilters([ShopDb.SoftDelete])
            .Where(o => o.IsDeleted)
            .ToListAsync(ct);
}
