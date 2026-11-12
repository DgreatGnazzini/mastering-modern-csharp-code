using System.Data;
using Microsoft.EntityFrameworkCore;

namespace MasteringModernCSharp.Ch10;

public record CartLine(int ProductId, int Quantity, decimal UnitPrice);

public static class Checkout
{
    // Reserve stock for every line and create the order: all of it
    // happens, or none of it does.
    public static async Task<int?> PlaceOrderAsync(
        ShopDb db, int customerId, IReadOnlyList<CartLine> lines,
        CancellationToken ct = default)
    {
        // With retries on (SQL Server's EnableRetryOnFailure), the
        // whole transaction must run inside the strategy, so a retry
        // repeats it from the start.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, ct);

            var order = new Order
            {
                TenantId = db.TenantId,
                CustomerId = customerId,
                PlacedAt = DateTime.UtcNow,
            };
            foreach (var line in lines)
            {
                // Atomic check-and-decrement in the database.
                var reserved = await db.Products
                    .Where(p => p.Id == line.ProductId
                                && p.Stock >= line.Quantity)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.Stock,
                                     p => p.Stock - line.Quantity)
                        .SetProperty(p => p.Version, Guid.NewGuid()), ct);
                if (reserved == 0)
                {
                    await tx.RollbackAsync(ct);  // undo earlier lines
                    return (int?)null;
                }
                order.Lines.Add(new OrderLine
                {
                    Sku = $"P{line.ProductId}",
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                });
            }

            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return order.Id;
        });
    }
}
