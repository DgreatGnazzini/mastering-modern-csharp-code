using MasteringModernCSharp.Ch11.Modules.Products.Internal;

namespace MasteringModernCSharp.Ch11.Modules.Products;

// The module's contract: the only types other modules may use.
public interface IProductCatalog
{
    Task<ProductInfo?> FindAsync(string sku, CancellationToken ct);
}

public record ProductInfo(string Sku, string Name, decimal Price);

public sealed class ProductsModule : IModule
{
    public string Name => "products";

    public void AddServices(IServiceCollection services,
                            IConfiguration config)
    {
        services.AddSingleton<ProductStore>();
        services.AddSingleton<IProductCatalog>(
            sp => sp.GetRequiredService<ProductStore>());
    }

    public void MapEndpoints(IEndpointRouteBuilder group) =>
        GetProduct.Map(group);
}
