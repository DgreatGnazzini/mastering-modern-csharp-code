namespace MasteringModernCSharp.Ch02;

// Before: classic extension methods.
//
// public static class NumberExtensions
// {
//     public static bool IsEven(this int n) => n % 2 == 0;
//     public static int Clamp(this int n, int min, int max) =>
//         Math.Clamp(n, min, max);
// }

// After: the same members in an extension block.
public static class NumberExtensions
{
    extension(int n)
    {
        public bool IsEven() => n % 2 == 0;

        public int Clamp(int min, int max) => Math.Clamp(n, min, max);

        // New members can join the block without breaking anyone.
        public bool IsOdd => !n.IsEven();
    }
}
