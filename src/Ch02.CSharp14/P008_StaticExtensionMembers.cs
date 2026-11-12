namespace MasteringModernCSharp.Ch02;

public static class FactoryExtensions
{
    // Static members extend the type itself, not an instance.
    extension<T>(List<T>)
    {
        public static List<T> Repeat(T value, int count) =>
            [.. Enumerable.Repeat(value, count)];
    }

    extension(DateOnly)
    {
        public static DateOnly Today =>
            DateOnly.FromDateTime(DateTime.Today);
    }
}
