using System.Diagnostics;
using MasteringModernCSharp.Ch11;
using MasteringModernCSharp.Ch11.Modules;

var builder = WebApplication.CreateBuilder(args);
builder.AddTelemetry();                                     // 105
builder.Services.AddPricingClient(builder.Configuration);   // 102
builder.Services.AddHealth();                               // 109
builder.Services.AddStockClient(builder.Configuration);     // 101
builder.Services.AddInventory();                            // 103
builder.Services.AddOrderUpdates();                         // 104
builder.AddModules();                                       // 110

var app = builder.Build();
app.MapHealth();
app.MapInventory();
app.MapOrderUpdates();
app.MapModules();

app.MapGet("/products/{sku}", async (
    string sku, PricingClient pricing, ILogger<Program> log,
    CancellationToken ct) =>
{
    using var activity = Telemetry.Source.StartActivity("LookupPrice");
    activity?.SetTag("catalog.sku", sku);
    Telemetry.Lookups.Add(1);
    var started = Stopwatch.GetTimestamp();
    try
    {
        var price = await pricing.GetAsync(sku, ct);
        log.ProductLookedUp(sku,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return price is null
            ? Results.NotFound()
            : Results.Ok(price);
    }
    catch (HttpRequestException ex)
        when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        log.ProductNotFound(sku);
        return Results.NotFound();
    }
    catch (Exception ex) when (ex is HttpRequestException
        or Polly.ExecutionRejectedException) // timeout, open circuit
    {
        log.PricingFailed(ex, sku);
        return Results.StatusCode(
            StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();
