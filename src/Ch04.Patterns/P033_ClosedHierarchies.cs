using System.Diagnostics;

namespace MasteringModernCSharp.Ch04;

// Every possible outcome of a payment, in one place.
public abstract record PaymentResult
{
    private PaymentResult() { } // no subclasses outside this record

    public sealed record Approved(string TransactionId) : PaymentResult;
    public sealed record Declined(string Reason) : PaymentResult;
    public sealed record NeedsVerification(Uri VerifyUrl)
        : PaymentResult;
}

public static class PaymentMessages
{
    public static string Describe(PaymentResult result) => result switch
    {
        PaymentResult.Approved(var id) => $"Paid. Reference {id}.",
        PaymentResult.Declined(var reason) => $"Declined: {reason}",
        PaymentResult.NeedsVerification(var url) =>
            $"Please verify at {url}",
        // The compiler can't prove the list above is complete.
        _ => throw new UnreachableException(),
    };
}
