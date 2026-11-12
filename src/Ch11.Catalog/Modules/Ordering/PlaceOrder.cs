using MasteringModernCSharp.Ch11.Modules.Products;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MasteringModernCSharp.Ch11.Modules.Ordering;

// One feature, one file: the request, the response, the rules
// and the endpoint for "place an order".
internal static class PlaceOrder
{
    public record Request(string Sku, int Quantity);
    public record Response(Guid OrderId, decimal Total);

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/", HandleAsync);

    private static async Task<Results<Created<Response>,
        ValidationProblem, NotFound>> HandleAsync(
        Request request, IProductCatalog products, OrderBook orders,
        CancellationToken ct)
    {
        if (request.Quantity is < 1 or > 100)
        {
            return TypedResults.ValidationProblem(new Dictionary<
                string, string[]>
            {
                ["quantity"] = ["Order between 1 and 100 items."],
            });
        }

        // Another module, through its contract only.
        var product = await products.FindAsync(request.Sku, ct);
        if (product is null) return TypedResults.NotFound();

        var order = orders.Add(product.Sku, request.Quantity,
                               product.Price * request.Quantity);
        return TypedResults.Created($"/shop/orders/{order.Id}",
            new Response(order.Id, order.Total));
    }
}
