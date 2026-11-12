namespace MasteringModernCSharp.Ch12.Tests;

#region p111
public class PasswordHasherTests
{
    // Strongly typed rows: a wrong type is a compile error.
    public static TheoryData<string, string, bool> Cases => new()
    {
        { "correct horse", "correct horse", true },
        { "correct horse", "Correct horse", false },
        { "", "", true },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Verifies_only_the_same_password(
        string stored, string attempt, bool expected)
    {
        var hash = PasswordHasher.Hash(stored);
        Assert.Equal(expected, PasswordHasher.Verify(attempt, hash));
    }

    [Fact]
    public void Skips_itself_at_run_time()
    {
        // Decide while running; the test reports as Skipped.
        Assert.SkipUnless(OperatingSystem.IsWindows(),
                          "Windows-only check.");
        Assert.True(OperatingSystem.IsWindows());
    }

    [Fact]
    public async Task Gets_the_run_context()
    {
        // Cancelled when the run is aborted or times out.
        var ct = TestContext.Current.CancellationToken;
        await Task.Delay(1, ct);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Running {TestContext.Current.Test?.TestDisplayName}");
    }

    // Runs only when asked for, e.g. with --explicit on.
    [Fact(Explicit = true)]
    public void Slow_full_iteration_benchmark()
    {
        for (var i = 0; i < 20; i++)
        {
            PasswordHasher.Hash("benchmark");
        }
    }
}
#endregion
