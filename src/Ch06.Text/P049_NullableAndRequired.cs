using System.Text.Json;

namespace MasteringModernCSharp.Ch06;

public class SignupForm
{
    public required string Email { get; init; }
    public string DisplayName { get; init; } = "";
    public string? Referrer { get; init; }
}

public static class SignupJson
{
    private static readonly JsonSerializerOptions Options =
        new(JsonSerializerDefaults.Web)
        {
            // Honor `string` vs `string?` when reading and writing.
            RespectNullableAnnotations = true,
            // Treat non-optional constructor parameters as required.
            RespectRequiredConstructorParameters = true,
        };

    public static SignupForm Parse(string json) =>
        JsonSerializer.Deserialize<SignupForm>(json, Options)
            ?? throw new JsonException("Body was null.");
}
