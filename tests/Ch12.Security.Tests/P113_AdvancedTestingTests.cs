using CsCheck;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace MasteringModernCSharp.Ch12.Tests;

#region p113-doubles
// A stub: returns canned data.
public sealed class StubInvoices(params Invoice[] invoices) : IInvoiceStore
{
    public Task<IReadOnlyList<Invoice>> UnpaidAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Invoice>>(invoices);
}

// A fake: a working, in-memory version that records what it did.
public sealed class FakeEmail : IEmailSender
{
    public List<(string To, string Subject)> Sent { get; } = [];

    public Task SendAsync(string to, string subject, string body,
                          CancellationToken ct)
    {
        Sent.Add((to, subject));
        return Task.CompletedTask;
    }
}

public class ReminderServiceTests
{
    private static CancellationToken Ct =>
        TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset Due =
        new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Due);
    private readonly FakeEmail _email = new();
    private readonly FakeLogger<ReminderService> _log = new();

    private ReminderService Service(params Invoice[] invoices) =>
        new(new StubInvoices(invoices), _email, _clock, _log);

    [Fact]
    public async Task Reminds_only_after_the_grace_period()
    {
        var service = Service(
            new Invoice("A-1", "ada@example.com", 120m, Due, Paid: false));

        _clock.Advance(ReminderService.Grace - TimeSpan.FromSeconds(1));
        Assert.Equal(0, await service.SendRemindersAsync(Ct));

        _clock.Advance(TimeSpan.FromSeconds(1));      // exactly 7 days
        Assert.Equal(1, await service.SendRemindersAsync(Ct));

        Assert.Equal(("ada@example.com", "Invoice A-1 is overdue"),
                     Assert.Single(_email.Sent));
        Assert.Equal("Sent a reminder for invoice A-1",
                     _log.LatestRecord.Message);
    }
}
#endregion

public class ReminderServiceMoreTests
{
    private static CancellationToken Ct =>
        TestContext.Current.CancellationToken;
    [Fact]
    public async Task Paid_invoices_are_skipped()
    {
        var due = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(due.AddDays(30));
        var email = new FakeEmail();
        var service = new ReminderService(
            new StubInvoices(
                new Invoice("B-2", "bob@example.com", 5m, due, true)),
            email, clock, new FakeLogger<ReminderService>());

        Assert.Equal(0, await service.SendRemindersAsync(Ct));
        Assert.Empty(email.Sent);
    }
}

public class MoneySplitTests
{
    #region p113-properties
    // Any whole-cent amount up to 1,000,000.00, and 1 to 50 parts.
    private static readonly Gen<(decimal Total, int Parts)> Inputs =
        Gen.Select(Gen.Long[0, 100_000_000].Select(c => c / 100m),
                   Gen.Int[1, 50]);

    [Fact]
    public void Parts_always_add_up_to_the_total() =>
        Inputs.Sample((total, parts) =>
            Money.Split(total, parts).Sum() == total);

    [Fact]
    public void Parts_differ_by_at_most_one_cent() =>
        Inputs.Sample((total, parts) =>
        {
            var split = Money.Split(total, parts);
            return split.Length == parts
                   && split.Max() - split.Min() <= 0.01m
                   && split.All(p => decimal.Round(p, 2) == p);
        });

    [Fact]
    public void The_naive_version_loses_cents()
    {
        // CsCheck reports the smallest failing input it found, such
        // as (408.51, 24), and a seed that reproduces the run.
        var failure = Assert.Throws<CsCheckException>(() =>
            Inputs.Sample((total, parts) =>
                Money.SplitNaive(total, parts).Sum() == total));
        Assert.Contains("seed", failure.Message,
                        StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    [Theory]
    [InlineData(10.00, 3, new[] { 3.34, 3.33, 3.33 })]
    [InlineData(0.05, 5, new[] { 0.01, 0.01, 0.01, 0.01, 0.01 })]
    [InlineData(0.02, 3, new[] { 0.01, 0.01, 0.00 })]
    public void Examples(double total, int parts, double[] expected) =>
        Assert.Equal(expected.Select(e => (decimal)e),
                     Money.Split((decimal)total, parts));

    [Fact]
    public void Rejects_fractions_of_a_cent() =>
        Assert.Throws<ArgumentException>(() => Money.Split(1.005m, 2));
}
