using System.Globalization;

namespace MasteringModernCSharp.Ch05;

public static class NaturalSort
{
    // "file2" before "file10", the way people expect.
    private static readonly StringComparer Natural =
        StringComparer.Create(
            CultureInfo.InvariantCulture,
            CompareOptions.NumericOrdering | CompareOptions.IgnoreCase);

    public static List<string> Sort(IEnumerable<string> names) =>
        names.Order(Natural).ToList();
}
