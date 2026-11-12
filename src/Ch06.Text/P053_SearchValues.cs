using System.Buffers;

namespace MasteringModernCSharp.Ch06;

public static class ContentFilter
{
    // Built once: the runtime picks the fastest search algorithm
    // for this exact set of words.
    private static readonly SearchValues<string> Blocked =
        SearchValues.Create(
            ["password", "secret", "api_key", "private key"],
            StringComparison.OrdinalIgnoreCase);

    private static readonly SearchValues<char> Unsafe =
        SearchValues.Create("<>\"'&");

    public static bool ContainsSecret(ReadOnlySpan<char> text) =>
        text.ContainsAny(Blocked);

    public static int FirstUnsafeChar(ReadOnlySpan<char> text) =>
        text.IndexOfAny(Unsafe);
}
