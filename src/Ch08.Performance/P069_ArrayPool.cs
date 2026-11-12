using System.Buffers;
using System.Security.Cryptography;

namespace MasteringModernCSharp.Ch08;

public static class FileHasher
{
    // Hashes a stream of any size with one borrowed 80 KB buffer
    // instead of allocating a new one per call.
    public static async Task<string> Sha256Async(
        Stream input, CancellationToken ct = default)
    {
        using var hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(81_920);
        try
        {
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                // Rent may return a bigger array: use only `read`.
                hash.AppendData(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
