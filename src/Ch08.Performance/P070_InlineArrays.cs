using System.Runtime.CompilerServices;

namespace MasteringModernCSharp.Ch08;

// Eight doubles stored inside the struct itself: no array object,
// no extra allocation, no pointer to follow.
[InlineArray(8)]
public struct EightDoubles
{
    private double _element0;
}

// Keeps the last 8 sensor readings. The whole thing is one value
// type, so a million of them are one flat block of memory.
public struct RecentReadings
{
    private EightDoubles _values;
    private int _next;
    private int _count;

    public readonly int Count => _count;

    public void Add(double value)
    {
        _values[_next] = value;
        _next = (_next + 1) % 8;
        _count = Math.Min(_count + 1, 8);
    }

    public readonly double Average()
    {
        if (_count == 0) return 0;
        // An inline array converts to a span.
        ReadOnlySpan<double> all = _values;
        var sum = 0.0;
        foreach (var v in all[.._count]) sum += v;
        return sum / _count;
    }
}
