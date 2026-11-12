using DotNet.Testcontainers.Configurations;
using MasteringModernCSharp.Ch12.Orders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace MasteringModernCSharp.Ch12.Tests;

#region p112-factory
// The real API, backed by a real PostgreSQL in a throwaway container.
public sealed class PostgresApiFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();   // pulls the image if needed
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDb>();
        await db.Database.EnsureCreatedAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        // Overrides appsettings.json: point the app at the container.
        builder.UseSetting("ConnectionStrings:orders",
                           _postgres.GetConnectionString());

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();  // removes the container
    }
}
#endregion

public static class Docker
{
    public static bool IsAvailable { get; } = Detect();

    private static bool Detect()
    {
        try
        {
            return TestcontainersSettings.OS
                .DockerEndpointAuthConfig is not null;
        }
        catch
        {
            return false;
        }
    }
}
