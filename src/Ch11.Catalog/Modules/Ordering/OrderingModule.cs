using System.Collections.Concurrent;

namespace MasteringModernCSharp.Ch11.Modules.Ordering;

public sealed class OrderingModule : IModule
{
    public string Name => "orders";

    public void AddServices(IServiceCollection services,
                            IConfiguration config) =>
        services.AddSingleton<OrderBook>();

    public void MapEndpoints(IEndpointRouteBuilder group)
    {
        PlaceOrder.Map(group);
        GetOrder.Map(group);
    }
}

internal sealed record PlacedOrder(
    Guid Id, string Sku, int Quantity, decimal Total);

internal sealed class OrderBook
{
    private readonly ConcurrentDictionary<Guid, PlacedOrder> _orders =
        new();

    public PlacedOrder Add(string sku, int quantity, decimal total)
    {
        var order = new PlacedOrder(
            Guid.CreateVersion7(), sku, quantity, total);
        _orders[order.Id] = order;
        return order;
    }

    public PlacedOrder? Find(Guid id) => _orders.GetValueOrDefault(id);
}

internal static class GetOrder
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/{id:guid}", (Guid id, OrderBook orders) =>
            orders.Find(id) is { } order
                ? Results.Ok(order)
                : Results.NotFound());
}
