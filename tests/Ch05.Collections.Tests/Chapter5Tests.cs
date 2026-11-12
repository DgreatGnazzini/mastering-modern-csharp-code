using MasteringModernCSharp.Ch05;

namespace MasteringModernCSharp.Ch05.Tests;

public class P038_LeftRightJoinTests
{
    private static readonly Employee[] Staff =
        [new(1, "Ada", 10), new(2, "Lin", null), new(3, "Sam", 10)];
    private static readonly Department[] Depts =
        [new(10, "Engineering"), new(20, "Legal")];

    [Fact]
    public void Left_join_keeps_employees_without_a_department() =>
        Assert.Equal(
            [("Ada", "Engineering"), ("Lin", "(none)"), ("Sam", "Engineering")],
            StaffDirectory.WithDepartments(Staff, Depts));

    [Fact]
    public void Right_join_keeps_empty_departments() =>
        Assert.Equal(
            [("Engineering", 2), ("Legal", 0)],
            StaffDirectory.Headcounts(Staff, Depts));
}

public class P039_CountByAggregateByTests
{
    private static readonly Order[] Orders =
    [
        new("ada", "Shipped", 10), new("lin", "Pending", 5),
        new("ada", "Shipped", 15),
    ];

    [Fact]
    public void Counts_by_status() =>
        Assert.Equal(2, OrderStats.CountByStatus(Orders)["Shipped"]);

    [Fact]
    public void Totals_by_customer() =>
        Assert.Equal(25m, OrderStats.SpendByCustomer(Orders)["ada"]);
}

public class P040_FrozenCollectionsTests
{
    [Fact]
    public void Lookups_ignore_case()
    {
        Assert.Equal("Japan", CountryCodes.NameOf("jp"));
        Assert.Null(CountryCodes.NameOf("XX"));
        Assert.True(CountryCodes.IsInEu("de"));
        Assert.False(CountryCodes.IsInEu("US"));
    }
}

public class P041_OrderedDictionaryTests
{
    [Fact]
    public void Keeps_insertion_order_and_supports_index_access()
    {
        var headers = new HeaderList();
        headers.Set("Accept", "json");
        headers.Set("User-Agent", "book");
        headers.InsertFirst("Host", "example.com");
        headers.Set("accept", "xml");

        Assert.Equal("Host", headers[0].Key);
        Assert.Equal("xml", headers.Get("ACCEPT"));
        Assert.Equal("Host: example.com\nAccept: xml\nUser-Agent: book",
                     headers.Render());
    }
}

public class P042_PriorityQueueTests
{
    [Fact]
    public void Most_severe_and_oldest_first()
    {
        var q = new TicketQueue();
        var t0 = new DateTime(2026, 1, 1);
        q.Add(new("printer", Severity.Low, t0));
        q.Add(new("db down", Severity.Critical, t0.AddHours(2)));
        q.Add(new("login", Severity.Critical, t0.AddHours(1)));

        Assert.Equal("login", q.PeekNext()!.Title);
        Assert.True(q.TryTakeNext(out var first));
        Assert.True(q.TryTakeNext(out var second));
        Assert.Equal(["login", "db down"], [first!.Title, second!.Title]);
    }
}

public class P043_AlternateLookupTests
{
    [Fact]
    public void Counts_words_case_insensitively()
    {
        var counts = WordCounter.Count("The cat. the DOG, the end!");
        Assert.Equal(3, counts["the"]);
        Assert.Equal(1, counts["dog"]);
        Assert.Equal(4, counts.Count);
    }
}

public class P044_BatchingSequencesTests
{
    [Fact]
    public async Task Imports_in_fixed_size_batches()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllLinesAsync(path,
            Enumerable.Range(1, 1201).Select(i => $"row {i}"),
            TestContext.Current.CancellationToken);
        var sizes = new List<int>();

        var batches = await Batching.ImportAsync(path, b =>
        {
            sizes.Add(b.Length);
            return Task.CompletedTask;
        });

        Assert.Equal(3, batches);
        Assert.Equal([500, 500, 201], sizes);
        File.Delete(path);
    }
}

public class P045_NumericOrderingTests
{
    [Fact]
    public void Numbers_inside_strings_sort_by_value() =>
        Assert.Equal(
            ["file1", "File2", "file10", "file20"],
            NaturalSort.Sort(["file10", "File2", "file20", "file1"]));
}
