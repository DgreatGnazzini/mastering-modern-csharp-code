using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MasteringModernCSharp.Ch10;

public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int Stock { get; set; }

    // Checked on every UPDATE and DELETE: if someone else changed
    // the row since we read it, the version won't match.
    // (On SQL Server, use a [Timestamp] byte[] rowversion instead.)
    [ConcurrencyCheck]
    public Guid Version { get; set; } = Guid.NewGuid();
}

public partial class ShopDb
{
    public DbSet<Product> Products => Set<Product>();

    private void ConfigureProducts(ModelBuilder model) { }
}

public static class Inventory
{
    // Reserves stock, retrying when another request got there
    // first. Returns false when there isn't enough left.
    public static async Task<bool> ReserveAsync(
        ShopDb db, int productId, int quantity,
        CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var product = await db.Products.SingleAsync(
                p => p.Id == productId, ct);
            if (product.Stock < quantity) return false;

            product.Stock -= quantity;
            product.Version = Guid.NewGuid();
            try
            {
                await db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 3)
            {
                // Someone else won. Forget our stale copy and
                // decide again with fresh data.
                db.ChangeTracker.Clear();
            }
        }
    }
}
