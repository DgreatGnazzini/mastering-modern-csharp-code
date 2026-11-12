namespace MasteringModernCSharp.Ch07;

public static class PriceComparison
{
    // Queries every supplier at once and reports each quote the
    // moment it arrives. A failing supplier does not stop the rest.
    public static async IAsyncEnumerable<string> QuotesAsync(
        IEnumerable<(string Supplier, Task<decimal> Quote)> requests)
    {
        var names = requests.ToDictionary(r => r.Quote, r => r.Supplier);

        await foreach (var task in Task.WhenEach(names.Keys))
        {
            var supplier = names[task];
            yield return task.IsCompletedSuccessfully
                ? $"{supplier}: {task.Result:0.00}"
                : $"{supplier}: failed";
        }
    }
}
