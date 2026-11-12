namespace MasteringModernCSharp.Ch03;

public interface IClock
{
    DateTimeOffset Now { get; }
}

public record Invoice(string Number, decimal Amount, DateTimeOffset Due);

// Dependencies arrive through the primary constructor and are
// captured where they're used. No fields, no assignments.
public sealed class InvoiceService(IClock clock, decimal lateFeeRate)
{
    public bool IsOverdue(Invoice invoice) => clock.Now > invoice.Due;

    public decimal AmountDue(Invoice invoice) =>
        IsOverdue(invoice)
            ? invoice.Amount * (1 + lateFeeRate)
            : invoice.Amount;
}

// When a value must never change, copy it into a readonly field
// and use only the field from then on.
public sealed class RetryPolicy(int maxAttempts)
{
    private readonly int _maxAttempts = maxAttempts > 0
        ? maxAttempts
        : throw new ArgumentOutOfRangeException(nameof(maxAttempts));

    public int MaxAttempts => _maxAttempts;

    public bool ShouldRetry(int attempt) => attempt < _maxAttempts;
}
