using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace MasteringModernCSharp.Ch08;

// "30.27,-97.74" parsed straight from a span: no Split, no
// substrings, no garbage.
public readonly record struct GeoPoint(double Lat, double Lon)
    : ISpanParsable<GeoPoint>
{
    public static GeoPoint Parse(
        ReadOnlySpan<char> s, IFormatProvider? provider) =>
        TryParse(s, provider, out var point)
            ? point
            : throw new FormatException($"Bad point: '{s}'.");

    public static bool TryParse(
        ReadOnlySpan<char> s, IFormatProvider? provider,
        out GeoPoint result)
    {
        result = default;
        var comma = s.IndexOf(',');
        if (comma < 0) return false;

        provider ??= CultureInfo.InvariantCulture;
        var latText = s[..comma].Trim();
        var lonText = s[(comma + 1)..].Trim();
        if (!double.TryParse(latText, provider, out var lat) ||
            !double.TryParse(lonText, provider, out var lon))
        {
            return false;
        }
        if (lat is < -90 or > 90 || lon is < -180 or > 180) return false;

        result = new(lat, lon);
        return true;
    }

    // ISpanParsable<T> includes IParsable<T>; strings just forward.
    public static GeoPoint Parse(string s, IFormatProvider? provider) =>
        Parse(s.AsSpan(), provider);

    public static bool TryParse(
        [NotNullWhen(true)] string? s, IFormatProvider? provider,
        out GeoPoint result) =>
        TryParse(s.AsSpan(), provider, out result);
}

public static class SpanParsing
{
    // Works for any span-parsable type: int, Guid, DateOnly, GeoPoint...
    public static List<T> ParseList<T>(
        ReadOnlySpan<char> text, char separator = ';')
        where T : ISpanParsable<T>
    {
        var items = new List<T>();
        foreach (var range in text.Split(separator))
        {
            var part = text[range].Trim();
            if (!part.IsEmpty)
                items.Add(T.Parse(part, CultureInfo.InvariantCulture));
        }
        return items;
    }
}
