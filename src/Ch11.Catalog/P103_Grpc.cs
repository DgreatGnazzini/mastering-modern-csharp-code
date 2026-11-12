using System.Collections.Concurrent;
using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MasteringModernCSharp.Ch11.Stock;

namespace MasteringModernCSharp.Ch11;

// Current stock, with an event for each change.
public sealed class StockLevels
{
    private readonly ConcurrentDictionary<string, int> _levels = new();

    public event Action<string, int>? Changed;

    public int? Get(string sku) =>
        _levels.TryGetValue(sku, out var quantity) ? quantity : null;

    public void Set(string sku, int quantity)
    {
        _levels[sku] = quantity;
        Changed?.Invoke(sku, quantity);
    }
}

#region p103-service
public sealed class InventoryService(
    StockLevels stock, TimeProvider clock) : Inventory.InventoryBase
{
    public override Task<StockReply> GetStock(
        StockRequest request, ServerCallContext context) =>
        stock.Get(request.Sku) is { } quantity
            ? Task.FromResult(Reply(request.Sku, quantity))
            : throw new RpcException(new Status(
                StatusCode.NotFound, $"No such SKU: {request.Sku}"));

    public override async Task WatchStock(WatchRequest request,
        IServerStreamWriter<StockReply> responses,
        ServerCallContext context)
    {
        var changes = Channel.CreateUnbounded<StockReply>();
        void OnChanged(string sku, int quantity)
        {
            if (request.Skus.Contains(sku))
                changes.Writer.TryWrite(Reply(sku, quantity));
        }

        stock.Changed += OnChanged;
        try
        {
            foreach (var sku in request.Skus)
            {
                if (stock.Get(sku) is { } quantity)
                    await responses.WriteAsync(Reply(sku, quantity));
            }
            // Runs until the client cancels or the deadline passes.
            var ct = context.CancellationToken;
            await foreach (var reply in changes.Reader.ReadAllAsync(ct))
                await responses.WriteAsync(reply, ct);
        }
        catch (OperationCanceledException)
        {
            // The client went away: end the stream quietly.
        }
        finally
        {
            stock.Changed -= OnChanged;     // no leak (Problem 74)
        }
    }

    private StockReply Reply(string sku, int quantity) => new()
    {
        Sku = sku,
        Quantity = quantity,
        AsOf = Timestamp.FromDateTimeOffset(clock.GetUtcNow()),
    };
}
#endregion

#region p103-setup
public static class InventoryServices
{
    public static IServiceCollection AddInventory(
        this IServiceCollection services)
    {
        services.AddGrpc(o => o.MaxReceiveMessageSize = 64 * 1024);
        services.AddSingleton<StockLevels>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }

    public static void MapInventory(this IEndpointRouteBuilder app) =>
        app.MapGrpcService<InventoryService>();

    // In the service that calls Inventory: a typed client from
    // IHttpClientFactory (Problem 101).
    public static IHttpClientBuilder AddInventoryClient(
        this IServiceCollection services, Uri address) =>
        services.AddGrpcClient<Inventory.InventoryClient>(
            o => o.Address = address);
}
#endregion
