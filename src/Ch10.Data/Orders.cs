using Microsoft.EntityFrameworkCore;

namespace MasteringModernCSharp.Ch10;

public class Order
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTime PlacedAt { get; set; }
    public string Status { get; set; } = "Open";
    public bool IsDeleted { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public List<OrderLine> Lines { get; } = [];
}

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public required string Sku { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public partial class ShopDb
{
    public DbSet<Order> Orders => Set<Order>();

    private void ConfigureOrders(ModelBuilder model)
    {
        model.Entity<Order>()
            .HasIndex(o => new { o.TenantId, o.PlacedAt });
        model.Entity<OrderLine>().Property(l => l.UnitPrice)
            .HasPrecision(18, 2);
        ConfigureOrderFilters(model);
    }
}
