using System.Threading.RateLimiting;
using MasteringModernCSharp.Ch07;
using Microsoft.Extensions.Time.Testing;

namespace MasteringModernCSharp.Ch07.Tests;

public class P054_CancellationTests
{
    [Fact]
    public async Task Slow_operation_times_out() =>
        await Assert.ThrowsAsync<TimeoutException>(() =>
            Timeouts.WithTimeoutAsync(async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return 0;
            }, TimeSpan.FromMilliseconds(50),
            TestContext.Current.CancellationToken));

    [Fact]
    public async Task Caller_cancel_is_not_a_timeout()
    {
        using var cts = new CancellationTokenSource();
        var task = Timeouts.WithTimeoutAsync(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }, TimeSpan.FromMinutes(5), cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public void Loop_stops_when_cancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            Timeouts.SumSlowly([1, 2, 3], cts.Token));
    }
}

public class P057_ParallelForEachAsyncTests
{
    [Fact]
    public async Task Never_exceeds_the_limit()
    {
        int inFlight = 0, peak = 0;
        var urls = Enumerable.Range(0, 40).Select(i => $"u{i}");

        var results = await BulkFetcher.FetchAllAsync(urls, async (u, ct) =>
        {
            var now = Interlocked.Increment(ref inFlight);
            InterlockedMax(ref peak, now);
            await Task.Delay(5, ct);
            Interlocked.Decrement(ref inFlight);
            return u.Length;
        }, maxConcurrency: 4, TestContext.Current.CancellationToken);

        Assert.Equal(40, results.Count);
        Assert.InRange(peak, 1, 4);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while ((seen = Volatile.Read(ref target)) < value &&
               Interlocked.CompareExchange(ref target, value, seen) != seen) { }
    }
}

public class P058_ChannelsTests
{
    [Fact]
    public async Task Every_item_is_processed_once()
    {
        var results = await ImagePipeline.RunAsync(
            Files(), async (f, ct) => { await Task.Yield(); return f + ".png"; },
            workers: 3, capacity: 2, TestContext.Current.CancellationToken);

        Assert.Equal(50, results.Count);
        Assert.Equal(50, results.Distinct().Count());

        static async IAsyncEnumerable<string> Files()
        {
            for (var i = 0; i < 50; i++)
            {
                await Task.Yield();
                yield return $"img{i}";
            }
        }
    }
}

public class P059_AsyncStreamsTests
{
    [Fact]
    public async Task Fetches_only_the_pages_it_needs()
    {
        var fetched = 0;
        var token = TestContext.Current.CancellationToken;
        var items = await PagedApi.GetAllAsync<int>((page, ct) =>
        {
            fetched++;
            var data = Enumerable.Range((page - 1) * 10, 10).ToList();
            return Task.FromResult(new Page<int>(data, page < 5 ? page + 1 : null));
        }, token).Take(15).ToListAsync(token);

        Assert.Equal(15, items.Count);
        Assert.Equal(2, fetched);
    }
}

public class P060_WhenEachTests
{
    [Fact]
    public async Task Yields_in_completion_order()
    {
        var slow = new TaskCompletionSource<decimal>();
        var fast = new TaskCompletionSource<decimal>();
        var broken = new TaskCompletionSource<decimal>();

        await using var quotes = PriceComparison.QuotesAsync(
            [("Slow", slow.Task), ("Fast", fast.Task), ("Broken", broken.Task)])
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        async Task<string> Next(Action complete)
        {
            var moving = quotes.MoveNextAsync();
            complete();
            Assert.True(await moving);
            return quotes.Current;
        }

        Assert.Equal("Fast: 9.50", await Next(() => fast.SetResult(9.5m)));
        Assert.Equal("Broken: failed",
            await Next(() => broken.SetException(new HttpRequestException())));
        Assert.Equal("Slow: 12.00", await Next(() => slow.SetResult(12m)));
    }
}

