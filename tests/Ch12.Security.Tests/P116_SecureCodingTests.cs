using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MasteringModernCSharp.Ch12.Tests;

public class P116_SecureCodingTests : IDisposable
{
    private static CancellationToken Ct =>
        TestContext.Current.CancellationToken;

    private readonly SqliteConnection _connection =
        new("Data Source=:memory:");
    private readonly NotesDb _db;

    public P116_SecureCodingTests()
    {
        _connection.Open();
        _db = new NotesDb(new DbContextOptionsBuilder<NotesDb>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.Notes.AddRange(
            new Note { OwnerId = "ada", Title = "Groceries" },
            new Note { OwnerId = "lin", Title = "Passwords" });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private const string Attack = "x' OR '1'='1";

    #region p116
    [Fact]
    public async Task Interpolated_FromSql_is_parameterized()
    {
        // The attack string is just a title that matches nothing.
        Assert.Empty(await SecureCoding.SearchAsync(_db, Attack, Ct));

        // Concatenating it into raw SQL returns every row.
        var leaked = await _db.Notes
            .FromSqlRaw("SELECT * FROM Notes WHERE Title = '" +
                        Attack + "'")
            .ToListAsync(Ct);
        Assert.Equal(2, leaked.Count);
    }
    #endregion

    [Fact]
    public void Html_is_encoded()
    {
        var html = SecureCoding.RenderComment(
            "Eve", "<script>steal()</script>");
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    private static ClaimsPrincipal User(string sub) =>
        new(new ClaimsIdentity([new Claim("sub", sub)], "test"));

    [Fact]
    public async Task Users_only_see_their_own_notes()
    {
        var lins = await _db.Notes.SingleAsync(
            n => n.OwnerId == "lin", Ct);

        var asLin = await SecureCoding.GetNoteAsync(
            lins.Id, User("lin"), _db, Ct);
        var asAda = await SecureCoding.GetNoteAsync(
            lins.Id, User("ada"), _db, Ct);

        Assert.IsAssignableFrom<IValueHttpResult>(asLin);
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(asAda);
        Assert.Equal(404,
            ((IStatusCodeHttpResult)asAda).StatusCode);
    }

    [Fact]
    public async Task Form_post_without_antiforgery_token_is_rejected()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAntiforgery();
        await using var app = builder.Build();
        app.UseAntiforgery();
        app.MapNoteForms();
        await app.StartAsync(Ct);

        var response = await app.GetTestClient().PostAsync(
            "/notes/rename",
            new FormUrlEncodedContent(
                new Dictionary<string, string> { ["title"] = "x" }),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.20.0.1", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)]   // cloud metadata
    [InlineData("::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:10.0.0.1", false)]
    [InlineData("100.64.0.1", false)]        // carrier-grade NAT
    [InlineData("198.18.0.1", false)]        // benchmarking
    [InlineData("0.0.0.0", false)]
    [InlineData("224.0.0.1", false)]         // multicast
    [InlineData("255.255.255.255", false)]
    [InlineData("fe80::1", false)]
    [InlineData("ff02::1", false)]
    [InlineData("64:ff9b::a00:1", false)]    // NAT64 for 10.0.0.1
    [InlineData("2002:a00:1::1", false)]     // 6to4 for 10.0.0.1
    [InlineData("2001:db8::1", false)]       // documentation
    [InlineData("8.8.8.8", true)]
    [InlineData("100.128.0.1", true)]        // just past 100.64/10
    [InlineData("2606:4700::1111", true)]
    public void Only_public_addresses_are_allowed(string ip, bool ok) =>
        Assert.Equal(ok, SafeFetch.IsPublic(IPAddress.Parse(ip)));

    [Fact]
    public async Task Safe_client_refuses_to_connect_inside()
    {
        using var client = SafeFetch.CreateClient();
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync("http://localhost:9/", Ct));
        Assert.Contains("not public", ex.ToString());
    }

    [Fact]
    public async Task Allowlist_refuses_other_hosts()
    {
        using var client = SafeFetch.CreateClient(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "images.example.com",
            });
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync("http://localhost:9/", Ct));
        Assert.Contains("not allowed", ex.ToString());
    }
}
