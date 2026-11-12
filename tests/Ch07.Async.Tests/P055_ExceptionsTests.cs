namespace MasteringModernCSharp.Ch07.Tests;

public class P055_ExceptionsTests
{
    private static CancellationToken Ct =>
        TestContext.Current.CancellationToken;

    private static Task Fail(string file, CancellationToken ct) =>
        Task.FromException(new InvalidOperationException(file));

    [Fact]
    public async Task Plain_await_on_WhenAll_hides_later_failures()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Task.WhenAll(Fail("a", Ct), Fail("b", Ct)));
        Assert.Equal("a", ex.Message);   // "b" is lost
    }

    [Fact]
    public async Task ImportAll_reports_every_failure()
    {
        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => Importer.ImportAllAsync(["a", "b", "c"], Fail, Ct));
        Assert.Equal(["a", "b", "c"],
            ex.InnerExceptions.Select(e => e.Message));
    }

    [Fact]
    public async Task ImportAll_single_failure_is_not_wrapped() =>
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Importer.ImportAllAsync(["a"], Fail, Ct));

    [Fact]
    public async Task Missing_files_are_skipped_and_logged()
    {
        var log = new List<string>();
        var count = await Importer.RunAsync(["a", "gone", "b"],
            (f, _) => f == "gone"
                ? throw new FileNotFoundException(f)
                : Task.CompletedTask,
            log.Add, Ct);

        Assert.Equal(2, count);
        Assert.Equal(["Skipped missing file gone."], log);
    }

    [Fact]
    public async Task IO_errors_are_wrapped_with_context()
    {
        var disk = new IOException("disk full");
        var ex = await Assert.ThrowsAsync<ImportFailedException>(
            () => Importer.RunAsync(["a"],
                (_, _) => throw disk, _ => { }, Ct));

        Assert.Equal("a", ex.File);
        Assert.Same(disk, ex.InnerException);
    }

    [Fact]
    public async Task Cancellation_stops_quietly()
    {
        using var cts = new CancellationTokenSource();
        var log = new List<string>();
        var count = await Importer.RunAsync(["a", "b", "c"],
            (f, ct) =>
            {
                if (f == "b") cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            log.Add, cts.Token);

        Assert.Equal(1, count);
        Assert.Equal(["Stopped: cancellation was requested."], log);
    }

    [Fact]
    public async Task Unrelated_cancellation_still_propagates()
    {
        // A timeout inside import, not our token: not swallowed.
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => Importer.RunAsync(["a"],
                (_, _) => throw new TaskCanceledException(),
                _ => { }, Ct));
    }

    private static void Thrower() =>
        throw new InvalidOperationException("boom");

    [Fact]
    public void Throw_keeps_the_stack_trace_but_throw_ex_does_not()
    {
        var kept = Assert.Throws<InvalidOperationException>(
            () => Rethrow.Preserve(Thrower));
        var reset = Assert.Throws<InvalidOperationException>(
            () => Rethrow.Reset(Thrower));

        Assert.Contains(nameof(Thrower), kept.StackTrace);
        Assert.DoesNotContain(nameof(Thrower), reset.StackTrace);
    }
}
