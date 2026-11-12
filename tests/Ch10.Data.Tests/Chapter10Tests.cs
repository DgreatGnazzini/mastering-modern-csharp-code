using System.Data.Common;
using MasteringModernCSharp.Ch10;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MasteringModernCSharp.Ch10.Tests;

// Counts the SQL commands EF sends, to prove N+1 and its fixes.
public sealed class CommandCounter : DbCommandInterceptor
{
    public int Count;
    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken ct = default)
    {
        Interlocked.Increment(ref Count);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken ct = default)
    {
        Interlocked.Increment(ref Count);
        return ValueTask.FromResult(result);
    }
}

// A real SQLite database in memory, built from the migrations.
public sealed class TestDb : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public CommandCounter Commands { get; } = new();

    private TestDb() => _connection.Open();

    public static async Task<TestDb> CreateAsync()
    {
        var db = new TestDb();
        await using var ctx = db.Context();
        await ctx.Database.MigrateAsync(Ct);
        return db;
    }

    public ShopDb Context(int tenantId = 1) =>
        new(new DbContextOptionsBuilder<ShopDb>()
            .UseSqlite(_connection)
            .AddInterceptors(Commands)
            .Options) { TenantId = tenantId };

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    public async Task SeedAsync()
    {
        await using var db = Context();
        var ada = NewCustomer("Ada", "London", newsletter: true);
        var bob = NewCustomer("Bob", "Paris", newsletter: false);
        var cy = NewCustomer("Cy", "London", newsletter: false);
        db.Customers.AddRange(ada, bob, cy);
        await db.SaveChangesAsync(Ct);

        foreach (var (customer, tenant, day) in new[]
                 { (ada, 1, 1), (ada, 1, 20), (bob, 1, 5), (bob, 2, 6) })
        {
            var order = new Order
            {
                CustomerId = customer.Id, TenantId = tenant,
                PlacedAt = new DateTime(2026, 1, day),
            };
            order.Lines.Add(new OrderLine { Sku = "A", Quantity = 2, UnitPrice = 5m });
            order.Lines.Add(new OrderLine { Sku = "B", Quantity = 1, UnitPrice = 3m });
            db.Orders.Add(order);
        }
        db.Products.Add(new Product { Name = "Widget", Stock = 10 });
        await db.SaveChangesAsync(Ct);
    }

    public static Customer NewCustomer(string name, string city, bool newsletter) => new()
    {
        Name = name,
        ShippingAddress = new($"1 {name} St", city, "12345"),
        Preferences = new(newsletter, "en", ["books"]),
    };
}

public class P090_ComplexTypeTests
{
    [Fact]
    public async Task Queries_columns_and_json()
    {
        await using var test = await TestDb.CreateAsync();
        await test.SeedAsync();
        await using var db = test.Context();

        Assert.Equal(["Ada"], await CustomerQueries
            .InCityWantingNewsletterAsync(db, "London", TestDb.Ct));

        var ada = await db.Customers.SingleAsync(c => c.Name == "Ada", TestDb.Ct);
        Assert.Equal(["books"], ada.Preferences.Interests);
    }

    [Fact]
    public async Task Replacing_a_value_object_updates_the_row()
    {
        await using var test = await TestDb.CreateAsync();
        await test.SeedAsync();
        await using (var db = test.Context())
        {
            var ada = await db.Customers.SingleAsync(c => c.Name == "Ada", TestDb.Ct);
            ada.ShippingAddress = ada.ShippingAddress with { City = "Leeds" };
            await db.SaveChangesAsync(TestDb.Ct);
        }
        await using var check = test.Context();
        Assert.Equal("Leeds", (await check.Customers
            .SingleAsync(c => c.Name == "Ada", TestDb.Ct)).ShippingAddress.City);
    }
}

public class P091_QueryFilterTests
{
    [Fact]
    public async Task Soft_delete_and_tenant_filters()
    {
        await using var test = await TestDb.CreateAsync();
        await test.SeedAsync();
        await using var db = test.Context(tenantId: 1);

        Assert.Equal(3, await db.Orders.CountAsync(TestDb.Ct));
        db.Orders.Remove(await db.Orders.FirstAsync(TestDb.Ct));
        await db.SaveChangesAsync(TestDb.Ct);

        Assert.Equal(2, await db.Orders.CountAsync(TestDb.Ct));
        Assert.Single(await RecycleBin.DeletedOrdersAsync(db, TestDb.Ct));
        Assert.Equal(4, await db.Orders.IgnoreQueryFilters().CountAsync(TestDb.Ct));

        await using var other = test.Context(tenantId: 2);
        Assert.Equal(1, await other.Orders.CountAsync(TestDb.Ct));
    }
}

