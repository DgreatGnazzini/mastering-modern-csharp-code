using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MasteringModernCSharp.Ch11;

// Flips to "draining" as soon as shutdown begins, so the load
// balancer stops sending new requests before the app exits.
public sealed class Readiness
{
    public volatile bool Draining;
}

public sealed class ReadinessCheck(Readiness readiness) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default) =>
        Task.FromResult(readiness.Draining
            ? HealthCheckResult.Unhealthy("Shutting down")
            : HealthCheckResult.Healthy());
}

public static class Health
{
    public static IServiceCollection AddHealth(
        this IServiceCollection services)
    {
        services.AddSingleton<Readiness>();
        services.AddHealthChecks()
            // Liveness: is the process able to answer at all?
            .AddCheck("self", () => HealthCheckResult.Healthy(),
                tags: ["live"])
            // Readiness: should it receive traffic right now?
            .AddCheck<ReadinessCheck>("ready", tags: ["ready"]);

        // Give in-flight requests time to finish on SIGTERM.
        services.Configure<HostOptions>(o =>
            o.ShutdownTimeout = TimeSpan.FromSeconds(30));
        return services;
    }

    public static void MapHealth(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });

        var readiness = app.Services.GetRequiredService<Readiness>();
        app.Lifetime.ApplicationStopping.Register(
            () => readiness.Draining = true);
    }
}
