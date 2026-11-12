using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace MasteringModernCSharp.Ch08;

// Reads newline-delimited messages from a socket or file stream.
// Messages can arrive split across reads, or several in one read.
public static class LineProtocol
{
    public static async Task<List<string>> ReadMessagesAsync(
        Stream stream, CancellationToken ct = default)
    {
        var messages = new List<string>();
        var reader = PipeReader.Create(stream);
        while (true)
        {
            var result = await reader.ReadAsync(ct);
            var buffer = result.Buffer;

            while (TryReadLine(ref buffer, out var line))
            {
                messages.Add(Encoding.UTF8.GetString(line));
            }

            // Consumed: up to the last full line. Examined: all of
            // it, so the next ReadAsync waits for more data.
            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                if (!buffer.IsEmpty)   // last line had no newline
                    messages.Add(Encoding.UTF8.GetString(buffer));
                break;
            }
        }
        await reader.CompleteAsync();
        return messages;
    }

    private static bool TryReadLine(
        ref ReadOnlySequence<byte> buffer,
        out ReadOnlySequence<byte> line)
    {
        // Works even when the line spans several memory segments.
        var position = buffer.PositionOf((byte)'\n');
        if (position is null)
        {
            line = default;
            return false;
        }
        line = buffer.Slice(0, position.Value);
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }
}