public class P092_LeftJoinTests
{
    [Fact]
    public async Task Customers_without_orders_are_kept()
    {
        await using var test = await TestDb.CreateAsync();
        await test.SeedAsync();
        await using var db = test.Context();
        var query = SalesReport.CustomersWithOrders(db);

        Assert.Contains("LEFT JOIN", query.ToQueryString());
        var rows = await query.ToListAsync(TestDb.Ct);
        Assert.Contains(rows, r => r.Customer == "Cy" && r.OrderId is null);
        Assert.Equal(2, rows.Count(r => r.Customer == "Ada"));
    }
}

public class P093_BulkUpdateTests
{
    [Fact]
    public async Task Updates_and_deletes_in_one_statement()
    {
        await using var test = await TestDb.CreateAsync();
        await test.SeedAsync();
        await using var db = test.Context();

        test.Commands.Count = 0;
        var archived = await OrderMaintenance.ArchiveOldAsync(
            db, new DateTime(2026, 1, 10), stampDate: true, TestDb.Ct);
        Assert.Equal(2, archived); // tenant 1 only
        Assert.Equal(1, test.Commands.Count);

        db.Orders.Remove(await db.Orders.FirstAsync(TestDb.Ct));
        await db.SaveChangesAsync(TestDb.Ct);
        Assert.Equal(1, await OrderMaintenance.PurgeDeletedAsync(db, TestDb.Ct));
        Assert.Equal(3, await db.Orders.IgnoreQueryFilters().CountAsync(TestDb.Ct));
    }
}

public class P094_NPlusOneTests
{
    [Fact]
    public async Task Projection_uses_one_query_instead_of_many()
    {
        await using var test = await TestDb.CreateAsync();
        await test.SeedAsync();

        await using (var db = test.Context())
        {
            test.Commands.Count = 0;
            var slow = await OrderReads.SlowAsync(db, TestDb.Ct);
            // 1 for the orders, 1 per order for its lines, and 1 per
            // distinct customer (EF skips a customer already loaded).
            Assert.Equal(3, slow.Count);
            Assert.Equal(1 + 3 + 2, test.Commands.Count);
        }
        await using (var db = test.Context())
        {
            test.Commands.Count = 0;
            var fast = await OrderReads.ProjectedAsync(db, TestDb.Ct);
            Assert.Equal(1, test.Commands.Count);
            Assert.All(fast, o => Assert.Equal(13m, o.Total));
        }
        await using (var db = test.Context())
        {
            test.Commands.Count = 0;
            await OrderReads.WithLinesAsync(db, TestDb.Ct);
            // Orders joined to customers, then all lines: 2 queries.
            Assert.Equal(2, test.Commands.Count);
        }
        await using (var db = test.Context())
        {
            var first = await db.Orders.FirstAsync(TestDb.Ct);
            Assert.Equal(2, (await OrderReads.FindAsync(db, first.Id))!.Lines.Count);
        }
    }
}

public class P095_ConcurrencyTests
{
    [Fact]
    public async Task Stale_write_is_detected()
    {
        await using var test = await TestDb.CreateAsync();
        await test.SeedAsync();
        await using var a = test.Context();
        await using var b = test.Context();
        var pa = await a.Products.SingleAsync(TestDb.Ct);
        var pb = await b.Products.SingleAsync(TestDb.Ct);

        pa.Stock = 9; pa.Version = Guid.NewGuid();
        await a.SaveChangesAsync(TestDb.Ct);

        pb.Stock = 8; pb.Version = Guid.NewGuid();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => b.SaveChangesAsync(TestDb.Ct));
    }

    [Fact]
    public async Task Reserve_never_oversells()
    {
        await using var test = await TestDb.CreateAsync();
        await test.SeedAsync();
        await using var a = test.Context();
        await using var b = test.Context();
        var id = (await a.Products.SingleAsync(TestDb.Ct)).Id;

        Assert.True(await Inventory.ReserveAsync(a, id, 6, TestDb.Ct));
        Assert.False(await Inventory.ReserveAsync(b, id, 6, TestDb.Ct));
        Assert.True(await Inventory.ReserveAsync(b, id, 4, TestDb.Ct));
    }
}

public class P098_VectorSearchTests
{
    [Fact]
    public void Translates_to_VECTOR_DISTANCE()
    {
        // No server needed: ToQueryString only translates.
        using var db = new KnowledgeBaseDb(
            new DbContextOptionsBuilder<KnowledgeBaseDb>()
                .UseSqlServer("Server=unused;Database=unused")
                .Options);
        var sql = SemanticSearch.Nearest(db, new float[1536], 3)
            .ToQueryString();
        Assert.Contains("VECTOR_DISTANCE('cosine'", sql);
        Assert.Contains("TOP(@", sql);
    }
}

public class P099_MigrationTests
{
    [Fact]
    public async Task Migrations_match_the_model()
    {
        await using var test = await TestDb.CreateAsync();
        await using var db = test.Context();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestDb.Ct));
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
