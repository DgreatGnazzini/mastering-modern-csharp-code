namespace MasteringModernCSharp.Ch02.Tests;

public class P014_ImplicitSpanConversionsTests
{
    [Fact]
    public void Arrays_call_span_extensions_directly()
    {
        int[] scores = [40, 75, 90, 60];
        Assert.Equal(2, scores.CountAbove(60));
    }

    [Fact]
    public void Arrays_pass_to_span_parameters()
    {
        double[] values = [1, 2, 3, 4];
        Assert.Equal(2.5, SpanStats.Average(values));
        Assert.Equal(3.5, SpanStats.Average(values.AsSpan(2)));
    }
}

public class P015_LambdaParameterModifiersTests
{
    [Fact]
    public void Parses_valid_inputs_and_skips_the_rest()
    {
        Assert.Equal([1, 3], Parsers.ParseAll(["1", "two", "3"], Parsers.Int));
        Assert.Equal(
            [TimeSpan.FromMinutes(1.5)],
            Parsers.ParseAll(["1.5", "soon"], Parsers.Minutes));
    }
}

public class P016_PartialConstructorsEventsTests
{
    [Fact]
    public void Constructor_and_event_work_across_parts()
    {
        var sensor = new Sensor("temp-1");
        double? last = null;
        sensor.Reading += (_, value) => last = value;

        sensor.Publish(21.5);

        Assert.Equal("temp-1", sensor.Id);
        Assert.Equal(1, sensor.SubscriberCount);
        Assert.Equal(21.5, last);
    }

    [Fact]
    public void Constructor_rejects_blank_ids() =>
        Assert.Throws<ArgumentException>(() => new Sensor(" "));
}

public class P017_CompoundAssignmentOperatorsTests
{
    [Fact]
    public void PlusEquals_updates_in_place()
    {
        var total = new Histogram(3);
        var batch = new Histogram(3);
        batch.Record(1);
        batch.Record(1);
        var before = total;

        total += batch;

        Assert.Same(before, total);
        Assert.Equal([0L, 2L, 0L], total.Counts);
    }

    [Fact]
    public void Plus_still_returns_a_new_instance()
    {
        var a = new Histogram(2);
        a.Record(0);
        var sum = a + a;
        Assert.NotSame(a, sum);
        Assert.Equal([2L, 0L], sum.Counts);
        Assert.Equal([1L, 0L], a.Counts);
    }
}

public class P018_IncrementOperatorsTests
{
    [Fact]
    public void Increment_and_decrement_mutate_the_same_object()
    {
        var counter = new HitCounter();
        var same = counter;
        counter++;
        ++counter;
        counter--;
        Assert.Same(same, counter);
        Assert.Equal(1, counter.Value);
    }

    [Fact]
    public void Decrement_stops_at_zero()
    {
        var counter = new HitCounter();
        counter--;
        Assert.Equal(0, counter.Value);
    }
}
