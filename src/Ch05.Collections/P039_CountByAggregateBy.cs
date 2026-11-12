namespace MasteringModernCSharp.Ch05;

public record Order(string Customer, string Status, decimal Total);

public static class OrderStats
{
    // How many orders are in each status, in one pass.
    public static Dictionary<string, int> CountByStatus(
        IEnumerable<Order> orders) =>
        orders.CountBy(o => o.Status)
              .ToDictionary();

    // Total spend per customer, in one pass, with no groups built.
    public static Dictionary<string, decimal> SpendByCustomer(
        IEnumerable<Order> orders) =>
        orders.AggregateBy(
                  keySelector: o => o.Customer,
                  seed: 0m,
                  func: (total, o) => total + o.Total)
              .ToDictionary();
}
