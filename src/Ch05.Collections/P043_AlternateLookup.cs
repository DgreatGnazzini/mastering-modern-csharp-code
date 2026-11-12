namespace MasteringModernCSharp.Ch05;

public static class WordCounter
{
    // Counts words in a large text without allocating a string
    // for each word.
    public static Dictionary<string, int> Count(ReadOnlySpan<char> text)
    {
        var counts = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);
        var lookup = counts.GetAlternateLookup<ReadOnlySpan<char>>();

        foreach (var range in text.SplitAny(" \t\r\n.,;:!?"))
        {
            var word = text[range];
            if (word.IsEmpty) continue;

            // Looks up by span; a string is created only for new words.
            lookup.TryGetValue(word, out var n);
            lookup[word] = n + 1;
        }
        return counts;
    }
}
