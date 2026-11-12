using Microsoft.Extensions.Logging;

namespace MasteringModernCSharp.Ch12;

#region p113-split
public static class Money
{
    // Splits an amount into equal parts to the cent. The first parts
    // take the leftover cents, so the parts always add up.
    public static decimal[] Split(decimal total, int parts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parts);
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        if (decimal.Round(total, 2) != total)
            throw new ArgumentException("Use whole cents.", nameof(total));

        var cents = (long)(total * 100);
        var (each, extra) = Math.DivRem(cents, parts);
        return [.. Enumerable.Range(0, parts)
            .Select(i => (each + (i < extra ? 1 : 0)) / 100m)];
    }

    // The first version: rounds every part the same way.
    public static decimal[] SplitNaive(decimal total, int parts) =>
        [.. Enumerable.Repeat(decimal.Round(total / parts, 2), parts)];
}
#endregion

#region p113-reminders
public record Invoice(string Number, string Email, decimal Amount,
                      DateTimeOffset DueAt, bool Paid);

public interface IInvoiceStore
{
    Task<IReadOnlyList<Invoice>> UnpaidAsync(CancellationToken ct);
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body,
                   CancellationToken ct);
}

public sealed partial class ReminderService(
    IInvoiceStore invoices, IEmailSender email, TimeProvider clock,
    ILogger<ReminderService> log)
{
    public static readonly TimeSpan Grace = TimeSpan.FromDays(7);

    public async Task<int> SendRemindersAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var sent = 0;
        foreach (var invoice in await invoices.UnpaidAsync(ct))
        {
            if (invoice.Paid || now - invoice.DueAt < Grace) continue;
            await email.SendAsync(invoice.Email,
                $"Invoice {invoice.Number} is overdue",
                $"Please pay {invoice.Amount:C}.", ct);
            LogReminder(invoice.Number);
            sent++;
        }
        return sent;
    }

    [LoggerMessage(Level = LogLevel.Information,
                   Message = "Sent a reminder for invoice {Number}")]
    private partial void LogReminder(string number);
}
#endregion
