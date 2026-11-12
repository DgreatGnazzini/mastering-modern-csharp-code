using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace MasteringModernCSharp.Ch06;

public record WeatherQuery(
    [property: Description("City name, e.g. Austin")] string City,
    [property: Description("Days ahead, 1 to 7")] int Days,
    string? Units = "metric");

public static class SchemaExport
{
    // Produces a JSON Schema document for any .NET type.
    public static string For<T>()
    {
        // Not the Web defaults: they allow numbers as strings,
        // which turns "integer" into ["string", "integer"].
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            RespectNullableAnnotations = true,
            // The exporter needs a resolver to read metadata from.
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        var schema = options.GetJsonSchemaAsNode(
            typeof(T),
            new JsonSchemaExporterOptions
            {
                // Copy [Description] attributes into the schema.
                TransformSchemaNode = (context, node) =>
                {
                    var description = context.PropertyInfo?
                        .AttributeProvider?
                        .GetCustomAttributes(
                            typeof(DescriptionAttribute), true)
                        .OfType<DescriptionAttribute>()
                        .FirstOrDefault()?.Description;
                    if (description is not null && node is
                        System.Text.Json.Nodes.JsonObject obj)
                    {
                        obj.Insert(0, "description", description);
                    }
                    return node;
                },
            });
        return schema.ToJsonString(
            new JsonSerializerOptions { WriteIndented = true });
    }
}
