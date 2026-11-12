using MasteringModernCSharp.Ch04;

namespace MasteringModernCSharp.Ch04.Tests;

public class P030_SwitchExpressionsTests
{
    [Theory]
    [InlineData(ShippingSpeed.Standard, 1, 4.99)]
    [InlineData(ShippingSpeed.Standard, 4, 7.99)]
    [InlineData(ShippingSpeed.Express, 3, 15.99)]
    [InlineData(ShippingSpeed.Overnight, 1, 33.99)]
    public void Cost_matches_the_rate_table(
        ShippingSpeed speed, double kg, double expected) =>
        Assert.Equal((decimal)expected, Shipping.Cost(speed, (decimal)kg));

    [Fact]
    public void Zero_weight_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Shipping.Cost(ShippingSpeed.Standard, 0));
}

public class P031_ListPatternsTests
{
    [Fact]
    public void Parses_each_command_shape()
    {
        Assert.Equal(new AddTask("buy milk"),
                     CommandParser.Parse(["add", "buy", "milk"]));
        Assert.Equal(new CompleteTask(3), CommandParser.Parse(["done", "3"]));
        Assert.Equal(new ListTasks(true), CommandParser.Parse(["list", "--all"]));
        Assert.IsType<Help>(CommandParser.Parse([]));
        Assert.IsType<Help>(CommandParser.Parse(["done", "x"]));
        Assert.IsType<Help>(CommandParser.Parse(["add"]));
    }
}

public class P032_PropertyPatternsTests
{
    [Theory]
    [InlineData(150, 1, "Gold", 1, 0.20)]
    [InlineData(50, 1, "Gold", 1, 0.10)]
    [InlineData(50, 1, "Silver", 6, 0.08)]
    [InlineData(50, 12, "Silver", 1, 0.05)]
    [InlineData(50, 1, "Silver", 1, 0.0)]
    public void Rules_apply_in_order(
        double total, int items, string tier, int years, double rate) =>
        Assert.Equal((decimal)rate, Discounts.Rate(
            new Cart((decimal)total, items, new Member(tier, years))));

    [Fact]
    public void Guests_without_membership_still_match() =>
        Assert.Equal(0.05m, Discounts.Rate(new Cart(600, 1, null)));
}

public class P033_ClosedHierarchiesTests
{
    [Fact]
    public void Each_outcome_has_a_message()
    {
        Assert.Equal("Paid. Reference T1.",
            PaymentMessages.Describe(new PaymentResult.Approved("T1")));
        Assert.StartsWith("Declined",
            PaymentMessages.Describe(new PaymentResult.Declined("funds")));
        Assert.Contains("https://pay.example/v",
            PaymentMessages.Describe(new PaymentResult.NeedsVerification(
                new Uri("https://pay.example/v"))));
    }
}

public class P034_ResultTypeTests
{
    [Fact]
    public void Success_flows_through()
    {
        var message = Signup.Register("Ada@Example.com", 30)
            .Match(ok => ok, fail => fail.Code);
        Assert.Equal("Welcome, ada@example.com!", message);
    }

    [Theory]
    [InlineData("nope", 30, "email.invalid")]
    [InlineData("a@b.c", 9, "age.range")]
    public void First_error_stops_the_chain(string email, int age, string code)
    {
        var result = Signup.Register(email, age);
        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Match(_ => "", fail => fail.Code));
    }
}

public class P035_LinqPipelinesTests
{
    [Fact]
    public void Summarizes_by_region_for_one_year()
    {
        Sale[] sales =
        [
            new("West", "Desk", 2, 300, new(2026, 1, 5)),
            new("West", "Lamp", 10, 20, new(2026, 2, 1)),
            new("East", "Lamp", 5, 20, new(2026, 3, 1)),
            new("East", "Desk", 9, 300, new(2025, 3, 1)),
        ];

        var report = SalesReport.Build(sales, 2026);

        Assert.Equal(
            [new RegionSummary("West", 800, "Desk"),
             new RegionSummary("East", 100, "Lamp")],
            report);
    }
}

public class P036_RawStringLiteralsTests
{
    [Fact]
    public void Json_template_is_valid_json()
    {
        var json = Templates.WebhookJson("order.shipped", 42);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(42, doc.RootElement
            .GetProperty("data").GetProperty("orderId").GetInt32());
    }

    [Fact]
    public void Sql_keeps_its_layout_and_has_no_values() =>
        Assert.StartsWith("SELECT o.Id", Templates.OrdersByCustomerSql);
}

public class P037_GenericMathTests
{
    [Fact]
    public void Mean_works_for_several_number_types()
    {
        Assert.Equal(2, NumericStats.Mean<int>([1, 2, 3]));
        Assert.Equal(2.5, NumericStats.Mean<double>([2, 3]));
        Assert.Equal(0.5m, NumericStats.Mean<decimal>([0, 1]));
    }

    [Fact]
    public void Clamp_is_generic_too()
    {
        Assert.Equal(10, NumericStats.Clamp(42, 0, 10));
        Assert.Equal(-1.5, NumericStats.Clamp(-1.5, -2.0, 2.0));
    }
}
