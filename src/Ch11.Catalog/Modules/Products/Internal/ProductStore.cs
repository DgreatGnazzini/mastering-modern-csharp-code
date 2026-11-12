using System.Collections.Concurrent;

namespace MasteringModernCSharp.Ch11.Modules.Products.Internal;

// Stands in for the module's own tables.
internal sealed class ProductStore : IProductCatalog
{
    private readonly ConcurrentDictionary<string, ProductInfo> _items =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DESK-1"] = new("DESK-1", "Standing desk", 349m),
            ["LAMP-2"] = new("LAMP-2", "Desk lamp", 29.5m),
        };

    public Task<ProductInfo?> FindAsync(string sku,
                                        CancellationToken ct) =>
        Task.FromResult(_items.GetValueOrDefault(sku));
}

internal static class GetProduct
{
    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/{sku}", async (
            string sku, ProductStore store, CancellationToken ct) =>
            await store.FindAsync(sku, ct) is { } product
                ? Results.Ok(product)
                : Results.NotFound());
}
