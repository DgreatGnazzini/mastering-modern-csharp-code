using System.Net;
using System.Net.Http.Json;
using MasteringModernCSharp.Ch09;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace MasteringModernCSharp.Ch09.Tests;

public class P081_ApiVersioningTests(ApiFactory factory)
    : ApiTest(factory)
{
    #region p081
    [Fact]
    public async Task Each_version_keeps_its_own_contract()
    {
        var v1 = await Client.GetAsync("/api/v1/customers/7", Ct);
        var v2 = await Client.GetFromJsonAsync<CustomerV2>(
            "/api/v2/customers/7", Ct);

        Assert.Equal("Ada Lovelace",
            (await v1.Content.ReadFromJsonAsync<CustomerV1>(Ct))!.Name);
        Assert.Equal("Lovelace", v2!.FamilyName);
        // Clients are told v1 is deprecated, and when it ends.
        Assert.Equal("1", Header(v1, "api-deprecated-versions"));
        Assert.Equal("2", Header(v1, "api-supported-versions"));
        Assert.Contains("2027", Header(v1, "Sunset"));
    }
    #endregion

    private static string Header(HttpResponseMessage r, string name) =>
        string.Join(",", r.Headers.GetValues(name));

    [Fact]
    public async Task Unknown_version_is_rejected()
    {
        var response = await Client.GetAsync("/api/v3/customers/7", Ct);
        // No route serves v3, so it's a plain 404 ProblemDetails.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public class P087_OpenIdConnectTests
{
    private static CancellationToken Ct =>
        TestContext.Current.CancellationToken;

    // A web app using AddOidcSignIn, with the identity provider's
    // metadata supplied directly instead of downloaded.
    private static async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Oidc:Authority"] = "https://login.example",
                ["Oidc:ClientId"] = "shop-web",
                ["Oidc:ClientSecret"] = "dev-secret",
            });
        builder.Services.AddOidcSignIn(builder.Configuration);
        builder.Services.PostConfigure<OpenIdConnectOptions>(
            OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = "https://login.example",
                    AuthorizationEndpoint =
                        "https://login.example/authorize",
                    TokenEndpoint = "https://login.example/token",
                };
                options.ConfigurationManager =
                    new StaticConfigurationManager<
                        OpenIdConnectConfiguration>(options.Configuration);
            });
        builder.Services.AddAuthorization();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAccount();
        await app.StartAsync(Ct);
        return app;
    }

    private static HttpClient NoRedirects(WebApplication app)
    {
        var client = app.GetTestServer().CreateClient();
        client.BaseAddress = new Uri("https://shop.example");
        return client;
    }

    #region p087
    [Fact]
    public async Task Login_redirects_to_the_provider_with_PKCE()
    {
        await using var app = await StartAppAsync();

        var response = await NoRedirects(app).GetAsync(
            "/account/login?returnUrl=/orders", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith("https://login.example/authorize?", location);
        Assert.Contains("response_type=code", location);
        Assert.Contains("code_challenge_method=S256", location);
        Assert.Contains("client_id=shop-web", location);
        Assert.Contains("orders.read", location);
    }
    #endregion

    [Fact]
    public async Task Protected_page_challenges_anonymous_users()
    {
        await using var app = await StartAppAsync();

        var response = await NoRedirects(app).GetAsync("/account/me", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://login.example/authorize",
            response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task External_return_urls_are_ignored()
    {
        await using var app = await StartAppAsync();

        var response = await NoRedirects(app).GetAsync(
            "/account/login?returnUrl=//evil.example", Ct);

        // The state parameter carries the redirect; it must not
        // contain the attacker's host.
        Assert.DoesNotContain("evil.example",
            Uri.UnescapeDataString(response.Headers.Location!.Query));
    }

    // Fake token endpoint: counts requests, issues 1-hour tokens.
    private sealed class FakeTokenEndpoint : HttpMessageHandler
    {
        public int Requests;
        public string? LastBody;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var n = Interlocked.Increment(ref Requests);
            LastBody = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new Dictionary<string, object>
                {
                    ["access_token"] = $"token-{n}",
                    ["expires_in"] = 3600,
                    ["token_type"] = "Bearer",
                }),
            };
        }
    }

    [Fact]
    public async Task Client_credentials_token_is_cached_until_expiry()
    {
        var endpoint = new FakeTokenEndpoint();
        var clock = new FakeTimeProvider();
        var cache = new TokenCache(new HttpClient(endpoint),
            new ClientCredentials
            {
                TokenEndpoint = new Uri("https://login.example/token"),
                ClientId = "orders-worker",
                ClientSecret = "s3cret",
                Scope = "inventory.write",
            }, clock);

        Assert.Equal("token-1", await cache.GetAsync(Ct));
        Assert.Equal("token-1", await cache.GetAsync(Ct));
        Assert.Contains("grant_type=client_credentials",
                        endpoint.LastBody);

        clock.Advance(TimeSpan.FromMinutes(59));   // inside the margin
        Assert.Equal("token-2", await cache.GetAsync(Ct));
        Assert.Equal(2, endpoint.Requests);
    }

    [Fact]
    public async Task Bearer_handler_adds_the_token()
    {
        var cache = new TokenCache(new HttpClient(new FakeTokenEndpoint()),
            new ClientCredentials
            {
                TokenEndpoint = new Uri("https://login.example/token"),
                ClientId = "c", ClientSecret = "s", Scope = "x",
            }, TimeProvider.System);
        HttpRequestMessage? seen = null;
        var api = new HttpClient(new BearerTokenHandler(cache)
        {
            InnerHandler = new Inspect(r => seen = r),
        });

        await api.GetAsync("https://inventory.example/stock", Ct);

        Assert.Equal("Bearer token-1",
                     seen?.Headers.Authorization?.ToString());
    }

    private sealed class Inspect(Action<HttpRequestMessage> see)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            see(request);
            return Task.FromResult(new HttpResponseMessage());
        }
    }
}
