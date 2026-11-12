using System.ComponentModel.DataAnnotations;

namespace MasteringModernCSharp.Ch09;

public record Address(
    [Required] string Street,
    [Required, StringLength(10, MinimumLength = 3)] string PostCode);

public record SignupRequest(
    [Required, EmailAddress] string Email,
    [Range(13, 120)] int Age,
    [Required] Address Address) : IValidatableObject
{
    // Rules that span several properties.
    public IEnumerable<ValidationResult> Validate(
        ValidationContext context)
    {
        if (Email.EndsWith("@example.com",
                StringComparison.OrdinalIgnoreCase))
        {
            yield return new("Example addresses aren't allowed.",
                [nameof(Email)]);
        }
    }
}

public static class SignupEndpoints
{
    // With AddValidation() registered, invalid bodies never reach
    // the handler: the framework answers 400 with the errors.
    public static void MapSignup(this IEndpointRouteBuilder app) =>
        app.MapPost("/signup", (SignupRequest request) =>
            TypedResults.Ok(new { Welcome = request.Email }));
}
