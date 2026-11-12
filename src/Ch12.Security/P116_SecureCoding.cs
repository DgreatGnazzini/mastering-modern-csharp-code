using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace MasteringModernCSharp.Ch12;

public class Note
{
    public int Id { get; set; }
    public required string OwnerId { get; set; }
    public required string Title { get; set; }
}

public class NotesDb(DbContextOptions<NotesDb> options)
    : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();
}

public static class SecureCoding
{
    // Injection: FromSql turns {title} into a SQL parameter.
    public static Task<List<Note>> SearchAsync(
        NotesDb db, string title, CancellationToken ct) =>
        db.Notes.FromSql($"SELECT * FROM Notes WHERE Title = {title}")
            .ToListAsync(ct);

    // XSS: user text inserted into HTML is always encoded.
    public static string RenderComment(string author, string text) =>
        $"<p><b>{HtmlEncoder.Default.Encode(author)}</b>: " +
        $"{HtmlEncoder.Default.Encode(text)}</p>";

    // Broken access control: check ownership, not just sign-in.
    public static async Task<IResult> GetNoteAsync(
        int id, ClaimsPrincipal user, NotesDb db, CancellationToken ct)
    {
        var note = await db.Notes.FindAsync([id], ct);
        // 404 for "not yours" too, so IDs can't be probed.
        return note is not null &&
               note.OwnerId == user.FindFirstValue("sub")
            ? Results.Ok(note)
            : Results.NotFound();
    }

    // CSRF: form posts from a cookie-authenticated browser need an
    // antiforgery token. Minimal APIs check it for [FromForm].
    public static void MapNoteForms(this IEndpointRouteBuilder app) =>
        app.MapPost("/notes/rename",
            ([FromForm] string title) => Results.Ok(title));
}

// SSRF: the server fetches a URL the user gave it. Check where the
// connection really goes, after DNS, every time.
//
// A teaching example, not a complete defense: also prefer an
// allowlist of hosts, and block egress at the network level.
public static class SafeFetch
{
    // Addresses that are not globally reachable, from the IANA
    // special-purpose registries. Review this list periodically.
    private static readonly System.Net.IPNetwork[] Blocked =
    [
        .. new[]
        {
            "0.0.0.0/8", "10.0.0.0/8", "100.64.0.0/10", "127.0.0.0/8",
            "169.254.0.0/16", "172.16.0.0/12", "192.0.0.0/24",
            "192.0.2.0/24", "192.88.99.0/24", "192.168.0.0/16",
            "198.18.0.0/15", "198.51.100.0/24", "203.0.113.0/24",
            "224.0.0.0/4", "240.0.0.0/4",
            // IPv6. Prefixes that embed an IPv4 address (NAT64, 6to4,
            // Teredo) are blocked too, since they can lead inside.
            "64:ff9b::/96", "64:ff9b:1::/48", "100::/64", "2001::/23",
            "2001:db8::/32", "2002::/16", "3fff::/20",
        }.Select(System.Net.IPNetwork.Parse),
    ];

    // Only global unicast IPv6 (2000::/3) is considered at all.
    private static readonly System.Net.IPNetwork GlobalV6 =
        System.Net.IPNetwork.Parse("2000::/3");

    public static bool IsPublic(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily == AddressFamily.InterNetworkV6
            && !GlobalV6.Contains(ip))
        {
            return false;   // loopback, link-local, unique local, ...
        }
        return !Blocked.Any(range => range.Contains(ip));
    }

    // With a list of hosts, only those may be fetched at all.
    public static bool IsAllowedHost(
        string host, IReadOnlySet<string>? allowedHosts) =>
        allowedHosts is null || allowedHosts.Contains(host);

    public static HttpClient CreateClient(
        IReadOnlySet<string>? allowedHosts = null)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,  // a redirect could go inside
            UseProxy = false,           // we must see the real target
            ConnectCallback = async (context, ct) =>
            {
                var host = context.DnsEndPoint.Host;
                if (!IsAllowedHost(host, allowedHosts))
                {
                    throw new HttpRequestException(
                        $"{host} is not allowed.");
                }

                var addresses = await Dns.GetHostAddressesAsync(host, ct);
                var target = addresses.FirstOrDefault(IsPublic)
                    ?? throw new HttpRequestException(
                        $"{host} is not public.");

                var socket = new Socket(
                    SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(
                        target, context.DnsEndPoint.Port, ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
        return new HttpClient(handler);
    }
}
