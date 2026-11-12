namespace MasteringModernCSharp.Ch09;

public interface IPaymentGateway
{
    Task<string> ChargeAsync(decimal amount, CancellationToken ct);
}

public sealed class CardGateway : IPaymentGateway
{
    public Task<string> ChargeAsync(
        decimal amount, CancellationToken ct) =>
        Task.FromResult($"card:{amount:0.00}");
}

public sealed class PayPalGateway : IPaymentGateway
{
    public Task<string> ChargeAsync(
        decimal amount, CancellationToken ct) =>
        Task.FromResult($"paypal:{amount:0.00}");
}

// One per HTTP request.
public sealed class RequestInfo
{
    public Guid Id { get; } = Guid.NewGuid();
}

// Scoped, so it may depend on scoped and singleton services.
public sealed class Checkout(
    [FromKeyedServices("card")] IPaymentGateway defaultGateway,
    RequestInfo request)
{
    public async Task<string> PayAsync(
        decimal amount, CancellationToken ct) =>
        $"{request.Id}:{await defaultGateway.ChargeAsync(amount, ct)}";
}

// A singleton must not hold a scoped service. To use one, it
// creates a scope for each unit of work.
public sealed class AuditTrail(IServiceScopeFactory scopes)
{
    public Guid Record()
    {
        using var scope = scopes.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<RequestInfo>().Id;
    }
}

public static class PaymentRegistration
{
    public static IServiceCollection AddPayments(
        this IServiceCollection services)
    {
        // Stateless and thread-safe: one instance for the app.
        services.AddKeyedSingleton<IPaymentGateway, CardGateway>("card");
        services.AddKeyedSingleton<IPaymentGateway, PayPalGateway>(
            "paypal");
        services.AddScoped<RequestInfo>();
        services.AddScoped<Checkout>();
        services.AddSingleton<AuditTrail>();
        return services;
    }

    public static void MapPayments(this IEndpointRouteBuilder app)
    {
        app.MapPost("/checkout", (decimal amount, Checkout checkout,
                                  CancellationToken ct) =>
            checkout.PayAsync(amount, ct));

        // Choose an implementation by key at run time.
        app.MapPost("/pay/{method}", async (string method,
            decimal amount, HttpContext http, CancellationToken ct) =>
            http.RequestServices.GetKeyedService<IPaymentGateway>(method)
                is { } gateway
                ? Results.Ok(await gateway.ChargeAsync(amount, ct))
                : Results.BadRequest($"Unknown payment method {method}."));
    }
}
