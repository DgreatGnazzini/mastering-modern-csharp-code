namespace MasteringModernCSharp.Ch03;

// Small value, compared by value, never changes:
// readonly record struct.
public readonly record struct Money(decimal Amount, string Currency)
{
    public Money Add(Money other) =>
        other.Currency == Currency
            ? this with { Amount = Amount + other.Amount }
            : throw new InvalidOperationException("Currency mismatch.");
}

// An entity-like object passed around by reference,
// compared by value: record class.
public record Customer(string Id, string Name, string Email);

// Hot-path numeric type with custom members and no need for
// generated equality or ToString: readonly struct.
public readonly struct Point3(double x, double y, double z)
{
    public double X { get; } = x;
    public double Y { get; } = y;
    public double Z { get; } = z;

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
}
