namespace MasteringModernCSharp.Ch04;

public record Member(string Tier, int YearsActive);

public record Cart(decimal Total, int ItemCount, Member? Member);

public static class Discounts
{
    // Rules read top to bottom; the first match wins.
    public static decimal Rate(Cart cart) => cart switch
    {
        { Total: <= 0 } => 0m,
        { Member.Tier: "Gold", Total: >= 100 } => 0.20m,
        { Member.Tier: "Gold" } => 0.10m,
        { Member.YearsActive: >= 5 } => 0.08m,
        { ItemCount: >= 10 } or { Total: > 500 } => 0.05m,
        _ => 0m,
    };
}
