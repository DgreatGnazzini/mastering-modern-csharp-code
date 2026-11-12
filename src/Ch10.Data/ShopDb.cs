using Microsoft.EntityFrameworkCore;

namespace MasteringModernCSharp.Ch10;

// One context for the chapter. Each problem's file adds its
// entities and model configuration to this partial class.
public partial class ShopDb(DbContextOptions<ShopDb> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        ConfigureCustomers(model);   // Problem 90
        ConfigureOrders(model);      // Problems 91, 93, 94
        ConfigureProducts(model);    // Problem 95
        ConfigureMessaging(model);   // Problem 97
    }
}
