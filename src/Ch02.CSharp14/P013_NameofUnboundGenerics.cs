namespace MasteringModernCSharp.Ch02;

public static class CacheKeys
{
    // No need to invent a type argument just to get the name.
    public static string For<TKey, TValue>() =>
        $"{nameof(Dictionary<,>)}:" +
        $"{typeof(TKey).Name}:{typeof(TValue).Name}";

    public static readonly string ListName = nameof(List<>);
}
