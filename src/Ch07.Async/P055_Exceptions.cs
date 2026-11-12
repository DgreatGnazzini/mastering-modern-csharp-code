namespace MasteringModernCSharp.Ch07;

// A domain exception: specific type, useful properties, and a
// message that makes sense in a log on its own.
public sealed class ImportFailedException(
    string file, string reason, Exception? inner = null)
    : Exception($"Import of '{file}' failed: {reason}", inner)
{
    public string File { get; } = file;
    public string Reason { get; } = reason;
}

public static class Importer
{
    // await rethrows only the FIRST failure of Task.WhenAll.
    // This version reports every one.
    public static async Task ImportAllAsync(
        IEnumerable<string> files,
        Func<string, CancellationToken, Task> import,
        CancellationToken ct)
    {
        var all = Task.WhenAll(files.Select(f => import(f, ct)));
        try
        {
            await all;
        }
        catch when (all.Exception is { InnerExceptions.Count: > 1 })
        {
            throw all.Exception;   // AggregateException with them all
        }
    }

    // Catch only what you can handle; let the rest propagate.
    public static async Task<int> RunAsync(
        IEnumerable<string> files,
        Func<string, CancellationToken, Task> import,
        Action<string> log,
        CancellationToken ct)
    {
        var imported = 0;
        foreach (var file in files)
        {
            try
            {
                await import(file, ct);
                imported++;
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                log("Stopped: cancellation was requested.");
                break;             // expected, not an error
            }
            catch (IOException ex)
                when (ex is not FileNotFoundException)
            {
                // Translate, keeping the original as InnerException.
                throw new ImportFailedException(file, "I/O error", ex);
            }
            catch (FileNotFoundException)
            {
                log($"Skipped missing file {file}.");
            }
        }
        return imported;
    }
}

public static class Rethrow
{
    // "throw;" keeps the original stack trace.
    public static void Preserve(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { throw; }
    }

    // "throw ex;" resets it to this line. Don't do this.
    public static void Reset(Action action)
    {
        try { action(); }
#pragma warning disable CA2200 // the point of the example
        catch (InvalidOperationException ex) { throw ex; }
#pragma warning restore CA2200
    }
}
