using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MasteringModernCSharp.Ch09;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "https://login.example";
    public string Audience { get; set; } = "shop-api";
    // Demo only. Real apps validate tokens from an identity
    // provider using its published public keys instead.
    public string SigningKey { get; set; } = "";
}

public static class JwtAuth
{
    public static IServiceCollection AddJwtAuth(
        this IServiceCollection services, JwtSettings jwt)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false; // keep "sub", "scope"
                options.TokenValidationParameters = new()
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = Key(jwt),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("orders:write", policy => policy
                .AddAuthenticationSchemes(
                    JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx =>
                    HasScope(ctx.User, "orders:write")));
        return services;
    }

    // "scope" is one space-separated string, e.g. "orders:read
    // orders:write", so RequireClaim("scope", ...) would miss it.
    private static bool HasScope(ClaimsPrincipal user, string scope) =>
        user.FindAll("scope")
            .SelectMany(c => c.Value.Split(' '))
            .Contains(scope);

    public static void MapOrderWrites(this IEndpointRouteBuilder app) =>
        app.MapPost("/orders", (ClaimsPrincipal user) =>
                TypedResults.Ok(
                    new { placedBy = user.FindFirstValue("sub") }))
            .RequireAuthorization("orders:write");

    public static SymmetricSecurityKey Key(JwtSettings jwt) =>
        new(Encoding.UTF8.GetBytes(jwt.SigningKey));

    // Issues a token the way an identity provider would; used by
    // the tests and for local experiments.
    public static string CreateToken(
        JwtSettings jwt, string subject, string scopes,
        TimeSpan lifetime) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Expires = DateTime.UtcNow + lifetime,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = subject,
                ["scope"] = scopes,
            },
            SigningCredentials = new SigningCredentials(
                Key(jwt), SecurityAlgorithms.HmacSha256),
        });
}
