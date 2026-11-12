using System.Security.Cryptography;

namespace MasteringModernCSharp.Ch12;

// Callers depend on this, never on a specific algorithm.
public interface ISigner : IDisposable
{
    string Algorithm { get; }
    byte[] PublicKey { get; }
    byte[] Sign(byte[] data);
    bool Verify(byte[] data, byte[] signature);
}

public sealed class MLDsaSigner : ISigner
{
    private readonly MLDsa _key =
        MLDsa.GenerateKey(MLDsaAlgorithm.MLDsa65);
    public string Algorithm => "ML-DSA-65";
    // Raw FIPS 204 key: the SubjectPublicKeyInfo export is still
    // marked experimental (SYSLIB5006) in .NET 10.
    public byte[] PublicKey => _key.ExportMLDsaPublicKey();
    public byte[] Sign(byte[] data) => _key.SignData(data);
    public bool Verify(byte[] data, byte[] signature) =>
        _key.VerifyData(data, signature);
    public void Dispose() => _key.Dispose();
}

public sealed class EcdsaSigner : ISigner
{
    private readonly ECDsa _key =
        ECDsa.Create(ECCurve.NamedCurves.nistP256);
    public string Algorithm => "ECDSA-P256";
    public byte[] PublicKey => _key.ExportSubjectPublicKeyInfo();
    public byte[] Sign(byte[] data) =>
        _key.SignData(data, HashAlgorithmName.SHA256);
    public bool Verify(byte[] data, byte[] signature) =>
        _key.VerifyData(data, signature, HashAlgorithmName.SHA256);
    public void Dispose() => _key.Dispose();
}

// One shared secret, two ways to agree on it.
public interface IKeyExchange : IDisposable
{
    string Algorithm { get; }
    byte[] PublicKey { get; }
    // Sender: make a secret for the owner of publicKey.
    (byte[] Ciphertext, byte[] Secret) Encapsulate(byte[] publicKey);
    // Receiver: recover the same secret.
    byte[] Decapsulate(byte[] ciphertext);
}

public sealed class MLKemExchange : IKeyExchange
{
    private readonly MLKem _key =
        MLKem.GenerateKey(MLKemAlgorithm.MLKem768);
    public string Algorithm => "ML-KEM-768";
    public byte[] PublicKey => _key.ExportEncapsulationKey();

    public (byte[], byte[]) Encapsulate(byte[] publicKey)
    {
        using var peer = MLKem.ImportEncapsulationKey(
            MLKemAlgorithm.MLKem768, publicKey);
        peer.Encapsulate(out var ciphertext, out var secret);
        return (ciphertext, secret);
    }

    public byte[] Decapsulate(byte[] ciphertext) =>
        _key.Decapsulate(ciphertext);
    public void Dispose() => _key.Dispose();
}

// Classic ECDH shaped like a KEM: the "ciphertext" is the
// sender's one-time public key.
public sealed class EcdhExchange : IKeyExchange
{
    private readonly ECDiffieHellman _key =
        ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
    public string Algorithm => "ECDH-P256";
    public byte[] PublicKey => _key.ExportSubjectPublicKeyInfo();

    public (byte[], byte[]) Encapsulate(byte[] publicKey)
    {
        using var ephemeral =
            ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        return (ephemeral.ExportSubjectPublicKeyInfo(),
                Derive(ephemeral, publicKey));
    }

    public byte[] Decapsulate(byte[] ciphertext) =>
        Derive(_key, ciphertext);

    private static byte[] Derive(ECDiffieHellman mine, byte[] theirs)
    {
        using var peer = ECDiffieHellman.Create();
        peer.ImportSubjectPublicKeyInfo(theirs, out _);
        return mine.DeriveKeyFromHash(
            peer.PublicKey, HashAlgorithmName.SHA256);
    }

    public void Dispose() => _key.Dispose();
}

public static class Crypto
{
    // ML-KEM and ML-DSA need OpenSSL 3.5+ on Linux, or a
    // recent Windows build. Fall back to elliptic curves elsewhere.
    public static ISigner CreateSigner(bool preferPostQuantum = true) =>
        preferPostQuantum && MLDsa.IsSupported
            ? new MLDsaSigner() : new EcdsaSigner();

    public static IKeyExchange CreateKeyExchange(
        bool preferPostQuantum = true) =>
        preferPostQuantum && MLKem.IsSupported
            ? new MLKemExchange() : new EcdhExchange();

    // A verifier holds only the public key and the algorithm name
    // that travelled with it.
    public static bool Verify(string algorithm, byte[] publicKey,
                              byte[] data, byte[] signature)
    {
        switch (algorithm)
        {
            case "ML-DSA-65":
                using (var key = MLDsa.ImportMLDsaPublicKey(
                           MLDsaAlgorithm.MLDsa65, publicKey))
                {
                    return key.VerifyData(data, signature);
                }
            case "ECDSA-P256":
                using (var key = ECDsa.Create())
                {
                    key.ImportSubjectPublicKeyInfo(publicKey, out _);
                    return key.VerifyData(
                        data, signature, HashAlgorithmName.SHA256);
                }
            default:
                throw new NotSupportedException(algorithm);
        }
    }
}
