using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MasteringModernCSharp.Ch10;

// Lets `dotnet ef` create the context at design time, without
// starting the application.
public sealed class ShopDbFactory : IDesignTimeDbContextFactory<ShopDb>
{
    public ShopDb CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ShopDb>()
            .UseSqlite(Environment.GetEnvironmentVariable("SHOP_DB")
                       ?? "Data Source=shop.db")
            .Options);
}
