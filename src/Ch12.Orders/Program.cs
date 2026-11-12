using MasteringModernCSharp.Ch12.Orders;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<OrdersDb>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("orders")));

var app = builder.Build();

app.MapPost("/orders", async (NewOrder input, OrdersDb db,
                              CancellationToken ct) =>
{
    var order = new Order
    {
        Reference = input.Reference,
        Customer = input.Customer,
        Total = input.Total,
    };
    db.Orders.Add(order);
    try
    {
        await db.SaveChangesAsync(ct);
    }
    catch (DbUpdateException ex) when (ex.InnerException is
        PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
    {
        return Results.Conflict($"Order {input.Reference} exists.");
    }
    return Results.Created($"/orders/{order.Id}", order);
});

app.MapGet("/orders/{id:int}", async (int id, OrdersDb db,
                                      CancellationToken ct) =>
    await db.Orders.FindAsync([id], ct) is { } order
        ? Results.Ok(order) : Results.NotFound());

// ILIKE is PostgreSQL-only: a fake database can't check this query.
app.MapGet("/orders", (string customer, OrdersDb db) =>
    db.Orders
      .Where(o => EF.Functions.ILike(o.Customer, $"%{customer}%"))
      .OrderBy(o => o.Id)
      .AsAsyncEnumerable());

app.Run();
