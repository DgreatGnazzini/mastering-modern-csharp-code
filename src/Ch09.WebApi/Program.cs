using MasteringModernCSharp.Ch09;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddSingleton(TimeProvider.System);
services.AddSingleton<TodoStore>();                         // 76
services.AddValidation();                                   // 79
services.AddApiDocs();                                      // 80
services.AddErrorHandling();                                // 83
services.AddProtection();                                   // 84
services.AddHybridCache();                                  // 85
services.AddSingleton<ProductRepository>();
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? new JwtSettings();
services.AddSingleton(jwt);
services.AddJwtAuth(jwt);                                   // 86
var identityDb = new SqliteConnection("DataSource=:memory:");
identityDb.Open();
services.AddPasskeyIdentity(identityDb);                    // 88
services.AddSingleton<SessionStore>();                      // 89
services.AddPayments();                                     // 77
services.AddShipping();                                     // 78
services.AddVersioning();                                   // 81
services.AddHostedService<SessionCleanup>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppIdentityDb>()
        .Database.EnsureCreated();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseOutputCache();
app.UseAuthentication();
app.UseAuthorization();

app.MapTodos();
app.MapSignup();
app.MapApiDocs();
app.MapPriceStream();
app.MapErrorDemo();
app.MapProtectedEndpoints();
app.MapProducts();
app.MapOrderWrites();
app.MapPasskeys();
app.MapPayments();
app.MapCustomers();
app.MapGet("/shipping", (decimal total, ShippingQuote quote) =>
    quote.For(total));

app.Run();
