using System.Net.Http.Headers;

namespace MasteringModernCSharp.Ch11;

public record StockLevel(string Sku, int Available);

// A typed client: HttpClient arrives configured and ready.
public sealed class StockClient(HttpClient http)
{
    public Task<StockLevel?> GetAsync(string sku, CancellationToken ct) =>
        http.GetFromJsonAsync<StockLevel>($"/stock/{sku}", ct);
}

// Runs on every request the client sends, like middleware.
public sealed class ApiKeyHandler(IConfiguration config)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Add("X-Api-Key", config["Services:StockKey"]);
        return base.SendAsync(request, ct);
    }
}

public static class StockRegistration
{
    public static IHttpClientBuilder AddStockClient(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddTransient<ApiKeyHandler>();
        return services.AddHttpClient<StockClient>(http =>
            {
                http.BaseAddress = new Uri(
                    config["Services:Stock"] ?? "http://localhost:5101");
                http.Timeout = TimeSpan.FromSeconds(10);
                http.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("catalog", "1.0"));
            })
            .AddHttpMessageHandler<ApiKeyHandler>()
            // Re-resolve DNS every 2 minutes, even if this client
            // ends up captured by a long-lived singleton.
            .UseSocketsHttpHandler((handler, _) =>
                handler.PooledConnectionLifetime =
                    TimeSpan.FromMinutes(2))
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
    }
}

// Outside DI (a console tool, a static helper): one shared client
// with a connection lifetime, never "new HttpClient()" per call.
public static class SharedHttp
{
    public static HttpClient Client { get; } = new(
        new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        });
}
