using Microsoft.AspNetCore.SignalR;

namespace MasteringModernCSharp.Ch11;

#region p104-hub
// The methods the server can call on a connected client.
public interface IOrderClient
{
    Task StatusChanged(string orderId, string status);
}

// Methods a client can call on the server.
public sealed class OrderHub : Hub<IOrderClient>
{
    public Task Follow(string orderId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, Group(orderId));

    public Task Unfollow(string orderId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(orderId));

    public static string Group(string orderId) => $"order:{orderId}";
}
#endregion

#region p104-notify
public record StatusUpdate(string Status);

// Pushes from anywhere in the app, outside the hub.
public sealed class OrderNotifier(IHubContext<OrderHub, IOrderClient> hub)
{
    public Task StatusChangedAsync(string orderId, string status) =>
        hub.Clients.Group(OrderHub.Group(orderId))
            .StatusChanged(orderId, status);
}

public static class OrderUpdates
{
    public static IServiceCollection AddOrderUpdates(
        this IServiceCollection services)
    {
        services.AddSignalR(o =>
        {
            o.MaximumReceiveMessageSize = 32 * 1024;
            o.KeepAliveInterval = TimeSpan.FromSeconds(15);
        });
        services.AddSingleton<OrderNotifier>();
        return services;
    }

    public static void MapOrderUpdates(this IEndpointRouteBuilder app)
    {
        app.MapHub<OrderHub>("/hubs/orders");

        app.MapPost("/orders/{id}/status", async (
            string id, StatusUpdate update, OrderNotifier notifier) =>
        {
            // ...save the new status, then tell the followers.
            await notifier.StatusChangedAsync(id, update.Status);
            return Results.NoContent();
        });
    }
}
#endregion
