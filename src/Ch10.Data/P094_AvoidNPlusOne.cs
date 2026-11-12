using Microsoft.EntityFrameworkCore;

namespace MasteringModernCSharp.Ch10;

public record OrderSummary(int Id, string Customer, int Lines,
                           decimal Total);

public static class OrderReads
{
    // N+1: one query for the orders, then one more per order
    // when each order's lines are loaded. Shown for contrast.
    public static async Task<List<OrderSummary>> SlowAsync(
        ShopDb db, CancellationToken ct = default)
    {
        var result = new List<OrderSummary>();
        foreach (var order in await db.Orders.ToListAsync(ct))
        {
            await db.Entry(order).Collection(o => o.Lines).LoadAsync(ct);
            await db.Entry(order).Reference(o => o.Customer).LoadAsync(ct);
            result.Add(new(order.Id, order.Customer!.Name,
                order.Lines.Count,
                order.Lines.Sum(l => l.Quantity * l.UnitPrice)));
        }
        return result;
    }

    // Fix 1: project exactly what the screen needs. One query,
    // with the counting and summing done by the database.
    public static Task<List<OrderSummary>> ProjectedAsync(
        ShopDb db, CancellationToken ct = default) =>
        db.Orders
            .Select(o => new OrderSummary(
                o.Id,
                o.Customer!.Name,
                o.Lines.Count,
                o.Lines.Sum(l => l.Quantity * l.UnitPrice)))
            .ToListAsync(ct);

    // Fix 2: when you need whole entities, include them. Split
    // queries avoid repeating each order's columns per line.
    public static Task<List<Order>> WithLinesAsync(
        ShopDb db, CancellationToken ct = default) =>
        db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Lines)
            .AsSplitQuery()
            .ToListAsync(ct);

    // Fix 3: a hot query compiled once, reused on every call.
    private static readonly Func<ShopDb, int, Task<Order?>> ById =
        EF.CompileAsyncQuery((ShopDb db, int id) =>
            db.Orders.Include(o => o.Lines)
                .FirstOrDefault(o => o.Id == id));

    public static Task<Order?> FindAsync(ShopDb db, int id) =>
        ById(db, id);
}
