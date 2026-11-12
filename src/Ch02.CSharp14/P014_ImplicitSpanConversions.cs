namespace MasteringModernCSharp.Ch02;

public static class SpanStats
{
    // One implementation over ReadOnlySpan<T> serves arrays,
    // spans and slices alike.
    extension<T>(ReadOnlySpan<T> values) where T : IComparable<T>
    {
        public int CountAbove(T threshold)
        {
            var count = 0;
            foreach (var value in values)
            {
                if (value.CompareTo(threshold) > 0) count++;
            }
            return count;
        }
    }

    public static double Average(ReadOnlySpan<double> values)
    {
        if (values.IsEmpty) return 0;
        var sum = 0.0;
        foreach (var value in values) sum += value;
        return sum / values.Length;
    }
}
