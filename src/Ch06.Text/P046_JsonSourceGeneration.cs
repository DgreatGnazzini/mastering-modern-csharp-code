using System.Text.Json;
using System.Text.Json.Serialization;

namespace MasteringModernCSharp.Ch06;

public record Product(string Sku, string Name, decimal Price,
                      IReadOnlyList<string> Tags);

// The source generator writes the serialization code at build time:
// no reflection at runtime, so it's fast and safe for Native AOT.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(Product))]
[JsonSerializable(typeof(List<Product>))]
public partial class CatalogJsonContext : JsonSerializerContext;

public static class CatalogJson
{
    public static string Serialize(Product product) =>
        JsonSerializer.Serialize(
            product, CatalogJsonContext.Default.Product);

    public static List<Product>? DeserializeList(string json) =>
        JsonSerializer.Deserialize(
            json, CatalogJsonContext.Default.ListProduct);
}
