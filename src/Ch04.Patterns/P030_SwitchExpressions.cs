namespace MasteringModernCSharp.Ch04;

public enum ShippingSpeed { Standard, Express, Overnight }

public static class Shipping
{
    // Every input maps to exactly one result, in one expression.
    public static decimal Cost(ShippingSpeed speed, decimal weightKg) =>
        (speed, weightKg) switch
        {
            (_, <= 0) => throw new ArgumentOutOfRangeException(
                nameof(weightKg), "Weight must be positive."),
            (ShippingSpeed.Standard, <= 2) => 4.99m,
            (ShippingSpeed.Standard, _) => 4.99m + (weightKg - 2) * 1.5m,
            (ShippingSpeed.Express, <= 2) => 12.99m,
            (ShippingSpeed.Express, _) => 12.99m + (weightKg - 2) * 3m,
            (ShippingSpeed.Overnight, _) => 29.99m + weightKg * 4m,
            _ => throw new ArgumentOutOfRangeException(nameof(speed)),
        };
}
