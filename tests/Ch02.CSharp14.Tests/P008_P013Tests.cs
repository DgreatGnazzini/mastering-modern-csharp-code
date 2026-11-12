namespace MasteringModernCSharp.Ch02.Tests;

public class P008_StaticExtensionMembersTests
{
    [Fact]
    public void List_Repeat_reads_like_a_static_factory() =>
        Assert.Equal([0, 0, 0], List<int>.Repeat(0, 3));

    [Fact]
    public void DateOnly_Today_matches_DateTime_Today() =>
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), DateOnly.Today);
}

public class P009_MigrateExtensionMethodsTests
{
    [Fact]
    public void Instance_call_syntax_still_works()
    {
        Assert.True(4.IsEven());
        Assert.Equal(10, 42.Clamp(0, 10));
        Assert.True(3.IsOdd);
    }

    [Fact]
    public void Static_call_syntax_still_works()
    {
        // Existing callers that used the static form keep compiling.
        Assert.True(NumberExtensions.IsEven(4));
        Assert.Equal(0, NumberExtensions.Clamp(-5, 0, 10));
    }
}

public class P010_FieldKeywordTests
{
    [Fact]
    public void Setter_validates_and_stores()
    {
        var t = new Thermostat { TargetCelsius = 21.5, Room = "  Office " };
        Assert.Equal(21.5, t.TargetCelsius);
        Assert.Equal("Office", t.Room);
    }

    [Fact]
    public void Invalid_values_throw()
    {
        var t = new Thermostat();
        Assert.Throws<ArgumentOutOfRangeException>(() => t.TargetCelsius = -300);
        Assert.Throws<ArgumentException>(() => t.Room = " ");
    }
}

public class P011_LazyFieldPropertyTests
{
    [Fact]
    public void Lines_load_once_on_first_read()
    {
        var template = new ReportTemplate(() => ["Title", "Body"]);
        Assert.Equal(0, template.LoadCount);
        Assert.Equal(2, template.Lines.Count);
        Assert.Equal(2, template.Lines.Count);
        Assert.Equal(1, template.LoadCount);
    }
}

public class P012_NullConditionalAssignmentTests
{
    [Fact]
    public void Updates_an_existing_customer()
    {
        var now = new DateTime(2026, 11, 11);
        var customer = new Customer { Tags = [] };
        VisitTracker.RecordVisit(customer, now);
        Assert.Equal(now, customer.LastSeen);
        Assert.Equal(1, customer.Visits);
        Assert.Equal(["returning"], customer.Tags);
    }

    [Fact]
    public void Does_nothing_for_null() =>
        VisitTracker.RecordVisit(null, DateTime.Now);
}

public class P013_NameofUnboundGenericsTests
{
    [Fact]
    public void Names_come_without_type_arguments()
    {
        Assert.Equal("List", CacheKeys.ListName);
        Assert.Equal("Dictionary:String:Int32", CacheKeys.For<string, int>());
    }
}
