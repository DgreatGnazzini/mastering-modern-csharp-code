using System.Net;
using System.Net.Http.Json;
using MasteringModernCSharp.Ch12.Orders;

namespace MasteringModernCSharp.Ch12.Tests;

#region p112-tests
public class OrdersApiTests
{
    // Docker is optional on a laptop, but CI always has it.
    public static bool CanRun =>
        Docker.IsAvailable || Environment.GetEnvironmentVariable("CI")
            is "true";

    [Fact(SkipUnless = nameof(CanRun),
          Skip = "Needs Docker for the PostgreSQL container.")]
    public async Task Duplicate_reference_returns_409()
    {
        await using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();
        var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var order = new NewOrder("A-100", "Contoso", 12.50m);

        var first = await client.PostAsJsonAsync("/orders", order, ct);
        var second = await client.PostAsJsonAsync("/orders", order, ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        // The real unique index raised the real Postgres error.
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
#endregion

    [Fact(SkipUnless = nameof(CanRun),
          Skip = "Needs Docker for the PostgreSQL container.")]
    public async Task Search_is_case_insensitive_with_ILIKE()
    {
        await using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();
        var client = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        await client.PostAsJsonAsync(
            "/orders", new NewOrder("B-1", "Northwind Traders", 5m), ct);
        await client.PostAsJsonAsync(
            "/orders", new NewOrder("B-2", "Contoso", 7m), ct);

        var found = await client.GetFromJsonAsync<List<Order>>(
            "/orders?customer=NORTH", ct);

        var only = Assert.Single(found!);
        Assert.Equal("B-1", only.Reference);
    }

    [Fact]
    public void Docker_detection_does_not_throw() =>
        _ = Docker.IsAvailable;
}
