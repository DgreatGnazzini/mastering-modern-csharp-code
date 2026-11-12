using System.Text;

namespace MasteringModernCSharp.Ch07;

// Writes audit lines to a stream. It disposes the stream only if
// it owns it: whoever creates a resource is responsible for it.
public sealed class AuditLog : IAsyncDisposable, IDisposable
{
    private readonly Stream _stream;
    private readonly bool _ownsStream;
    private readonly StreamWriter _writer;
    private bool _disposed;

    public AuditLog(Stream stream, bool leaveOpen = false)
    {
        _stream = stream;
        _ownsStream = !leaveOpen;
        // The writer never closes the stream; we decide that below.
        _writer = new StreamWriter(stream, Encoding.UTF8,
                                   leaveOpen: true);
    }

    // Opens its own file, so the AuditLog owns it.
    public static AuditLog Open(string path) =>
        new(new FileStream(path, FileMode.Append, FileAccess.Write,
                           FileShare.Read, 4096, useAsync: true));

    public Task WriteAsync(string line, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _writer.WriteLineAsync(line.AsMemory(), ct);
    }

    // Preferred: flushes without blocking a thread.
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;     // safe to call twice
        _disposed = true;
        await _writer.DisposeAsync();
        if (_ownsStream) await _stream.DisposeAsync();
    }

    // For callers that can't await, such as a plain "using".
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writer.Dispose();
        if (_ownsStream) _stream.Dispose();
    }
}

public static class AuditExamples
{
    public static async Task RecordOrderAsync(string path, int orderId)
    {
        // Disposed at the end of the method, even on an exception.
        await using var log = AuditLog.Open(path);
        await log.WriteAsync($"order {orderId} placed");
    }
}
