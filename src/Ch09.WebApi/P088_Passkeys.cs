using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MasteringModernCSharp.Ch09;

public sealed class AppIdentityDb(DbContextOptions<AppIdentityDb> options)
    : IdentityDbContext<IdentityUser>(options);

public record PasskeyStart(string Email);

public static class Passkeys
{
    public static IServiceCollection AddPasskeyIdentity(
        this IServiceCollection services, SqliteConnection db)
    {
        services.AddDbContext<AppIdentityDb>(o => o.UseSqlite(db));
        services
            .AddIdentityCore<IdentityUser>(options =>
            {
                // Version 3 of the schema adds the passkeys table.
                options.Stores.SchemaVersion =
                    IdentitySchemaVersions.Version3;
                // One account per email address.
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppIdentityDb>()
            .AddSignInManager();
        services.AddAuthentication().AddIdentityCookies();
        return services;
    }

    public static void MapPasskeys(this IEndpointRouteBuilder app)
    {
        var passkeys = app.MapGroup("/passkeys");

        // 1. Sign-up: options for navigator.credentials.create().
        passkeys.MapPost("/register/options", async (
            PasskeyStart start, SignInManager<IdentityUser> signIn) =>
        {
            var entity = new PasskeyUserEntity
            {
                Id = Guid.NewGuid().ToString(),
                Name = start.Email,
                DisplayName = start.Email,
            };
            var json =
                await signIn.MakePasskeyCreationOptionsAsync(entity);
            return Results.Content(json, "application/json");
        });

        // 2. Sign-up: verify the new credential and save it.
        passkeys.MapPost("/register", async (
            HttpRequest request, SignInManager<IdentityUser> signIn,
            UserManager<IdentityUser> users, AppIdentityDb db) =>
        {
            var credential = await ReadBodyAsync(request);
            var result = await signIn.PerformPasskeyAttestationAsync(
                credential);
            if (!result.Succeeded)
            {
                return Results.Problem(result.Failure.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var user = new IdentityUser
            {
                Id = result.UserEntity.Id,
                UserName = result.UserEntity.Name,
                Email = result.UserEntity.Name,
            };
            var saved = await RegisterAsync(users, db, user,
                                            result.Passkey);
            if (!saved.Succeeded)
            {
                // For example DuplicateEmail: nothing was stored.
                return Results.ValidationProblem(saved.Errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key,
                        g => g.Select(e => e.Description).ToArray()));
            }

            // Only now does the account exist, so only now sign in.
            await signIn.SignInAsync(user, isPersistent: false);
            return Results.Ok(new { user.UserName });
        });

        // 3. Sign-in: options for navigator.credentials.get().
        //    No user is given, so any saved passkey may answer.
        passkeys.MapPost("/login/options", async (
            SignInManager<IdentityUser> signIn) =>
            Results.Content(
                await signIn.MakePasskeyRequestOptionsAsync(user: null),
                "application/json"));

        // 4. Sign-in: verify the assertion and issue the cookie.
        passkeys.MapPost("/login", async (
            HttpRequest request, SignInManager<IdentityUser> signIn) =>
        {
            var result = await signIn.PasskeySignInAsync(
                await ReadBodyAsync(request));
            return result.Succeeded
                ? Results.Ok()
                : Results.Unauthorized();
        });
    }

    // Stores the user and the passkey together, or neither.
    public static async Task<IdentityResult> RegisterAsync(
        UserManager<IdentityUser> users, AppIdentityDb db,
        IdentityUser user, UserPasskeyInfo passkey)
    {
        // Disposing without CommitAsync rolls back, also when one of
        // the calls below throws.
        await using var tx = await db.Database.BeginTransactionAsync();

        var created = await users.CreateAsync(user);
        if (!created.Succeeded) return created;

        var added = await users.AddOrUpdatePasskeyAsync(user, passkey);
        if (!added.Succeeded) return added;

        await tx.CommitAsync();
        return IdentityResult.Success;
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body);
        return await reader.ReadToEndAsync();
    }
}
