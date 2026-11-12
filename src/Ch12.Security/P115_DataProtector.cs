using System.Security.Cryptography;

namespace MasteringModernCSharp.Ch12;

// Encrypts with AES-256-GCM. Output layout:
// [version:1][nonce:12][tag:16][ciphertext:n]
public sealed class DataProtector(byte[] key) : IDisposable
{
    private const byte Version = 1;
    private const int NonceSize = 12;  // AesGcm.NonceByteSizes.MaxSize
    private const int TagSize = 16;    // AesGcm.TagByteSizes.MaxSize
    private const int HeaderSize = 1 + NonceSize + TagSize;

    private readonly AesGcm _aes = new(key, TagSize);

    public static byte[] NewKey() => RandomNumberGenerator.GetBytes(32);

    public byte[] Encrypt(
        ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> context = default)
    {
        var output = new byte[HeaderSize + plaintext.Length];
        output[0] = Version;
        var nonce = output.AsSpan(1, NonceSize);
        var tag = output.AsSpan(1 + NonceSize, TagSize);
        var ciphertext = output.AsSpan(HeaderSize);

        // Never reuse a nonce with the same key: random 96 bits
        // is safe for about 2^32 messages per key.
        RandomNumberGenerator.Fill(nonce);
        _aes.Encrypt(nonce, plaintext, ciphertext, tag, context);
        return output;
    }

    public byte[] Decrypt(
        ReadOnlySpan<byte> data, ReadOnlySpan<byte> context = default)
    {
        if (data.Length < HeaderSize || data[0] != Version)
        {
            throw new CryptographicException("Unknown format.");
        }
        var plaintext = new byte[data.Length - HeaderSize];
        // Throws AuthenticationTagMismatchException if anything
        // (ciphertext, nonce, tag or context) was changed.
        _aes.Decrypt(
            data.Slice(1, NonceSize), data[HeaderSize..],
            data.Slice(1 + NonceSize, TagSize), plaintext, context);
        return plaintext;
    }

    public void Dispose() => _aes.Dispose();
}
