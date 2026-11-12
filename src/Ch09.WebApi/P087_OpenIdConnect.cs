using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace MasteringModernCSharp.Ch09;

public static class OidcSignIn
{
    // A server-rendered web app (or a BFF in front of a SPA):
    // users sign in at the identity provider, the app keeps a cookie.
    public static AuthenticationBuilder AddOidcSignIn(
        this IServiceCollection services, IConfiguration config) =>
        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme =
                    CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme =
                    OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
            })
            .AddOpenIdConnect(options =>
            {
                options.Authority = config["Oidc:Authority"];
                options.ClientId = config["Oidc:ClientId"];
                options.ClientSecret = config["Oidc:ClientSecret"];
                options.ResponseType = "code";   // authorization code
                options.UsePkce = true;          // the default; explicit
                options.Scope.Add("orders.read");
                options.SaveTokens = true;       // keep the access token
                options.MapInboundClaims = false; // keep "sub", "name"
                options.TokenValidationParameters.NameClaimType = "name";
            });

    public static void MapAccount(this IEndpointRouteBuilder app)
    {
        app.MapGet("/account/login", (string? returnUrl) =>
            Results.Challenge(new AuthenticationProperties
            {
                // Only local paths: an open redirect is a phishing aid.
                RedirectUri = returnUrl is ['/', not '/', ..]
                    ? returnUrl : "/",
            }));

        app.MapGet("/account/me", (HttpContext http) =>
            new { Name = http.User.Identity?.Name })
            .RequireAuthorization();

        app.MapPost("/account/logout", () => Results.SignOut(
            authenticationSchemes:
            [
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme,
            ]));
    }
}

// Service-to-service calls: no user, so the service authenticates
// itself with the OAuth 2.0 client credentials grant.
public sealed class ClientCredentials
{
    public required Uri TokenEndpoint { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string Scope { get; init; }
}

public sealed class TokenCache(
    HttpClient http, ClientCredentials client, TimeProvider clock)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expires;

    public async Task<string> GetAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // Renew a minute early so a token never expires in flight.
            if (_token is null || clock.GetUtcNow() >= _expires)
            {
                var reply = await RequestAsync(ct);
                _token = reply.AccessToken;
                _expires = clock.GetUtcNow()
                    .AddSeconds(reply.ExpiresIn - 60);
            }
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<TokenReply> RequestAsync(CancellationToken ct)
    {
        using var response = await http.PostAsync(client.TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = client.ClientId,
                ["client_secret"] = client.ClientSecret,
                ["scope"] = client.Scope,
            }), ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content
            .ReadFromJsonAsync<TokenReply>(ct))!;
    }

    private sealed record TokenReply(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

// Adds the cached token to every request of a typed client.
public sealed class BearerTokenHandler(TokenCache tokens)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", await tokens.GetAsync(ct));
        return await base.SendAsync(request, ct);
    }
}
