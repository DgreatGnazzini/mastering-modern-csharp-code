namespace MasteringModernCSharp.Ch02;

public sealed class HitCounter
{
    public long Value { get; private set; }

    // C# 14: instance ++ and -- mutate this object in place.
    public void operator ++() => Value++;

    public void operator --()
    {
        if (Value > 0) Value--;
    }
}
