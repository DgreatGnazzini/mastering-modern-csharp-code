using System.Collections.Concurrent;

namespace MasteringModernCSharp.Ch07;

public static class BulkFetcher
{
    // Fetches every URL with at most `maxConcurrency` calls in
    // flight, and returns the results keyed by URL.
    public static async Task<IReadOnlyDictionary<string, int>>
        FetchAllAsync(
            IEnumerable<string> urls,
            Func<string, CancellationToken, ValueTask<int>> fetch,
            int maxConcurrency,
            CancellationToken cancellationToken = default)
    {
        var results = new ConcurrentDictionary<string, int>();
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency,
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(urls, options, async (url, ct) =>
        {
            results[url] = await fetch(url, ct);
        });

        return results;
    }
}
