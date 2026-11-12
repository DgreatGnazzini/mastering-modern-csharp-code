using System.Text.Json;
using System.Text.Json.Serialization;

namespace MasteringModernCSharp.Ch06;

public record TransferRequest(string FromAccount, string ToAccount,
                              decimal Amount);

public static class StrictJson
{
    // .NET 10: one preset that rejects unknown properties,
    // duplicate properties and nulls in non-nullable members,
    // and matches property names case-sensitively.
    private static readonly JsonSerializerOptions Strict =
        new(JsonSerializerOptions.Strict)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

    // Or turn on just the duplicate check.
    private static readonly JsonSerializerOptions NoDuplicates =
        new(JsonSerializerDefaults.Web)
        {
            AllowDuplicateProperties = false,
        };

    public static TransferRequest? ParseStrict(string json) =>
        JsonSerializer.Deserialize<TransferRequest>(json, Strict);

    public static TransferRequest? ParseNoDuplicates(string json) =>
        JsonSerializer.Deserialize<TransferRequest>(json, NoDuplicates);
}
