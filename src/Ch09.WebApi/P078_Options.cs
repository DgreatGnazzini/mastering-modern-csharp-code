using Microsoft.Extensions.Options;

namespace MasteringModernCSharp.Ch09;

public sealed class ShippingOptions
{
    public const string Section = "Shipping";
    public decimal FreeShippingOver { get; set; } = 50m;
    public decimal FlatRate { get; set; } = 4.99m;
}

public sealed class CarrierOptions
{
    public string TrackingUrl { get; set; } = "";
}

public static class ShippingSetup
{
    public static IServiceCollection AddShipping(
        this IServiceCollection services)
    {
        services.AddOptions<ShippingOptions>()
            .BindConfiguration(ShippingOptions.Section);
        // Named options: one class, several configurations.
        services.AddOptions<CarrierOptions>("ups")
            .BindConfiguration("Carriers:Ups");
        services.AddOptions<CarrierOptions>("dhl")
            .BindConfiguration("Carriers:Dhl");
        services.AddScoped<ShippingQuote>();
        services.AddSingleton<CarrierLinks>();
        return services;
    }
}

// Scoped: IOptionsSnapshot reads the current configuration once
// per request, so a request never sees a value change midway.
public sealed class ShippingQuote(
    IOptionsSnapshot<ShippingOptions> options)
{
    private readonly ShippingOptions _rules = options.Value;

    public decimal For(decimal orderTotal) =>
        orderTotal >= _rules.FreeShippingOver ? 0m : _rules.FlatRate;
}

// Singleton: IOptionsMonitor always has the latest values and
// can notify you when they change.
public sealed class CarrierLinks : IDisposable
{
    private readonly IOptionsMonitor<CarrierOptions> _carriers;
    private readonly IDisposable? _subscription;

    public CarrierLinks(IOptionsMonitor<CarrierOptions> carriers)
    {
        _carriers = carriers;
        _subscription = carriers.OnChange((_, name) => Changes++);
    }

    public int Changes { get; private set; }

    public string Track(string carrier, string parcel) =>
        string.Format(_carriers.Get(carrier).TrackingUrl, parcel);

    public void Dispose() => _subscription?.Dispose();
}
