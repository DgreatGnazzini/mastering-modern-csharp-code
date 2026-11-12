using System.Net;
using System.Net.Http.Json;
using Grpc.Core;
using Grpc.Net.Client;
using MasteringModernCSharp.Ch11.Stock;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using NetArchTest.Rules;

namespace MasteringModernCSharp.Ch11.Tests;

public class P103_GrpcTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private static CancellationToken Ct =>
        TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private StockLevels Stock =>
        _factory.Services.GetRequiredService<StockLevels>();

    #region p103-client
    private Inventory.InventoryClient Client()
    {
        // In production: GrpcChannel.ForAddress("https://inventory").
        // Here the channel talks to the in-memory test server.
        var channel = GrpcChannel.ForAddress(_factory.Server.BaseAddress,
            new GrpcChannelOptions
            {
                HttpHandler = _factory.Server.CreateHandler(),
            });
        return new Inventory.InventoryClient(channel);
    }

    [Fact]
    public async Task Unary_call_returns_the_stock_level()
    {
        var client = Client();
        Stock.Set("DESK-1", 12);

        var reply = await client.GetStockAsync(
            new StockRequest { Sku = "DESK-1" },
            deadline: DateTime.UtcNow.AddSeconds(5),
            cancellationToken: Ct);

        Assert.Equal(12, reply.Quantity);
    }

    [Fact]
    public async Task Unknown_sku_is_a_NotFound_status()
    {
        var client = Client();

        var ex = await Assert.ThrowsAsync<RpcException>(async () =>
            await client.GetStockAsync(
                new StockRequest { Sku = "NOPE" },
                cancellationToken: Ct));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }
    #endregion

    #region p103-stream
    [Fact]
    public async Task Stream_sends_current_level_then_changes()
    {
        var client = Client();
        Stock.Set("LAMP-2", 3);
        using var cts =
            CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        using var call = client.WatchStock(
            new WatchRequest { Skus = { "LAMP-2" } },
            cancellationToken: cts.Token);
        var received = new List<int>();
        await foreach (var reply in
                       call.ResponseStream.ReadAllAsync(cts.Token))
        {
            received.Add(reply.Quantity);
            if (received.Count == 1)
            {
                Stock.Set("DESK-1", 50);    // not watched: not sent
                Stock.Set("LAMP-2", 2);
            }
            if (received.Count == 2) break;
        }

        Assert.Equal([3, 2], received);
    }
    #endregion
}

public class P104_SignalRTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private static CancellationToken Ct =>
        TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    #region p104-client
    private HubConnection Connect() =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/orders"),
                o =>
                {
                    // Route the connection through the test server.
                    o.Transports = HttpTransportType.WebSockets;
                    o.HttpMessageHandlerFactory =
                        _ => _factory.Server.CreateHandler();
                    o.WebSocketFactory = (context, ct) =>
                        new ValueTask<System.Net.WebSockets.WebSocket>(
                            _factory.Server.CreateWebSocketClient()
                                .ConnectAsync(context.Uri, ct));
                })
            .WithAutomaticReconnect()
            .Build();

    [Fact]
    public async Task Followers_of_an_order_get_its_updates()
    {
        await using var following = Connect();
        await using var other = Connect();
        var got = new TaskCompletionSource<string>();
        var otherGot = 0;
        following.On<string, string>("StatusChanged",
            (id, status) => got.TrySetResult($"{id}:{status}"));
        other.On<string, string>("StatusChanged",
            (_, _) => Interlocked.Increment(ref otherGot));

        await following.StartAsync(Ct);
        await other.StartAsync(Ct);
        await following.InvokeAsync("Follow", "42", Ct);
        await other.InvokeAsync("Follow", "7", Ct);

        var http = _factory.CreateClient();
        var response = await http.PostAsJsonAsync("/orders/42/status",
            new StatusUpdate("Shipped"), Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("42:Shipped",
            await got.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        Assert.Equal(0, otherGot);
    }
    #endregion

    [Fact]
    public async Task Unfollow_stops_updates()
    {
        await using var connection = Connect();
        var count = 0;
        connection.On<string, string>("StatusChanged",
            (_, _) => Interlocked.Increment(ref count));
        await connection.StartAsync(Ct);
        await connection.InvokeAsync("Follow", "9", Ct);
        await connection.InvokeAsync("Unfollow", "9", Ct);

        var notifier =
            _factory.Services.GetRequiredService<OrderNotifier>();
        await notifier.StatusChangedAsync("9", "Packed");
        // A round trip on the same connection: anything sent before
        // it has arrived by the time it completes.
        await connection.InvokeAsync("Follow", "10", Ct);

        Assert.Equal(0, count);
    }
}

public class P110_ModularMonolithTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private static CancellationToken Ct =>
        TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private sealed record Placed(Guid OrderId, decimal Total);

    [Fact]
    public async Task Order_slice_uses_the_product_contract()
    {
        var http = _factory.CreateClient();

        var response = await http.PostAsJsonAsync("/shop/orders",
            new { sku = "LAMP-2", quantity = 2 }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var placed = await response.Content
            .ReadFromJsonAsync<Placed>(Ct);
        Assert.Equal(59m, placed!.Total);
        var get = await http.GetAsync(response.Headers.Location, Ct);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Order_slice_validates_and_reports_unknown_skus()
    {
        var http = _factory.CreateClient();

        var invalid = await http.PostAsJsonAsync("/shop/orders",
            new { sku = "LAMP-2", quantity = 0 }, Ct);
        var unknown = await http.PostAsJsonAsync("/shop/orders",
            new { sku = "NOPE", quantity = 1 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    #region p110-arch
    private const string Root = "MasteringModernCSharp.Ch11.Modules";

    [Fact]
    public void Ordering_uses_only_the_products_contract()
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .That().ResideInNamespace($"{Root}.Ordering")
            .Should().NotHaveDependencyOnAny($"{Root}.Products.Internal")
            .GetResult();

        Assert.NotEmpty(result.SelectedTypesForTesting);
        Assert.True(result.IsSuccessful, string.Join(", ",
            result.FailingTypes.Select(t => t.FullName)));
    }

    [Fact]
    public void Products_knows_nothing_about_ordering()
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .That().ResideInNamespace($"{Root}.Products")
            .Should().NotHaveDependencyOnAny($"{Root}.Ordering")
            .GetResult();

        Assert.NotEmpty(result.SelectedTypesForTesting);
        Assert.True(result.IsSuccessful, string.Join(", ",
            result.FailingTypes.Select(t => t.FullName)));
    }
    #endregion
}
