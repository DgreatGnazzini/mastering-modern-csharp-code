using System.ComponentModel.DataAnnotations;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MasteringModernCSharp.Ch12;

public sealed class PaymentOptions
{
    [Required] public string ApiKey { get; set; } = "";
    [Required, Url] public string Endpoint { get; set; } = "";
}

public static class SecretsSetup
{
    public static IHostApplicationBuilder AddAppSecrets(
        this IHostApplicationBuilder builder)
    {
        // Development: secrets.json in your user profile, outside
        // the repo. (WebApplication.CreateBuilder does this for the
        // entry assembly; a library adds its own explicitly.)
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets(
                typeof(SecretsSetup).Assembly, optional: true);
        }

        // Everywhere else: Azure Key Vault, reached with the app's
        // managed identity. No secret is needed to get the secrets.
        if (builder.Configuration["KeyVault:Uri"] is { } vaultUri)
        {
            builder.Configuration.AddAzureKeyVault(
                new Uri(vaultUri), new DefaultAzureCredential(),
                new PrefixSecretManager("Shop"));
        }

        builder.Services.AddOptions<PaymentOptions>()
            .BindConfiguration("Payment")
            .ValidateDataAnnotations()
            .ValidateOnStart();   // fail at startup, not first use
        return builder;
    }
}

// Lets several apps share one vault: this app loads only secrets
// named "Shop--...", and "--" becomes the ":" config separator.
public sealed class PrefixSecretManager(string prefix)
    : KeyVaultSecretManager
{
    private readonly string _prefix = prefix + "--";

    public override bool Load(SecretProperties secret) =>
        secret.Name.StartsWith(_prefix, StringComparison.Ordinal);

    public override string GetKey(KeyVaultSecret secret) =>
        secret.Name[_prefix.Length..].Replace("--", ":");
}
