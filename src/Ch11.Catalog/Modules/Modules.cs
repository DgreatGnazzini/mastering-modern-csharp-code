using MasteringModernCSharp.Ch11.Modules.Ordering;
using MasteringModernCSharp.Ch11.Modules.Products;

namespace MasteringModernCSharp.Ch11.Modules;

// Problem 110: each module registers its own services and endpoints.
public interface IModule
{
    string Name { get; }
    void AddServices(IServiceCollection services, IConfiguration config);
    void MapEndpoints(IEndpointRouteBuilder group);
}

public static class ModuleHost
{
    private static readonly IModule[] All =
        [new ProductsModule(), new OrderingModule()];

    public static WebApplicationBuilder AddModules(
        this WebApplicationBuilder builder)
    {
        foreach (var module in All)
            module.AddServices(builder.Services, builder.Configuration);
        return builder;
    }

    public static WebApplication MapModules(this WebApplication app)
    {
        var shop = app.MapGroup("/shop");
        foreach (var module in All)
        {
            module.MapEndpoints(shop.MapGroup($"/{module.Name}")
                .WithTags(module.Name));
        }
        return app;
    }
}
