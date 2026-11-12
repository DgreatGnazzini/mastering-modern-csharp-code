namespace MasteringModernCSharp.Ch07.Tests;

public class P056_DisposalTests
{
    private static CancellationToken Ct =>
        TestContext.Current.CancellationToken;

    // Records whether it was disposed.
    private sealed class TrackedStream : MemoryStream
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task Owned_stream_is_disposed_with_the_log()
    {
        var stream = new TrackedStream();
        await using (var log = new AuditLog(stream))
        {
            await log.WriteAsync("a", Ct);
        }
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task Borrowed_stream_stays_open_and_is_flushed()
    {
        using var stream = new TrackedStream();
        await using (var log = new AuditLog(stream, leaveOpen: true))
        {
            await log.WriteAsync("hello", Ct);
        }

        Assert.False(stream.Disposed);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        Assert.Equal("hello", (await reader.ReadToEndAsync(Ct)).Trim());
    }

    [Fact]
    public async Task Dispose_is_idempotent_and_use_after_fails()
    {
        var log = new AuditLog(new MemoryStream());
        await log.DisposeAsync();
        await log.DisposeAsync();   // no exception
        log.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => log.WriteAsync("late", Ct));
    }

    [Fact]
    public async Task Await_using_disposes_even_when_an_exception_escapes()
    {
        var stream = new TrackedStream();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var log = new AuditLog(stream);
            await log.WriteAsync("a", Ct);
            throw new InvalidOperationException();
        });
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task File_example_appends_a_line()
    {
        var path = Path.GetTempFileName();
        try
        {
            await AuditExamples.RecordOrderAsync(path, 42);
            Assert.Equal("order 42 placed",
                (await File.ReadAllTextAsync(path, Ct)).Trim());
        }
        finally
        {
            File.Delete(path);
        }
    }

    // The classic bug: returning the task without awaiting it.
    private static Task<string> ReadTooEarly(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return reader.ReadToEndAsync();   // reader disposed on return
    }

    private static async Task<string> ReadCorrectly(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task Awaiting_inside_the_using_keeps_the_reader_alive()
    {
        Assert.Equal("ok",
            await ReadCorrectly(new MemoryStream("ok"u8.ToArray())));
        // A slow stream shows the bug: the read outlives the reader.
        var slow = new SlowStream();
        await Assert.ThrowsAnyAsync<ObjectDisposedException>(
            () => ReadTooEarly(slow));
    }

    private sealed class SlowStream : MemoryStream
    {
        private bool _disposed;
        public SlowStream() : base("data"u8.ToArray()) { }
        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken ct = default)
        {
            await Task.Delay(50, ct);
            ObjectDisposedException.ThrowIf(_disposed, this);
            return await base.ReadAsync(buffer, ct);
        }
        protected override void Dispose(bool disposing)
        {
            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
