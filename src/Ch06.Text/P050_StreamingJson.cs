using System.Text.Json;

namespace MasteringModernCSharp.Ch06;

public record LogEntry(DateTime Time, string Level, string Message);

public static class LogStream
{
    // Reads a huge JSON array one element at a time,
    // instead of loading the whole array into memory.
    public static async Task<int> CountErrorsAsync(
        Stream json, CancellationToken ct = default)
    {
        var errors = 0;
        var entries = JsonSerializer.DeserializeAsyncEnumerable<LogEntry>(
            json, JsonSerializerOptions.Web, ct);

        await foreach (var entry in entries)
        {
            if (entry?.Level == "Error") errors++;
        }
        return errors;
    }

    // Writes a JSON array while the items are still being produced.
    public static Task WriteAsync(
        Stream output, IAsyncEnumerable<LogEntry> entries,
        CancellationToken ct = default) =>
        JsonSerializer.SerializeAsync(
            output, entries, JsonSerializerOptions.Web, ct);
}
