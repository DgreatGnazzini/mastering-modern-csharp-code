namespace MasteringModernCSharp.Ch04;

public record Sale(string Region, string Product, int Units,
                   decimal UnitPrice, DateOnly Date);

public record RegionSummary(string Region, decimal Revenue,
                            string TopProduct);

public static class SalesReport
{
    public static List<RegionSummary> Build(
        IEnumerable<Sale> sales, int year)
    {
        return sales
            .Where(InYear)
            .GroupBy(s => s.Region)
            .Select(Summarize)
            .OrderByDescending(r => r.Revenue)
            .ToList();

        // Local functions give each step a name and keep the
        // pipeline readable from top to bottom.
        bool InYear(Sale s) => s.Date.Year == year;

        static decimal Revenue(Sale s) => s.Units * s.UnitPrice;

        static RegionSummary Summarize(IGrouping<string, Sale> region) =>
            new(region.Key,
                region.Sum(Revenue),
                region.GroupBy(s => s.Product)
                      .MaxBy(p => p.Sum(Revenue))!.Key);
    }
}
