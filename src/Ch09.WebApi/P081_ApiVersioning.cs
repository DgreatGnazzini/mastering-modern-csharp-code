using Asp.Versioning;

namespace MasteringModernCSharp.Ch09;

public record CustomerV1(int Id, string Name);
public record CustomerV2(int Id, string GivenName, string FamilyName);

public static class VersionedCustomers
{
    public static IServiceCollection AddVersioning(
        this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1);
            // Adds api-supported-versions and api-deprecated-versions
            // headers, so clients learn about new versions.
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
            // Tells clients when v1 stops working, and where to read.
            options.Policies.Sunset(1)
                .Effective(new DateTimeOffset(2027, 6, 30, 0, 0, 0,
                                              TimeSpan.Zero))
                .Link("https://shop.example/docs/migrate-to-v2")
                    .Title("Migrating to v2");
        });
        return services;
    }

    public static void MapCustomers(this IEndpointRouteBuilder app)
    {
        var customers = app.NewVersionedApi("Customers");
        const string route = "/api/v{version:apiVersion}/customers";

        // v1: still served, but marked deprecated.
        customers.MapGroup(route).HasDeprecatedApiVersion(1)
            .MapGet("/{id:int}", (int id) =>
                new CustomerV1(id, "Ada Lovelace"));

        // v2: the name is split. A breaking change, so a new version.
        customers.MapGroup(route).HasApiVersion(2)
            .MapGet("/{id:int}", (int id) =>
                new CustomerV2(id, "Ada", "Lovelace"));
    }
}
