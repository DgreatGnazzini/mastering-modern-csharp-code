using System.Security.Cryptography;
using System.Text;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MasteringModernCSharp.Ch12.Tests;

public class P114_SecretsTests
{
    private static HostApplicationBuilder Builder(
        string environment, Dictionary<string, string?> config)
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = environment,
                DisableDefaults = true,
            });
        builder.Configuration.AddInMemoryCollection(config);
        return builder;
    }

    [Fact]
    public void Development_adds_user_secrets()
    {
        var builder = Builder("Development", []);
        builder.AddAppSecrets();
        Assert.Contains(builder.Configuration.Sources,
            s => s is JsonConfigurationSource { Path: "secrets.json" });
    }

    [Fact]
    public void Production_does_not_read_user_secrets()
    {
        var builder = Builder("Production", []);
        builder.AddAppSecrets();
        Assert.DoesNotContain(builder.Configuration.Sources,
            s => s is JsonConfigurationSource { Path: "secrets.json" });
    }

    [Fact]
    public async Task Missing_secret_fails_at_startup()
    {
        var builder = Builder("Production", new()
        {
            ["Payment:Endpoint"] = "https://pay.example.com",
        });
        builder.AddAppSecrets();
        using var host = builder.Build();

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));
        Assert.Contains("ApiKey", ex.Message);
    }

    [Fact]
    public async Task Valid_secrets_bind_to_options()
    {
        var builder = Builder("Production", new()
        {
            ["Payment:ApiKey"] = "sk_test_123",
            ["Payment:Endpoint"] = "https://pay.example.com",
        });
        builder.AddAppSecrets();
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        var options = host.Services
            .GetRequiredService<IOptions<PaymentOptions>>().Value;
        Assert.Equal("sk_test_123", options.ApiKey);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Prefix_manager_maps_vault_names_to_config_keys()
    {
        var manager = new PrefixSecretManager("Shop");

        Assert.True(manager.Load(
            SecretModelFactory.SecretProperties(name: "Shop--Payment")));
        Assert.False(manager.Load(
            SecretModelFactory.SecretProperties(name: "Blog--Payment")));
        Assert.Equal("Payment:ApiKey", manager.GetKey(
            new KeyVaultSecret("Shop--Payment--ApiKey", "x")));
    }
}

public class P115_HashingAndEncryptionTests
{
    [Fact]
    public void Same_password_gives_different_hashes()
    {
        var a = PasswordHasher.Hash("hunter2");
        var b = PasswordHasher.Hash("hunter2");
        Assert.NotEqual(a, b);   // random salt
        Assert.True(PasswordHasher.Verify("hunter2", a));
        Assert.True(PasswordHasher.Verify("hunter2", b));
    }

    [Fact]
    public void Hash_records_its_settings()
    {
        var hash = PasswordHasher.Hash("pw");
        Assert.StartsWith($"v1.{PasswordHasher.Iterations}.", hash);
        Assert.False(PasswordHasher.NeedsRehash(hash));
    }

    [Fact]
    public void Old_hashes_still_verify_but_need_rehash()
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var old = Rfc2898DeriveBytes.Pbkdf2(
            "pw", salt, 10_000, HashAlgorithmName.SHA256, 32);
        var stored = $"v1.10000.{Convert.ToBase64String(salt)}." +
                     Convert.ToBase64String(old);

