using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;

namespace MasteringModernCSharp.Ch09;

public record PriceTick(string Symbol, decimal Price, DateTimeOffset At);

public static class PriceStream
{
    // GET /prices/MSFT streams ticks as server-sent events.
    // Browsers read it with: new EventSource("/prices/MSFT").
    public static void MapPriceStream(this IEndpointRouteBuilder app) =>
        app.MapGet("/prices/{symbol}", (
            string symbol, int? count, TimeProvider time,
            CancellationToken ct) =>
            TypedResults.ServerSentEvents(
                Ticks(symbol, count ?? int.MaxValue, time, ct)));

    private static async IAsyncEnumerable<SseItem<PriceTick>> Ticks(
        string symbol, int count, TimeProvider time,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var price = 100m;
        for (var i = 0; i < count; i++)
        {
            price += Random.Shared.Next(-100, 101) / 100m;
            var tick = new PriceTick(symbol, price, time.GetUtcNow());
            yield return new SseItem<PriceTick>(tick, eventType: "tick")
            {
                // Lets a reconnecting browser say where it left off.
                EventId = i.ToString(),
            };
            await Task.Delay(TimeSpan.FromMilliseconds(250), time, ct);
        }
    }
}
