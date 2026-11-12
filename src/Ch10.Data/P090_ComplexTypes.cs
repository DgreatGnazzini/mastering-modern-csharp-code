using Microsoft.EntityFrameworkCore;

namespace MasteringModernCSharp.Ch10;

// Value objects: no identity of their own, compared by value.
public record Address(string Street, string City, string PostCode);

public record Preferences(
    bool Newsletter, string Language, List<string> Interests);

public class Customer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required Address ShippingAddress { get; set; }
    public required Preferences Preferences { get; set; }
    public List<Order> Orders { get; } = [];
}

public partial class ShopDb
{
    public DbSet<Customer> Customers => Set<Customer>();

    private void ConfigureCustomers(ModelBuilder model) =>
        model.Entity<Customer>(customer =>
        {
            // Stored as ordinary columns of the Customers table:
            // ShippingAddress_Street, ShippingAddress_City, ...
            customer.ComplexProperty(c => c.ShippingAddress);

            // Stored as one JSON document in a single column.
            customer.ComplexProperty(c => c.Preferences, p => p.ToJson());
        });
}

public static class CustomerQueries
{
    // Both kinds can be queried; EF translates the JSON path.
    public static Task<List<string>> InCityWantingNewsletterAsync(
        ShopDb db, string city, CancellationToken ct = default) =>
        db.Customers
            .Where(c => c.ShippingAddress.City == city
                        && c.Preferences.Newsletter)
            .Select(c => c.Name)
            .ToListAsync(ct);
}
