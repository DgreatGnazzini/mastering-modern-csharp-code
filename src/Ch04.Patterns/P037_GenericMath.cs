using System.Numerics;

namespace MasteringModernCSharp.Ch04;

public static class NumericStats
{
    // One implementation for int, long, double, decimal, Half, ...
    public static T Mean<T>(ReadOnlySpan<T> values)
        where T : INumber<T>
    {
        if (values.IsEmpty)
            throw new ArgumentException("No values.", nameof(values));

        var sum = T.Zero;
        foreach (var value in values) sum += value;
        return sum / T.CreateChecked(values.Length);
    }

    public static T Clamp<T>(T value, T min, T max)
        where T : INumber<T> =>
        T.Max(min, T.Min(max, value));
}
