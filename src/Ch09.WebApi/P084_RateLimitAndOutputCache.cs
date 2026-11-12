using System.Threading.RateLimiting;

namespace MasteringModernCSharp.Ch09;

public static class Protection
{
    public static IServiceCollection AddProtection(
        this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode =
                StatusCodes.Status429TooManyRequests;

            // 5 requests per 10 seconds, counted per client IP.
            options.AddPolicy("per-client", http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "anon",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromSeconds(10),
                    }));
        });

        services.AddOutputCache(options =>
            options.AddPolicy("reports", policy => policy
                .Expire(TimeSpan.FromSeconds(30))
                .SetVaryByQuery("region")
                .Tag("reports")));
        return services;
    }

    public static void MapProtectedEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/search", (string q) => TypedResults.Ok(new { q }))
            .RequireRateLimiting("per-client");

        // Expensive to build; served from cache for 30 seconds.
        app.MapGet("/reports/sales", (string region, TimeProvider time) =>
                TypedResults.Ok(
                    new { region, builtAt = time.GetUtcNow() }))
            .CacheOutput("reports");
    }
}
