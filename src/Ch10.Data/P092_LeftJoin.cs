using Microsoft.EntityFrameworkCore;

namespace MasteringModernCSharp.Ch10;

public record CustomerOrderRow(string Customer, int? OrderId);

public static class SalesReport
{
    // Every customer, with each of their orders or a null order.
    // EF 10 translates LeftJoin straight to SQL LEFT JOIN.
    public static IQueryable<CustomerOrderRow> CustomersWithOrders(
        ShopDb db) =>
        db.Customers
            .LeftJoin(
                db.Orders,
                c => c.Id,
                o => o.CustomerId,
                (c, o) => new { c.Name, OrderId = (int?)o!.Id })
            .OrderBy(r => r.Name)
            .Select(r => new CustomerOrderRow(r.Name, r.OrderId));
}
