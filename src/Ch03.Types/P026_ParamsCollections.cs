namespace MasteringModernCSharp.Ch03;

public static class Stats
{
    // No array is allocated: arguments go into a stack span.
    public static int Sum(params ReadOnlySpan<int> values)
    {
        var total = 0;
        foreach (var value in values) total += value;
        return total;
    }

    // Accepts separate arguments, an array, a list or any sequence.
    public static string JoinWords(params IEnumerable<string> words) =>
        string.Join(' ', words.Where(w => !string.IsNullOrWhiteSpace(w)));
}