public class P061_LockTests
{
    [Fact]
    public void Concurrent_withdrawals_never_overdraw()
    {
        var account = new Account(1_000m);
        var ok = 0;
        Parallel.For(0, 5_000, _ =>
        {
            if (account.TryWithdraw(1m)) Interlocked.Increment(ref ok);
        });
        Assert.Equal(1_000, ok);
        Assert.Equal(0m, account.Balance);
    }
}

public class P062_RateLimitingTests
{
    [Fact]
    public async Task Burst_then_reject()
    {
        var limiter = new TokenBucketRateLimiter(new()
        {
            TokenLimit = 2, TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromMilliseconds(20),
            AutoReplenishment = false, QueueLimit = 0,
        });
        await using var client = new ThrottledClient(
            (r, ct) => Task.FromResult("ok:" + r), limiter);
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal("ok:a", await client.TrySendAsync("a", ct));
        Assert.Equal("ok:b", await client.TrySendAsync("b", ct));
        Assert.StartsWith("retry after", await client.TrySendAsync("c", ct));

        // Manual replenishment adds nothing until a period passes.
        limiter.TryReplenish();
        Assert.StartsWith("retry after", await client.TrySendAsync("c", ct));
        await Task.Delay(40, ct);
        limiter.TryReplenish();
        Assert.Equal("ok:d", await client.TrySendAsync("d", ct));
    }
}

public class P063_SyncOverAsyncTests
{
    #region p063
    // Stands in for a stuck UI thread: it accepts callbacks
    // but never runs them.
    private sealed class BlockedContext : SynchronizationContext
    {
        public override void Post(
            SendOrPostCallback d, object? state) { }
    }

    // Blocks on the task, as `.Result` would, under that context.
    private static bool CompletesUnder(Func<Task<string>> start)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(
            new BlockedContext());
        try
        {
            return start().Wait(TimeSpan.FromMilliseconds(500));
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public void Naive_version_deadlocks() =>
        Assert.False(CompletesUnder(
            () => ReportLoader.LoadNaiveAsync(SlowRead)));

    [Fact]
    public void ConfigureAwait_false_version_completes() =>
        Assert.True(CompletesUnder(
            () => ReportLoader.LoadAsync(SlowRead)));

    [Fact]
    public void Sync_bridge_completes() =>
        Assert.True(CompletesUnder(() => Task.FromResult(
            SyncBridge.Run(() => ReportLoader.LoadNaiveAsync(SlowRead)))));
    #endregion

    private static async Task<string> SlowRead()
    {
        await Task.Delay(10).ConfigureAwait(false);
        return " data ";
    }
}

public class P064_ValueTaskTests
{
    [Fact]
    public async Task Cache_hits_complete_synchronously()
    {
        var loads = 0;
        var cache = new PriceCache(async (sku, ct) =>
        {
            loads++;
            await Task.Yield();
            return 4.2m;
        });
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(4.2m, await cache.GetPriceAsync("A", ct));
        var hit = cache.GetPriceAsync("A", ct);
        Assert.True(hit.IsCompletedSuccessfully);
        Assert.Equal(4.2m, await hit);
        Assert.Equal(1, loads);
    }
}

public class P065_TimeProviderTests
{
    #region p065
    [Fact]
    public void Lockout_expires_when_time_moves_on()
    {
        var clock = new FakeTimeProvider();
        var guard = new LoginGuard(clock, maxFailures: 3,
            lockout: TimeSpan.FromMinutes(15));

        guard.RecordFailure();
        guard.RecordFailure();
        Assert.False(guard.IsLockedOut);
        guard.RecordFailure();
        Assert.True(guard.IsLockedOut);

        clock.Advance(TimeSpan.FromMinutes(14));
        Assert.True(guard.IsLockedOut);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.False(guard.IsLockedOut);
    }
    #endregion

    [Fact]
    public async Task Delay_finishes_only_when_fake_time_advances()
    {
        var clock = new FakeTimeProvider();
        var guard = new LoginGuard(clock, 1, TimeSpan.FromMinutes(5));
        guard.RecordFailure();

        var wait = guard.WaitForUnlockAsync(TestContext.Current.CancellationToken);
        Assert.False(wait.IsCompleted);
        clock.Advance(TimeSpan.FromMinutes(5));
        await wait;
    }
}
