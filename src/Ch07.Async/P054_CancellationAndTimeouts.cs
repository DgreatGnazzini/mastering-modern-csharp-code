namespace MasteringModernCSharp.Ch07;

public static class Timeouts
{
    // Runs an operation with a time limit and still honors the
    // caller's own token. A timeout surfaces as TimeoutException;
    // a caller cancel stays an OperationCanceledException.
    public static async Task<T> WithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            return await operation(cts.Token);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Operation timed out after {timeout}.");
        }
    }

    // Long-running loops check the token between steps.
    public static int SumSlowly(
        IEnumerable<int> values, CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += value;
        }
        return total;
    }
}
