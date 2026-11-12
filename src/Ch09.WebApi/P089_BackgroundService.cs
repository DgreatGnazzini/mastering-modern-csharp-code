using System.Collections.Concurrent;

namespace MasteringModernCSharp.Ch09;

public sealed class SessionStore(TimeProvider time)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset>
        _expires = new();

    public void Add(string id, TimeSpan lifetime) =>
        _expires[id] = time.GetUtcNow() + lifetime;

    public int Count => _expires.Count;

    public int RemoveExpired()
    {
        var now = time.GetUtcNow();
        var removed = 0;
        foreach (var (id, expires) in _expires)
        {
            if (expires <= now && _expires.TryRemove(id, out _)) removed++;
        }
        return removed;
    }
}

// Runs for the life of the app, cleaning up once a minute.
public sealed class SessionCleanup(
    SessionStore sessions, TimeProvider time,
    ILogger<SessionCleanup> log) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        // PeriodicTimer never overlaps ticks, and taking the
        // TimeProvider makes it testable.
        using var timer = new PeriodicTimer(Interval, time);
        while (await timer.WaitForNextTickAsync(stop))
        {
            try
            {
                var removed = sessions.RemoveExpired();
                log.LogInformation("Removed {Count} sessions", removed);
            }
            catch (Exception ex)
            {
                // One bad run must not stop every later run.
                log.LogError(ex, "Session cleanup failed");
            }
        }
    }
}
