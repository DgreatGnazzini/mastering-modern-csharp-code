using System.Diagnostics.Metrics;

namespace MasteringModernCSharp.Ch08;

// Custom metrics that dotnet-counters and OpenTelemetry can read.
//   dotnet-counters monitor -n MyApp --counters Shop.Orders
public sealed class OrderMetrics : IDisposable
{
    public const string MeterName = "Shop.Orders";

    private readonly Meter _meter;
    private readonly Counter<long> _placed;
    private readonly Histogram<double> _checkoutMs;

    public OrderMetrics()
    {
        _meter = new Meter(MeterName, "1.0");
        _placed = _meter.CreateCounter<long>(
            "shop.orders.placed", unit: "{order}");
        _checkoutMs = _meter.CreateHistogram<double>(
            "shop.checkout.duration", unit: "ms");
    }

    public void OrderPlaced(string region, TimeSpan checkoutTime)
    {
        _placed.Add(1,
            new KeyValuePair<string, object?>("region", region));
        _checkoutMs.Record(checkoutTime.TotalMilliseconds);
    }

    public void Dispose() => _meter.Dispose();
}
