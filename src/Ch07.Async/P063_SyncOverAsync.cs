namespace MasteringModernCSharp.Ch07;

public static class ReportLoader
{
    // Library code: the continuation does not need the caller's
    // context, so ConfigureAwait(false) lets it run anywhere.
    public static async Task<string> LoadAsync(
        Func<Task<string>> read)
    {
        var raw = await read().ConfigureAwait(false);
        return raw.Trim().ToUpperInvariant();
    }

    // The same code without ConfigureAwait(false). If a caller on
    // a single-threaded context blocks on it, it deadlocks.
    public static async Task<string> LoadNaiveAsync(
        Func<Task<string>> read)
    {
        var raw = await read();
        return raw.Trim().ToUpperInvariant();
    }
}

public static class SyncBridge
{
    // Last resort when an API must be synchronous: run the async
    // work on the thread pool, where no context is captured, and
    // block on that. Costs a thread; never use it in ASP.NET Core
    // request code when an async path exists.
    public static T Run<T>(Func<Task<T>> work) =>
        Task.Run(work).GetAwaiter().GetResult();
}
