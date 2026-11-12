namespace MasteringModernCSharp.Ch02;

public static class StringExtensions
{
    extension(string text)
    {
        // An extension property: reads like a built-in member.
        public bool IsBlank => string.IsNullOrWhiteSpace(text);

        public int WordCount =>
            text.Split((char[]?)null,
                       StringSplitOptions.RemoveEmptyEntries).Length;

        // Extension methods can live in the same block.
        public string Truncate(int maxLength) =>
            text.Length <= maxLength
                ? text
                : string.Concat(text.AsSpan(0, maxLength - 1), "…");
    }
}
