using System.Collections.Frozen;

namespace MasteringModernCSharp.Ch05;

public static class CountryCodes
{
    // Built once at startup, then read millions of times.
    private static readonly FrozenDictionary<string, string> Names =
        new Dictionary<string, string>
        {
            ["US"] = "United States",
            ["GB"] = "United Kingdom",
            ["DE"] = "Germany",
            ["JP"] = "Japan",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> Eu =
        new[] { "DE", "FR", "IT", "ES", "NL" }
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static string? NameOf(string code) =>
        Names.GetValueOrDefault(code);

    public static bool IsInEu(string code) => Eu.Contains(code);
}
