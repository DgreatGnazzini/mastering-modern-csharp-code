using Microsoft.Extensions.Http.Resilience;

namespace MasteringModernCSharp.Ch11;

public record Price(string Sku, decimal Amount);

// A typed client for the pricing service.
public sealed class PricingClient(HttpClient http)
{
    public Task<Price?> GetAsync(string sku, CancellationToken ct) =>
        http.GetFromJsonAsync<Price>($"/prices/{sku}", ct);
}

public static class PricingRegistration
{
    public static IHttpClientBuilder AddPricingClient(
        this IServiceCollection services, IConfiguration config,
        Action<HttpStandardResilienceOptions>? tune = null)
    {
        var client = services.AddHttpClient<PricingClient>(http =>
            http.BaseAddress = new Uri(
                config["Services:Pricing"] ?? "http://localhost:5100"));

        // Rate limiter, total timeout, retries with backoff,
        // circuit breaker and per-attempt timeout, in that order.
        client.AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
            options.TotalRequestTimeout.Timeout =
                TimeSpan.FromSeconds(10);
            // Must be at least twice the attempt timeout.
            options.CircuitBreaker.SamplingDuration =
                TimeSpan.FromSeconds(30);
            tune?.Invoke(options);
        });
        return client;
    }
}
