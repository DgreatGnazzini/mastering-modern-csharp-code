using System.Collections.Concurrent;

namespace MasteringModernCSharp.Ch07;

public sealed class PriceCache(
    Func<string, CancellationToken, Task<decimal>> loadPrice)
{
    private readonly ConcurrentDictionary<string, decimal> _cache = new();

    // Most calls are cache hits that finish synchronously, so a
    // ValueTask avoids allocating a Task for each of them.
    public ValueTask<decimal> GetPriceAsync(
        string sku, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(sku, out var price))
        {
            return ValueTask.FromResult(price);
        }
        return new ValueTask<decimal>(LoadAndCacheAsync(sku, ct));
    }

    private async Task<decimal> LoadAndCacheAsync(
        string sku, CancellationToken ct)
    {
        var price = await loadPrice(sku, ct);
        _cache[sku] = price;
        return price;
    }
}
