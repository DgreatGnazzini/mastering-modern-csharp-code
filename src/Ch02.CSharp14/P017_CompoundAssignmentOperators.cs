namespace MasteringModernCSharp.Ch02;

public sealed class Histogram(int buckets)
{
    private readonly long[] _counts = new long[buckets];

    public IReadOnlyList<long> Counts => _counts;

    public void Record(int bucket) => _counts[bucket]++;

    // Classic operator: allocates a new Histogram every time.
    public static Histogram operator +(Histogram a, Histogram b)
    {
        var result = new Histogram(a._counts.Length);
        result += a;
        result += b;
        return result;
    }

    // C# 14: `h += other` updates h in place, with no allocation.
    public void operator +=(Histogram other)
    {
        for (var i = 0; i < _counts.Length; i++)
            _counts[i] += other._counts[i];
    }
}
