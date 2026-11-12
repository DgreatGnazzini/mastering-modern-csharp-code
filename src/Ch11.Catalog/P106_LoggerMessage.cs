namespace MasteringModernCSharp.Ch11;

// The generator writes the bodies: no boxing, no parsing of the
// template at runtime, and nothing at all when the level is off.
public static partial class Log
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Looked up {Sku} in {ElapsedMs} ms")]
    public static partial void ProductLookedUp(
        this ILogger logger, string sku, double elapsedMs);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning,
        Message = "Product {Sku} not found")]
    public static partial void ProductNotFound(
        this ILogger logger, string sku);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error,
        Message = "Pricing service failed for {Sku}")]
    public static partial void PricingFailed(
        this ILogger logger, Exception exception, string sku);
}
