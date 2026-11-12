using System.Threading.RateLimiting;

namespace MasteringModernCSharp.Ch07;

public sealed class ThrottledClient(
    Func<string, CancellationToken, Task<string>> send,
    RateLimiter limiter) : IAsyncDisposable
{
    // A token bucket: bursts up to `burst` calls, then one new
    // token per `period`. Extra callers queue up to `queue` deep.
    public static RateLimiter TokenBucket(
        int burst, TimeSpan period, int queue = 100) =>
        new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = burst,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = period,
            QueueLimit = queue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });

    // Waits for a permit, then sends.
    public async Task<string> SendAsync(
        string request, CancellationToken ct = default)
    {
        using var lease = await limiter.AcquireAsync(1, ct);
        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException("Rate limit queue full.");
        }
        return await send(request, ct);
    }

    // Never waits: either sends now or reports why it could not.
    public async Task<string?> TrySendAsync(
        string request, CancellationToken ct = default)
    {
        using var lease = limiter.AttemptAcquire();
        if (!lease.IsAcquired)
        {
            return lease.TryGetMetadata(
                MetadataName.RetryAfter, out var retryAfter)
                ? $"retry after {retryAfter}"
                : null;
        }
        return await send(request, ct);
    }

    public ValueTask DisposeAsync() => limiter.DisposeAsync();
}
