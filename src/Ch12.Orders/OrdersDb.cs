using Microsoft.EntityFrameworkCore;

namespace MasteringModernCSharp.Ch12.Orders;

public record NewOrder(string Reference, string Customer, decimal Total);

public class Order
{
    public int Id { get; set; }
    public required string Reference { get; set; }
    public required string Customer { get; set; }
    public decimal Total { get; set; }
}

public class OrdersDb(DbContextOptions<OrdersDb> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder model) =>
        model.Entity<Order>()
             .HasIndex(o => o.Reference).IsUnique();
}
