namespace MasteringModernCSharp.Ch02;

public delegate bool TryParser<T>(string text, out T result);

public static class Parsers
{
    // Before C# 14: (string text, out int result) => ...
    public static readonly TryParser<int> Int =
        (text, out result) => int.TryParse(text, out result);

    public static readonly TryParser<TimeSpan> Minutes =
        (text, out result) =>
        {
            var ok = double.TryParse(text, out var minutes);
            result = ok ? TimeSpan.FromMinutes(minutes) : default;
            return ok;
        };

    public static List<T> ParseAll<T>(
        IEnumerable<string> inputs, TryParser<T> parse)
    {
        List<T> results = [];
        foreach (var input in inputs)
        {
            if (parse(input, out var value)) results.Add(value);
        }
        return results;
    }
}
