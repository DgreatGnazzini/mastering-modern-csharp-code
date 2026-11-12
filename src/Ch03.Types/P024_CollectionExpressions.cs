using System.Collections.Immutable;

namespace MasteringModernCSharp.Ch03;

public static class Menus
{
    // One syntax for arrays, lists, spans and immutable collections.
    public static readonly string[] Starters = ["Soup", "Salad"];
    public static readonly List<string> Mains = ["Pasta", "Curry"];
    public static readonly ImmutableArray<string> Desserts =
        ["Cake", "Fruit"];

    // `..` spreads one collection into another.
    public static List<string> FullMenu(bool includeSpecial) =>
    [
        .. Starters,
        .. Mains,
        .. includeSpecial ? ["Chef's special"] : Array.Empty<string>(),
        .. Desserts,
    ];

    public static int CountVegetarian(ReadOnlySpan<string> dishes)
    {
        ReadOnlySpan<string> vegetarian = ["Soup", "Salad", "Fruit"];
        var count = 0;
        foreach (var dish in dishes)
            if (vegetarian.Contains(dish)) count++;
        return count;
    }
}
