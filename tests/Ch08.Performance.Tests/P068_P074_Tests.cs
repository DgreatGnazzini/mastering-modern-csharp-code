using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;

namespace MasteringModernCSharp.Ch08.Tests;

public class P068_PipelinesTests
{
    private static CancellationToken Ct =>
        TestContext.Current.CancellationToken;

    // Returns at most `chunk` bytes per read, like a slow socket.
    private sealed class TrickleStream(byte[] data, int chunk)
        : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken ct = default) =>
            base.ReadAsync(buffer[..Math.Min(chunk, buffer.Length)], ct);
    }

    #region p068
    [Theory]
    [InlineData(1)]      // one byte at a time
    [InlineData(5)]      // lines split mid-message
    [InlineData(4096)]   // everything in one read
    public async Task Messages_survive_any_split(int chunk)
    {
        var data = Encoding.UTF8.GetBytes("HELLO\nPRICE A1 9.99\nBYE");
        var stream = new TrickleStream(data, chunk);

        var messages = await LineProtocol.ReadMessagesAsync(stream, Ct);

        Assert.Equal(["HELLO", "PRICE A1 9.99", "BYE"], messages);
    }
    #endregion

    [Fact]
    public async Task Lines_spanning_segments_are_joined()
    {
        // A tiny segment size forces multi-segment buffers.
        var pipe = new Pipe(new PipeOptions(minimumSegmentSize: 16));
        var text = new string('x', 100) + "\n" + "end\n";
        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(text), Ct);
        await pipe.Writer.CompleteAsync();

        var messages = await LineProtocol.ReadMessagesAsync(
            pipe.Reader.AsStream(), Ct);

        Assert.Equal([new string('x', 100), "end"], messages);
    }

    [Fact]
    public async Task Empty_stream_gives_no_messages() =>
        Assert.Empty(await LineProtocol.ReadMessagesAsync(
            new MemoryStream(), Ct));
}

public class P074_GarbageCollectionTests
{
    [Fact]
    public void Large_arrays_go_straight_to_generation_2()
    {
        var small = new byte[Allocations.LargeObjectThreshold - 100];
        var large = new byte[Allocations.LargeObjectThreshold];
        Assert.Equal(0, GC.GetGeneration(small));
        Assert.Equal(2, GC.GetGeneration(large));   // the LOH
    }

    [Fact]
    public void Builder_allocates_far_less_than_concatenation()
    {
        var numbers = Enumerable.Range(0, 1_000).ToArray();
        Allocations.JoinNaive(numbers);     // warm up the JIT
        Allocations.JoinWithBuilder(numbers);

        var naive = Allocations.Measure(
            () => Allocations.JoinNaive(numbers));
        var better = Allocations.Measure(
            () => Allocations.JoinWithBuilder(numbers));

        Assert.True(naive > better * 50, $"{naive} vs {better}");
    }

    // NoInlining: the widget must not stay in a local of the test.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Create(PriceFeed feed, bool dispose)
    {
        if (dispose)
        {
            var widget = new Widget(feed);
            widget.Dispose();
            return new WeakReference(widget);
        }
        return new WeakReference(new LeakyWidget(feed));
    }

    private static void FullCollect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    #region p074
    [Fact]
    public void Event_subscription_keeps_a_widget_alive()
    {
        var feed = new PriceFeed();   // long-lived publisher
        var leaky = Create(feed, dispose: false);
        var fixedOne = Create(feed, dispose: true);

        FullCollect();

        Assert.True(leaky.IsAlive);       // held by feed's delegate
        Assert.False(fixedOne.IsAlive);   // unsubscribed, collected
        Assert.Equal(1, feed.Subscribers);
    }
    #endregion
}
