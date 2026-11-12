using System.Collections.Immutable;
using MasteringModernCSharp.Ch03;

namespace MasteringModernCSharp.Ch03.Tests;

file sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Now => now;
}

public class P019_PrimaryConstructorsTests
{
    [Fact]
    public void Captured_dependencies_are_used()
    {
        var due = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var service = new InvoiceService(
            new FixedClock(due.AddDays(1)), lateFeeRate: 0.1m);
        Assert.Equal(110m, service.AmountDue(new("INV-1", 100m, due)));
    }

    [Fact]
    public void Validated_parameter_is_copied_once()
    {
        var policy = new RetryPolicy(3);
        Assert.True(policy.ShouldRetry(2));
        Assert.False(policy.ShouldRetry(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy(0));
    }
}

public class P020_RecordKindsTests
{
    [Fact]
    public void Records_compare_by_value()
    {
        Assert.Equal(new Money(5, "USD"), new Money(5, "USD"));
        Assert.Equal(new Customer("1", "Ada", "a@x.io"),
                     new Customer("1", "Ada", "a@x.io"));
        Assert.Equal(new Money(8, "USD"),
                     new Money(5, "USD").Add(new Money(3, "USD")));
    }

    [Fact]
    public void Mixed_currencies_are_rejected() =>
        Assert.Throws<InvalidOperationException>(
            () => new Money(1, "USD").Add(new Money(1, "EUR")));

    [Fact]
    public void Readonly_struct_computes_length() =>
        Assert.Equal(5, new Point3(3, 4, 0).Length);
}

public class P021_RequiredInitTests
{
    [Fact]
    public void Object_initializer_sets_required_members()
    {
        var s = new SmtpSettings { Host = "smtp.example.com", FromAddress = "a@b.c" };
        Assert.Equal(587, s.Port);
    }

    [Fact]
    public void SetsRequiredMembers_constructor_needs_no_initializer() =>
        Assert.Equal("h", new SmtpSettings("h", "f").Host);
}

public class P023_WithExpressionsTests
{
    private static readonly Shipment Original = new(
        "S1",
        new Contact("Ada", new Address("1 Main St", "Austin", "US")),
        ["Book"]);

    [Fact]
    public void Nested_with_changes_only_the_city()
    {
        var moved = ShipmentUpdates.MoveTo(Original, "Dallas");
        Assert.Equal("Dallas", moved.Recipient.Address.City);
        Assert.Equal("Austin", Original.Recipient.Address.City);
        Assert.Equal("1 Main St", moved.Recipient.Address.Street);
    }

    [Fact]
    public void Adding_an_item_leaves_the_original_list_alone()
    {
        var updated = ShipmentUpdates.AddItem(Original, "Pen");
        Assert.Equal(["Book", "Pen"], updated.Items);
        Assert.Equal(["Book"], Original.Items);
    }
}

public class P024_CollectionExpressionsTests
{
    [Fact]
    public void Spread_combines_collections()
    {
        Assert.Equal(6, Menus.FullMenu(includeSpecial: false).Count);
        Assert.Contains("Chef's special", Menus.FullMenu(includeSpecial: true));
        Assert.IsType<ImmutableArray<string>>(Menus.Desserts);
    }

    [Fact]
    public void Span_targets_work_too() =>
        Assert.Equal(2, Menus.CountVegetarian(["Soup", "Pasta", "Fruit"]));
}

public class P025_CollectionBuilderTests
{
    [Fact]
    public void Collection_expression_builds_a_TagSet()
    {
        TagSet tags = ["urgent", "Billing", " URGENT "];
        Assert.Equal(2, tags.Count);
        Assert.True(tags.Contains("billing"));
    }
}

public class P026_ParamsCollectionsTests
{
    [Fact]
    public void Span_params_accept_separate_arguments() =>
        Assert.Equal(6, Stats.Sum(1, 2, 3));

    [Fact]
    public void Enumerable_params_accept_any_sequence()
    {
        Assert.Equal("a b", Stats.JoinWords("a", " ", "b"));
        Assert.Equal("x y", Stats.JoinWords(new List<string> { "x", "y" }));
    }
}

public class P027_UsingAliasesTests
{
    [Fact]
    public void Distance_between_two_cities_is_about_right()
    {
        // Austin to Dallas is roughly 300 km in a straight line.
        var km = Geo.DistanceKm((30.27, -97.74), (32.78, -96.80));
        Assert.InRange(km, 280, 310);
    }

    [Fact]
    public void Route_length_sums_its_legs() =>
        Assert.Equal(0, Geo.LengthKm([(1, 1), (1, 1)]));
}
