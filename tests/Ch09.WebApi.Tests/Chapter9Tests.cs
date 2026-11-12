using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MasteringModernCSharp.Ch09;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace MasteringModernCSharp.Ch09.Tests;

// Runs the real app in memory. Production mode, so error
// responses look the way real clients would see them.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Production");
}

public abstract class ApiTest : IClassFixture<ApiFactory>
{
    protected ApiTest(ApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected ApiFactory Factory { get; }
    protected HttpClient Client { get; }
    protected static CancellationToken Ct =>
        TestContext.Current.CancellationToken;

    protected static async Task<JsonElement> JsonAsync(
        HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct))
            .RootElement;
}

public class P076_RouteGroupTests(ApiFactory f) : ApiTest(f)
{
    [Fact]
    public async Task Create_get_and_delete()
    {
        var created = await Client.PostAsJsonAsync("/todos",
            new NewTodo("Write chapter 9"), Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var location = created.Headers.Location!.ToString();
        Assert.Matches("/todos/\\d+$", location);

        var todo = await Client.GetFromJsonAsync<Todo>(location, Ct);
        Assert.Equal("Write chapter 9", todo!.Title);

        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync(location, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync(location, Ct)).StatusCode);
    }
}

public class P079_ValidationTests(ApiFactory f) : ApiTest(f)
{
    [Fact]
    public async Task Invalid_body_gets_400_with_every_error()
    {
        var response = await Client.PostAsJsonAsync("/signup", new
        {
            email = "not-an-email",
            age = 7,
            address = new { street = "1 Main St", postCode = "1" },
        }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await JsonAsync(response)).GetProperty("errors");
        var keys = errors.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains("Email", keys);
        Assert.Contains("Age", keys);
        Assert.Contains("Address.PostCode", keys);
    }

    [Fact]
    public async Task Cross_field_rule_runs_too()
    {
        var response = await Client.PostAsJsonAsync("/signup", new
        {
            email = "ada@example.com",
            age = 36,
            address = new { street = "1 Main St", postCode = "12345" },
        }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Valid_body_reaches_the_handler()
    {
        var response = await Client.PostAsJsonAsync("/signup", new
        {
            email = "ada@lovelace.dev",
            age = 36,
            address = new { street = "1 Main St", postCode = "12345" },
        }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public class P080_OpenApiTests(ApiFactory f) : ApiTest(f)
{
    [Fact]
    public async Task Serves_an_OpenAPI_3_1_document()
    {
        var doc = await JsonAsync(
            await Client.GetAsync("/openapi/v1.json", Ct));
        Assert.StartsWith("3.1", doc.GetProperty("openapi").GetString());
        Assert.Equal("Shop API",
            doc.GetProperty("info").GetProperty("title").GetString());
        Assert.True(doc.GetProperty("paths").TryGetProperty("/todos/{id}", out _));
    }
}

public class P082_ServerSentEventsTests(ApiFactory f) : ApiTest(f)
{
    [Fact]
    public async Task Streams_named_events()
    {
        var response = await Client.GetAsync("/prices/MSFT?count=3", Ct);
        Assert.Equal("text/event-stream",
            response.Content.Headers.ContentType!.MediaType);

        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Equal(3, body.Split("event: tick").Length - 1);
        Assert.Contains("id: 2", body);
        Assert.Contains("\"symbol\":\"MSFT\"", body);
    }
}

public class P083_ProblemDetailsTests(ApiFactory f) : ApiTest(f)
{
    [Theory]
    [InlineData("GET", "/orders/7", 404)]
    [InlineData("POST", "/orders/7/cancel", 409)]
    public async Task Domain_errors_become_problem_details(
        string method, string url, int status)
    {
        var response = await Client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), url), Ct);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType!.MediaType);
        var problem = await JsonAsync(response);
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Unexpected_errors_hide_internals()
    {
        var response = await Client.GetAsync("/boom", Ct);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Secret internals",
            await response.Content.ReadAsStringAsync(Ct));
    }
}

public class P084_ProtectionTests(ApiFactory f) : ApiTest(f)
{
    [Fact]
    public async Task Sixth_request_in_the_window_is_rejected()
    {
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            codes.Add((await Client.GetAsync("/search?q=x", Ct)).StatusCode);
        }
        Assert.All(codes[..5], c => Assert.Equal(HttpStatusCode.OK, c));
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[5]);
    }

    [Fact]
    public async Task Reports_are_cached_per_region()
    {
        var eu1 = await Client.GetStringAsync("/reports/sales?region=eu", Ct);
        await Task.Delay(20, Ct);
        var eu2 = await Client.GetStringAsync("/reports/sales?region=eu", Ct);
        var us = await Client.GetStringAsync("/reports/sales?region=us", Ct);
        Assert.Equal(eu1, eu2);
        Assert.NotEqual(eu1, us);
    }
}

