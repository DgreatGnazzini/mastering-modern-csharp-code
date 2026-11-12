using System.Net;
using MasteringModernCSharp.Ch09;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MasteringModernCSharp.Ch09.Tests;

public class P077_DependencyInjectionTests(ApiFactory factory)
    : ApiTest(factory)
{
    private sealed class Clock { }
    private sealed class Captive(RequestInfo request)
    {
        public RequestInfo Request { get; } = request;
    }
    private sealed class NeedsClock(Clock clock)
    {
        public Clock Clock { get; } = clock;
    }

    [Fact]
    public void Lifetimes_control_how_instances_are_shared()
    {
        var services = new ServiceCollection();
        services.AddTransient<Clock>();
        services.AddScoped<RequestInfo>();
        using var provider = services.BuildServiceProvider();

        using var a = provider.CreateScope();
        using var b = provider.CreateScope();
        var sp = a.ServiceProvider;

        Assert.NotSame(sp.GetService<Clock>(), sp.GetService<Clock>());
        Assert.Same(sp.GetService<RequestInfo>(),
                    sp.GetService<RequestInfo>());
        Assert.NotSame(sp.GetService<RequestInfo>(),
                       b.ServiceProvider.GetService<RequestInfo>());
    }

    #region p077
    [Fact]
    public void Scope_validation_catches_a_captive_dependency()
    {
        var services = new ServiceCollection();
        services.AddScoped<RequestInfo>();
        services.AddSingleton<Captive>();   // singleton -> scoped: bug
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        var ex = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<Captive>());
        Assert.Contains("Cannot consume scoped service", ex.Message);
    }
    #endregion

    [Fact]
    public void ValidateOnBuild_catches_a_missing_registration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<NeedsClock>();   // Clock never added

        var ex = Assert.Throws<AggregateException>(
            () => services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateOnBuild = true }));
        Assert.Contains("Clock", ex.Message);
    }

    [Fact]
    public void Keyed_services_resolve_by_key()
    {
        using var provider = new ServiceCollection()
            .AddPayments().BuildServiceProvider();

        Assert.IsType<PayPalGateway>(
            provider.GetRequiredKeyedService<IPaymentGateway>("paypal"));
        Assert.Null(provider.GetService<IPaymentGateway>());  // no key
    }

    [Fact]
    public void Singletons_get_scoped_services_through_a_scope()
    {
        using var provider = new ServiceCollection()
            .AddPayments()
            .BuildServiceProvider(validateScopes: true);
        var audit = provider.GetRequiredService<AuditTrail>();

        Assert.NotEqual(audit.Record(), audit.Record());
    }

    [Fact]
    public async Task Endpoints_use_keyed_and_scoped_services()
    {
        var paypal = await Client.PostAsync(
            "/pay/paypal?amount=10", null, Ct);
        var unknown = await Client.PostAsync(
            "/pay/bitcoin?amount=10", null, Ct);
        var checkout = await Client.PostAsync(
            "/checkout?amount=5", null, Ct);

        Assert.Equal("\"paypal:10.00\"",
            await paypal.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.EndsWith(":card:5.00",
            await checkout.Content.ReadAsStringAsync(Ct));
    }
}

public class P078_OptionsTests
{
    private static (ServiceProvider, IConfigurationRoot) Build(
        Dictionary<string, string?> first,
        params Dictionary<string, string?>[] more)
    {
        var builder = new ConfigurationBuilder();
        foreach (var layer in more.Prepend(first))
        {
            builder.AddInMemoryCollection(layer);
        }
        var config = builder.Build();
        var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddShipping()
            .BuildServiceProvider();
        return (provider, config);
    }

    [Fact]
    public void Later_sources_override_earlier_ones()
    {
        var (provider, _) = Build(
            new() { ["Shipping:FreeShippingOver"] = "50",
                    ["Shipping:FlatRate"] = "4.99" },
            new Dictionary<string, string?>
            {
                ["Shipping:FreeShippingOver"] = "75",
            });

        var options = provider
            .GetRequiredService<IOptions<ShippingOptions>>().Value;
        Assert.Equal(75m, options.FreeShippingOver);   // overridden
        Assert.Equal(4.99m, options.FlatRate);         // kept
    }

    [Fact]
    public void Environment_variables_use_double_underscores()
    {
        var name = $"P078_{Guid.NewGuid():N}_";
        Environment.SetEnvironmentVariable(
            name + "Shipping__FlatRate", "2.50");
        try
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables(prefix: name).Build();
            Assert.Equal("2.50", config["Shipping:FlatRate"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                name + "Shipping__FlatRate", null);
        }
    }

    #region p078
    [Fact]
    public void Snapshot_is_fixed_per_scope_and_monitor_is_live()
    {
        var (provider, config) = Build(new()
        {
            ["Shipping:FreeShippingOver"] = "50",
            ["Carriers:Ups:TrackingUrl"] = "https://ups/{0}",
        });
        var links = provider.GetRequiredService<CarrierLinks>();
        using var before = provider.CreateScope();
        var oldQuote = before.ServiceProvider
            .GetRequiredService<ShippingQuote>();

        // Simulates editing appsettings.json while the app runs.
        config["Shipping:FreeShippingOver"] = "100";
        config["Carriers:Ups:TrackingUrl"] = "https://ups.new/{0}";
        config.Reload();

        using var after = provider.CreateScope();
        var newQuote = after.ServiceProvider
            .GetRequiredService<ShippingQuote>();
        Assert.Equal(0m, oldQuote.For(60m));      // old request: 50
        Assert.Equal(4.99m, newQuote.For(60m));   // new request: 100
        Assert.Equal("https://ups.new/P1", links.Track("ups", "P1"));
        Assert.True(links.Changes > 0);
    }
    #endregion

    [Fact]
    public void IOptions_never_changes_after_first_read()
    {
        var (provider, config) = Build(
            new() { ["Shipping:FlatRate"] = "4.99" });
        var options = provider
            .GetRequiredService<IOptions<ShippingOptions>>();
        _ = options.Value;

        config["Shipping:FlatRate"] = "9.99";
        config.Reload();

        Assert.Equal(4.99m, options.Value.FlatRate);
    }

    [Fact]
    public void Named_options_hold_one_configuration_each()
    {
        var (provider, _) = Build(new()
        {
            ["Carriers:Ups:TrackingUrl"] = "https://ups/{0}",
            ["Carriers:Dhl:TrackingUrl"] = "https://dhl?id={0}",
        });
        var links = provider.GetRequiredService<CarrierLinks>();

        Assert.Equal("https://ups/1Z", links.Track("ups", "1Z"));
        Assert.Equal("https://dhl?id=JD", links.Track("dhl", "JD"));
    }
}
