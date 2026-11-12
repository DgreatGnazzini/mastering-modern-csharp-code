namespace MasteringModernCSharp.Ch08;

public static class Allocations
{
    // Objects this size or larger go on the Large Object Heap.
    public const int LargeObjectThreshold = 85_000;

    // Bytes allocated on this thread while running the action.
    public static long Measure(Action action)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // Allocates a new string per number: garbage on a hot path.
    public static string JoinNaive(int[] numbers)
    {
        var text = "";
        foreach (var n in numbers) text += n + ",";
        return text;
    }

    // One growing buffer, one final string.
    public static string JoinWithBuilder(int[] numbers) =>
        string.Join(',', numbers) + ",";
}

// A long-lived publisher, such as a singleton service.
public sealed class PriceFeed
{
    public event Action<decimal>? PriceChanged;
    public int Subscribers =>
        PriceChanged?.GetInvocationList().Length ?? 0;
    public void Publish(decimal price) => PriceChanged?.Invoke(price);
}

// Subscribes and never unsubscribes: the feed keeps every
// instance alive forever. A classic managed memory leak.
public sealed class LeakyWidget
{
    private readonly byte[] _state = new byte[10_000];
    public LeakyWidget(PriceFeed feed) => feed.PriceChanged += OnPrice;
    private void OnPrice(decimal price) => _state[0] = (byte)price;
}

// The fix: release the subscription when done.
public sealed class Widget : IDisposable
{
    private readonly PriceFeed _feed;
    private readonly byte[] _state = new byte[10_000];

    public Widget(PriceFeed feed)
    {
        _feed = feed;
        _feed.PriceChanged += OnPrice;
    }

    private void OnPrice(decimal price) => _state[0] = (byte)price;
    public void Dispose() => _feed.PriceChanged -= OnPrice;
}