public class P085_HybridCacheTests(ApiFactory f) : ApiTest(f)
{
    [Fact]
    public async Task Loads_once_until_invalidated()
    {
        var repo = Factory.Services.GetRequiredService<ProductRepository>();
        var before = repo.Loads;

        await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => Client.GetStringAsync("/products/7", Ct)));
        Assert.Equal(before + 1, repo.Loads);

        await Client.PostAsync("/products/invalidate", null, Ct);
        await Client.GetStringAsync("/products/7", Ct);
        Assert.Equal(before + 2, repo.Loads);
    }
}

public class P086_JwtTests(ApiFactory f) : ApiTest(f)
{
    private async Task<HttpStatusCode> PostOrderAs(string? scopes)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/orders");
        if (scopes is not null)
        {
            var jwt = Factory.Services.GetRequiredService<JwtSettings>();
            var token = JwtAuth.CreateToken(jwt, "user-1", scopes,
                TimeSpan.FromMinutes(5));
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
        return (await Client.SendAsync(request, Ct)).StatusCode;
    }

    [Fact]
    public async Task No_token_is_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await PostOrderAs(null));

    [Fact]
    public async Task Missing_scope_is_403() =>
        Assert.Equal(HttpStatusCode.Forbidden, await PostOrderAs("orders:read"));

    [Fact]
    public async Task Right_scope_is_allowed() =>
        Assert.Equal(HttpStatusCode.OK,
            await PostOrderAs("orders:read orders:write"));
}

public class P088_PasskeyTests(ApiFactory f) : ApiTest(f)
{
    [Fact]
    public async Task Registration_options_follow_WebAuthn()
    {
        var response = await Client.PostAsJsonAsync("/passkeys/register/options",
            new PasskeyStart("ada@lovelace.dev"), Ct);
        var options = await JsonAsync(response);
        Assert.False(string.IsNullOrEmpty(
            options.GetProperty("challenge").GetString()));
        Assert.Equal("ada@lovelace.dev",
            options.GetProperty("user").GetProperty("name").GetString());
        Assert.True(options.TryGetProperty("rp", out _));
    }

    [Fact]
    public async Task Login_options_have_a_challenge()
    {
        var options = await JsonAsync(
            await Client.PostAsync("/passkeys/login/options", null, Ct));
        Assert.False(string.IsNullOrEmpty(
            options.GetProperty("challenge").GetString()));
    }

    [Fact]
    public async Task Bogus_credential_is_rejected()
    {
        await Client.PostAsJsonAsync("/passkeys/register/options",
            new PasskeyStart("eve@example.dev"), Ct);
        var response = await Client.PostAsync("/passkeys/register",
            new StringContent("{}"), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static UserPasskeyInfo Passkey(byte id) => new(
        credentialId: [id, 1, 2, 3], publicKey: [9, 9, 9],
        createdAt: DateTimeOffset.UtcNow, signCount: 0,
        transports: null, isUserVerified: true, isBackupEligible: false,
        isBackedUp: false, attestationObject: [0],
        clientDataJson: [0]);

    private async Task<(IdentityResult, int Users)> RegisterAsync(
        string email, UserPasskeyInfo? passkey)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDb>();
        var user = new IdentityUser
        {
            Id = Guid.NewGuid().ToString(), UserName = email, Email = email,
        };
        IdentityResult result;
        try
        {
            result = await Passkeys.RegisterAsync(users, db, user, passkey!);
        }
        catch (ArgumentNullException)
        {
            result = IdentityResult.Failed(
                new IdentityError { Code = "Threw" });
        }
        var count = await db.Users.CountAsync(u => u.Email == email, Ct);
        return (result, count);
    }

    [Fact]
    public async Task Duplicate_email_is_refused_and_nothing_saved()
    {
        var (first, _) = await RegisterAsync("grace@example.dev",
                                             Passkey(10));
        var (second, users) = await RegisterAsync("grace@example.dev",
                                                  Passkey(11));

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.Contains(second.Errors, e => e.Code == "DuplicateEmail");
        Assert.Equal(1, users);
    }

    [Fact]
    public async Task Failed_passkey_save_rolls_back_the_user()
    {
        // The user is created, then storing the passkey throws.
        var (result, users) = await RegisterAsync("lost@example.dev",
                                                  passkey: null);

        Assert.False(result.Succeeded);
        Assert.Equal(0, users);
    }
}

public class P089_BackgroundServiceTests
{
    [Fact]
    public async Task Removes_expired_sessions_each_minute()
    {
        var clock = new FakeTimeProvider();
        var sessions = new SessionStore(clock);
        sessions.Add("short", TimeSpan.FromSeconds(30));
        sessions.Add("long", TimeSpan.FromMinutes(5));

        using var worker = new SessionCleanup(
            sessions, clock, NullLogger<SessionCleanup>.Instance);
        await worker.StartAsync(TestContext.Current.CancellationToken);

        // ExecuteAsync starts on a background thread, so the timer
        // may not exist yet. Move time in small steps until the
        // first tick has run (well before "long" expires).
        for (var i = 0; i < 240 && sessions.Count != 1; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
        Assert.Equal(1, sessions.Count);

        await worker.StopAsync(TestContext.Current.CancellationToken);
    }
}
