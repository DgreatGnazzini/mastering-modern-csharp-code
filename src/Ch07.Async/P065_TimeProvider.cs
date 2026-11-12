namespace MasteringModernCSharp.Ch07;

// A login lockout that depends on the clock. Every time read goes
// through TimeProvider, so tests can move time by hand.
public sealed class LoginGuard(
    TimeProvider time, int maxFailures, TimeSpan lockout)
{
    private int _failures;
    private DateTimeOffset? _lockedUntil;

    public bool IsLockedOut =>
        _lockedUntil is { } until && time.GetUtcNow() < until;

    public void RecordFailure()
    {
        if (++_failures >= maxFailures)
        {
            _lockedUntil = time.GetUtcNow() + lockout;
            _failures = 0;
        }
    }

    // Delays and timers take the provider too.
    public Task WaitForUnlockAsync(CancellationToken ct = default)
    {
        if (_lockedUntil is not { } until) return Task.CompletedTask;
        var remaining = until - time.GetUtcNow();
        return remaining <= TimeSpan.Zero
            ? Task.CompletedTask
            : Task.Delay(remaining, time, ct);
    }
}
