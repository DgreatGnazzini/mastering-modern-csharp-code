using System.Security.Cryptography;

namespace MasteringModernCSharp.Ch12;

public static class PasswordHasher
{
    // OWASP's current guidance for PBKDF2 with HMAC-SHA256.
    public const int Iterations = 600_000;
    private const int SaltSize = 16;   // 128 bits
    private const int HashSize = 32;   // 256 bits
    private static readonly HashAlgorithmName Algorithm =
        HashAlgorithmName.SHA256;

    // Format: v1.{iterations}.{salt}.{hash}, with Base64 parts.
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, Algorithm, HashSize);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}." +
               Convert.ToBase64String(hash);
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts is not ["v1", var count, var salt64, var hash64]
            || !int.TryParse(count, out var iterations))
        {
            return false;
        }
        var expected = Convert.FromBase64String(hash64);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password, Convert.FromBase64String(salt64),
            iterations, Algorithm, expected.Length);
        // Compares every byte, so timing reveals nothing.
        return CryptographicOperations.FixedTimeEquals(
            actual, expected);
    }

    // True when the hash was made with weaker settings than today's.
    public static bool NeedsRehash(string stored) =>
        stored.Split('.') is not ["v1", var count, _, _]
        || !int.TryParse(count, out var n) || n < Iterations;
}
