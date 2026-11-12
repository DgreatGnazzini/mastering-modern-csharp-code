using System.Numerics.Tensors;
using System.Runtime.Intrinsics;

namespace MasteringModernCSharp.Ch08;

public static class Signal
{
    // Hand-written SIMD: add four floats per instruction, then
    // finish the leftovers one at a time.
    public static float SumVector128(ReadOnlySpan<float> values)
    {
        var acc = Vector128<float>.Zero;
        var i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            for (; i <= values.Length - Vector128<float>.Count;
                 i += Vector128<float>.Count)
            {
                acc += Vector128.Create(values.Slice(i));
            }
        }
        var sum = Vector128.Sum(acc);
        for (; i < values.Length; i++) sum += values[i];
        return sum;
    }

    // Usually better: let the library pick the widest vectors
    // the CPU supports (128, 256 or 512 bits).
    public static float Sum(ReadOnlySpan<float> values) =>
        TensorPrimitives.Sum(values);

    public static float Similarity(
        ReadOnlySpan<float> a, ReadOnlySpan<float> b) =>
        TensorPrimitives.CosineSimilarity(a, b);

    public static void Normalize(Span<float> values)
    {
        var norm = TensorPrimitives.Norm(values);
        if (norm > 0) TensorPrimitives.Divide(values, norm, values);
    }
}