        Assert.True(PasswordHasher.Verify("pw", stored));
        Assert.True(PasswordHasher.NeedsRehash(stored));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("v2.1.AA==.AA==")]
    public void Malformed_hashes_fail_closed(string stored) =>
        Assert.False(PasswordHasher.Verify("pw", stored));

    [Fact]
    public void Round_trips_with_context()
    {
        using var protector = new DataProtector(DataProtector.NewKey());
        var context = "user:42"u8.ToArray();
        var secret = Encoding.UTF8.GetBytes("4111 1111 1111 1111");

        var sealedData = protector.Encrypt(secret, context);

        Assert.Equal(1 + 12 + 16 + secret.Length, sealedData.Length);
        Assert.Equal(secret, protector.Decrypt(sealedData, context));
    }

    [Fact]
    public void Encrypting_twice_gives_different_output()
    {
        using var protector = new DataProtector(DataProtector.NewKey());
        Assert.NotEqual(
            protector.Encrypt("x"u8), protector.Encrypt("x"u8));
    }

    [Fact]
    public void Tampering_is_detected()
    {
        using var protector = new DataProtector(DataProtector.NewKey());
        var data = protector.Encrypt("pay $10"u8);
        data[^1] ^= 1;   // flip one bit of the ciphertext

        Assert.ThrowsAny<CryptographicException>(
            () => protector.Decrypt(data));
    }

    [Fact]
    public void Wrong_context_is_detected()
    {
        using var protector = new DataProtector(DataProtector.NewKey());
        var data = protector.Encrypt("secret"u8, "user:1"u8);

        Assert.Throws<AuthenticationTagMismatchException>(
            () => protector.Decrypt(data, "user:2"u8));
    }

    [Fact]
    public void Unknown_version_is_rejected()
    {
        using var protector = new DataProtector(DataProtector.NewKey());
        var data = protector.Encrypt("x"u8);
        data[0] = 9;
        Assert.Throws<CryptographicException>(
            () => protector.Decrypt(data));
    }
}

public class P117_QuantumSafeTests
{
    private static readonly byte[] Message = "release v2.0"u8.ToArray();

    [Fact]
    public void Best_signer_signs_and_a_verifier_checks()
    {
        using var signer = Crypto.CreateSigner();
        var signature = signer.Sign(Message);

        Assert.True(Crypto.Verify(
            signer.Algorithm, signer.PublicKey, Message, signature));
        Assert.False(Crypto.Verify(
            signer.Algorithm, signer.PublicKey, "release v2.1"u8.ToArray(),
            signature));
    }

    [Fact]
    public void Best_key_exchange_agrees_on_a_secret()
    {
        using var receiver = Crypto.CreateKeyExchange();
        using var sender = Crypto.CreateKeyExchange();

        var (ciphertext, secret) = sender.Encapsulate(receiver.PublicKey);

        Assert.Equal(32, secret.Length);
        Assert.Equal(secret, receiver.Decapsulate(ciphertext));
    }

    [Fact]
    public void Classic_fallback_works_everywhere()
    {
        using var signer = Crypto.CreateSigner(preferPostQuantum: false);
        using var kex = Crypto.CreateKeyExchange(preferPostQuantum: false);
        Assert.Equal("ECDSA-P256", signer.Algorithm);
        Assert.Equal("ECDH-P256", kex.Algorithm);

        var (ciphertext, secret) = kex.Encapsulate(kex.PublicKey);
        Assert.Equal(secret, kex.Decapsulate(ciphertext));
    }

    [Fact]
    public void Picks_post_quantum_when_the_platform_has_it()
    {
        using var signer = Crypto.CreateSigner();
        using var kex = Crypto.CreateKeyExchange();
        Assert.Equal(
            MLDsa.IsSupported ? "ML-DSA-65" : "ECDSA-P256",
            signer.Algorithm);
        Assert.Equal(
            MLKem.IsSupported ? "ML-KEM-768" : "ECDH-P256",
            kex.Algorithm);
    }

    [Fact]
    public void ML_KEM_sizes_match_FIPS_203()
    {
        Assert.SkipUnless(MLKem.IsSupported,
            "ML-KEM needs OpenSSL 3.5+ or Windows with PQC support.");
        using var kex = new MLKemExchange();
        var (ciphertext, secret) = kex.Encapsulate(kex.PublicKey);

        Assert.Equal(1184, kex.PublicKey.Length);
        Assert.Equal(1088, ciphertext.Length);
        Assert.Equal(secret, kex.Decapsulate(ciphertext));
    }

    [Fact]
    public void ML_DSA_signature_size_matches_FIPS_204()
    {
        Assert.SkipUnless(MLDsa.IsSupported,
            "ML-DSA needs OpenSSL 3.5+ or Windows with PQC support.");
        using var signer = new MLDsaSigner();

        Assert.Equal(1952, signer.PublicKey.Length);
        Assert.Equal(3309, signer.Sign(Message).Length);
    }

    [Fact]
    public void Unknown_algorithm_is_rejected() =>
        Assert.Throws<NotSupportedException>(() =>
            Crypto.Verify("RSA-512", [], Message, []));
}
