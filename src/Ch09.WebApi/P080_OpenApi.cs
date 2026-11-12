using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace MasteringModernCSharp.Ch09;

public static class ApiDocs
{
    public static IServiceCollection AddApiDocs(
        this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            // Edit the generated document before it's served.
            options.AddDocumentTransformer((document, context, ct) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Shop API",
                    Version = "v1",
                    Description = "Sample API for Mastering Modern C#.",
                };
                return Task.CompletedTask;
            });
        });

    public static void MapApiDocs(this WebApplication app)
    {
        // The document: /openapi/v1.json (OpenAPI 3.1 by default).
        app.MapOpenApi();

        // An interactive explorer at /scalar, for development only.
        if (app.Environment.IsDevelopment())
        {
            app.MapScalarApiReference();
        }
    }
}
