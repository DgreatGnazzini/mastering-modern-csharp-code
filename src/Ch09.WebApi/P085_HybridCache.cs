using Microsoft.Extensions.Caching.Hybrid;

namespace MasteringModernCSharp.Ch09;

public record Product(int Id, string Name, decimal Price);

public sealed class ProductRepository
{
    private int _loads;
    public int Loads => _loads;

    // Stands in for a slow database query.
    public async Task<Product> LoadAsync(int id, CancellationToken ct)
    {
        Interlocked.Increment(ref _loads);
        await Task.Delay(50, ct);
        return new Product(id, $"Product {id}", 10m + id);
    }
}

public static class ProductEndpoints
{
    public static void MapProducts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products/{id:int}", async (
            int id, HybridCache cache, ProductRepository repo,
            CancellationToken ct) =>
            // Memory first, then the distributed cache if one is
            // registered, then the factory. Concurrent misses for
            // the same key share one factory call.
            await cache.GetOrCreateAsync(
                $"product:{id}",
                async token => await repo.LoadAsync(id, token),
                tags: ["products"],
                cancellationToken: ct));

        app.MapPost("/products/invalidate", async (
            HybridCache cache, CancellationToken ct) =>
        {
            await cache.RemoveByTagAsync("products", ct);
            return TypedResults.NoContent();
        });
    }
}
