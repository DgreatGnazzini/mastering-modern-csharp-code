using System.Globalization;
using System.Text.RegularExpressions;

namespace MasteringModernCSharp.Ch06;

public static partial class Validators
{
    // The regex engine is generated at build time. No startup
    // parsing, and readable C# you can step through in a debugger.
    [GeneratedRegex(@"^[A-Z]{2}\d{2}[A-Z0-9]{11,30}$",
                    RegexOptions.IgnoreCase)]
    private static partial Regex IbanPattern { get; }

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b")]
    private static partial Regex IsoDate();

    public static bool LooksLikeIban(string value) =>
        IbanPattern.IsMatch(value.Replace(" ", ""));

    public static IEnumerable<DateOnly> FindDates(string text)
    {
        foreach (Match m in IsoDate().Matches(text))
        {
            // The regex finds candidates; the parser rejects
            // impossible dates such as 2026-13-45.
            if (DateOnly.TryParseExact(m.Value, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
                yield return date;
        }
    }
}
