using System.Net;
using System.Net.Http.Json;
using MasteringModernCSharp.Ch11;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MasteringModernCSharp.Ch11.Tests;

// Records each request and answers with a stock level.
public sealed class RecordingStock : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new StockLevel("A1", 7)),
        });
    }
}

public class P101_HttpClientFactoryTests
{
    private static (ServiceProvider, RecordingStock) Build()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Services:Stock"] = "https://stock.example",
                ["Services:StockKey"] = "secret-key",
            })
            .Build();
        var fake = new RecordingStock();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddStockClient(config)
            .ConfigurePrimaryHttpMessageHandler(() => fake);
        return (services.BuildServiceProvider(), fake);
    }

    [Fact]
    public async Task Typed_client_is_configured_and_sends_the_key()
    {
        var (provider, fake) = Build();
        using var _ = provider;
        var client = provider.GetRequiredService<StockClient>();

        var stock = await client.GetAsync(
            "A1", TestContext.Current.CancellationToken);

        Assert.Equal(7, stock?.Available);
        var request = Assert.Single(fake.Requests);
        Assert.Equal("https://stock.example/stock/A1",
                     request.RequestUri?.ToString());
        Assert.Equal(["secret-key"],
                     request.Headers.GetValues("X-Api-Key"));
        Assert.Contains("catalog/1.0",
                        request.Headers.UserAgent.ToString());
    }

    [Fact]
    public void Typed_clients_are_transient_but_share_handlers()
    {
        var (provider, _) = Build();
        using var _ = provider;

        var a = provider.GetRequiredService<StockClient>();
        var b = provider.GetRequiredService<StockClient>();
        Assert.NotSame(a, b);
    }

    [Fact]
    public void Default_registration_uses_a_SocketsHttpHandler()
    {
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddStockClient(config);
        using var provider = services.BuildServiceProvider();

        var handler = provider
            .GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler(nameof(StockClient));
        while (handler is DelegatingHandler { InnerHandler: { } inner })
        {
            handler = inner;
        }
        var sockets = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.Equal(TimeSpan.FromMinutes(2),
                     sockets.PooledConnectionLifetime);
    }
}
