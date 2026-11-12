using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using MasteringModernCSharp.Ch11;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using OpenTelemetry.Trace;

namespace MasteringModernCSharp.Ch11.Tests;

// Stands in for the pricing service: fails a set number of
// times, then answers.
public sealed class FlakyPricing(int failures, HttpStatusCode failWith)
    : HttpMessageHandler
{
    public int Calls;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var call = Interlocked.Increment(ref Calls);
        if (call <= failures)
            return Task.FromResult(new HttpResponseMessage(failWith));
        var sku = request.RequestUri!.Segments[^1];
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new Price(sku, 9.99m)),
        });
    }
}

// The exporter adds spans from server threads while a test reads
// them, so access goes through a lock.
public sealed class SpanList : Collection<Activity>
{
    private readonly Lock _gate = new();

    protected override void InsertItem(int index, Activity item)
    {
        lock (_gate) base.InsertItem(index, item);
    }

    public Activity[] Snapshot()
    {
        lock (_gate) return [.. this];
    }
}

public sealed class CatalogFactory(FlakyPricing pricing)
    : WebApplicationFactory<Program>
{
    public SpanList Spans { get; } = [];
    public FakeLogCollector Logs =>
        Services.GetRequiredService<FakeLogCollector>();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
        {
            // Swap the network for the fake. Calling AddPricingClient
            // again would stack a second resilience pipeline.
            services.AddHttpClient<PricingClient>()
                .ConfigurePrimaryHttpMessageHandler(() => pricing);
            // No real waiting between retries in tests. The standard
            // handler's options are named "<client>-standard".
            services.Configure<HttpStandardResilienceOptions>(
                "PricingClient-standard",
                o => o.Retry.Delay = TimeSpan.Zero);
            services.ConfigureOpenTelemetryTracerProvider(
                t => t.AddInMemoryExporter(Spans));
            services.AddLogging(l => l.AddFakeLogging());
        });
}

public class P102_ResilienceTests
{
    [Fact]
    public async Task Transient_failures_are_retried()
    {
        var pricing = new FlakyPricing(2, HttpStatusCode.ServiceUnavailable);
        await using var factory = new CatalogFactory(pricing);
        var response = await factory.CreateClient().GetAsync(
            "/products/A1", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, pricing.Calls);
    }

    [Fact]
    public async Task Not_found_is_not_retried()
    {
        var pricing = new FlakyPricing(int.MaxValue, HttpStatusCode.NotFound);
        await using var factory = new CatalogFactory(pricing);
        var response = await factory.CreateClient().GetAsync(
            "/products/nope", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, pricing.Calls);
    }

    [Fact]
    public async Task Gives_up_after_the_retry_budget()
    {
        var pricing = new FlakyPricing(int.MaxValue, HttpStatusCode.InternalServerError);
        await using var factory = new CatalogFactory(pricing);
        var response = await factory.CreateClient().GetAsync(
            "/products/A1", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(4, pricing.Calls); // 1 try + 3 retries
    }
}

public class P105_TelemetryTests
{
    [Fact]
    public async Task Custom_span_is_recorded_inside_the_request_span()
    {
        await using var factory = new CatalogFactory(new(0, HttpStatusCode.OK));
        await factory.CreateClient().GetAsync(
            "/products/OTEL-1", TestContext.Current.CancellationToken);

        // Listeners are process-wide and tests run in parallel, so
        // pick out this request's trace by its unique SKU.
        var lookup = factory.Spans.Snapshot().Single(
            s => s.DisplayName == "LookupPrice"
                 && (string?)s.GetTagItem("catalog.sku") == "OTEL-1");

        // The server span ends just after the response is sent, so
        // it can reach the exporter a moment after the client returns.
        Activity? request = null;
        for (var i = 0; i < 100 && request is null; i++)
        {
            request = factory.Spans.Snapshot().SingleOrDefault(
                s => s.TraceId == lookup.TraceId
                     && s.Kind == ActivityKind.Server);
            if (request is null)
            {
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }
        }
        Assert.NotNull(request);
        Assert.Equal(request.SpanId, lookup.ParentSpanId);
        // No HTTP client span here: the runtime emits it from inside
        // its own socket handler, which the fake pricing handler
        // replaces. Against a real service it appears as a child
        // of LookupPrice.
    }
}

public class P106_LoggerMessageTests
{
    [Fact]
    public void Writes_structured_log_entries()
    {
        var logger = new FakeLogger();
        logger.ProductNotFound("Z9");

        var record = logger.LatestRecord;
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(1002, record.Id.Id);
        Assert.Equal("Product Z9 not found", record.Message);
        Assert.Contains(record.StructuredState!,
            kv => kv.Key == "Sku" && kv.Value == "Z9");
    }

    [Fact]
    public async Task App_logs_through_the_generated_methods()
    {
        await using var factory = new CatalogFactory(new(0, HttpStatusCode.OK));
        await factory.CreateClient().GetAsync(
            "/products/A1", TestContext.Current.CancellationToken);
        Assert.Contains(factory.Logs.GetSnapshot(), r => r.Id.Id == 1001);
    }
}

public class P109_HealthTests
{
    [Fact]
    public async Task Ready_until_shutdown_starts()
    {
        await using var factory = new CatalogFactory(new(0, HttpStatusCode.OK));
        var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/health/live", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/health/ready", ct)).StatusCode);

        // Resolve first: stopping the app disposes its services.
        var readiness = factory.Services.GetRequiredService<Readiness>();

        // What happens when Kubernetes sends SIGTERM.
        factory.Services.GetRequiredService<IHostApplicationLifetime>()
            .StopApplication();

        Assert.True(readiness.Draining);
    }

    [Fact]
    public async Task Draining_instance_reports_not_ready()
    {
        await using var factory = new CatalogFactory(new(0, HttpStatusCode.OK));
        var client = factory.CreateClient();
        factory.Services.GetRequiredService<Readiness>().Draining = true;
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.GetAsync("/health/ready", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/health/live", ct)).StatusCode);
    }
}
