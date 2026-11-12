using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MasteringModernCSharp.Ch11;

public static class Telemetry
{
    public const string Name = "Shop.Catalog";

    // Our own spans and metrics, next to the framework's.
    public static readonly ActivitySource Source = new(Name);
    private static readonly Meter Meter = new(Name);
    public static readonly Counter<long> Lookups =
        Meter.CreateCounter<long>("catalog.lookups");

    public static IHostApplicationBuilder AddTelemetry(
        this IHostApplicationBuilder builder)
    {
        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("catalog"))
            .WithTracing(tracing => tracing
                .AddSource(Name)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddMeter(Name)
                .AddMeter("System.Runtime")  // GC, threads, CPU
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithLogging();

        // Send everything over OTLP when a collector is configured,
        // for example by Aspire (Problem 100) or in Kubernetes.
        if (builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            is not null)
        {
            otel.UseOtlpExporter();
        }
        return builder;
    }
}
