using Microsoft.EntityFrameworkCore;

namespace MasteringModernCSharp.Ch10.Tests;

public class P096_TransactionTests
{
    private static async Task<(TestDb, int Customer, int A, int B)>
        SetupAsync()
    {
        var test = await TestDb.CreateAsync();
        await using var db = test.Context();
        var customer = TestDb.NewCustomer("Ada", "London", false);
        db.Customers.Add(customer);
        var a = new Product { Name = "Desk", Stock = 5 };
        var b = new Product { Name = "Lamp", Stock = 1 };
        db.Products.AddRange(a, b);
        await db.SaveChangesAsync(TestDb.Ct);
        return (test, customer.Id, a.Id, b.Id);
    }

    private static async Task<int[]> StockAsync(TestDb test)
    {
        await using var db = test.Context();
        return await db.Products.OrderBy(p => p.Id)
            .Select(p => p.Stock).ToArrayAsync(TestDb.Ct);
    }

    [Fact]
    public async Task Successful_order_reserves_everything()
    {
        var (test, customer, a, b) = await SetupAsync();
        await using var _ = test;
        await using var db = test.Context();

        var id = await Checkout.PlaceOrderAsync(db, customer,
            [new(a, 2, 100m), new(b, 1, 20m)], TestDb.Ct);

        Assert.NotNull(id);
        var stock = await StockAsync(test);
        Assert.Equal([3, 0], stock);
        Assert.Equal(2, (await db.Orders.Include(o => o.Lines)
            .SingleAsync(o => o.Id == id, TestDb.Ct)).Lines.Count);
    }

    #region p096
    [Fact]
    public async Task Partial_failure_rolls_back_every_change()
    {
        var (test, customer, a, b) = await SetupAsync();
        await using var _ = test;
        await using var db = test.Context();

        // The desk can be reserved, the second lamp can't.
        var id = await Checkout.PlaceOrderAsync(db, customer,
            [new(a, 2, 100m), new(b, 2, 20m)], TestDb.Ct);

        Assert.Null(id);
        var stock = await StockAsync(test);
        Assert.Equal([5, 1], stock);   // the desk was restored
        Assert.Equal(0, await db.Orders.CountAsync(TestDb.Ct));
    }
    #endregion
}
