using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MasteringModernCSharp.Ch08;
using MasteringModernCSharp.Ch08.Benchmarks;

namespace MasteringModernCSharp.Ch08.Tests;

public class P066_BenchmarkTests
{
    // A benchmark that compares different answers measures nothing
    // useful, so check that every candidate agrees first.
    [Theory]
    [InlineData(10)]
    [InlineData(1_000)]
    public void All_candidates_return_the_same_answer(int size)
    {
        var b = new LookupBenchmarks { Size = size };
        b.Setup();
        Assert.Equal(b.List(), b.HashSet());
        Assert.Equal(b.List(), b.Frozen());
    }
}

public class P067_SpanParsingTests
{
    [Fact]
    public void Parses_points_from_spans()
    {
        var p = GeoPoint.Parse("30.27, -97.74", CultureInfo.InvariantCulture);
        Assert.Equal(new GeoPoint(30.27, -97.74), p);
        Assert.False(GeoPoint.TryParse("91,0", null, out _));
        Assert.False(GeoPoint.TryParse("nonsense", null, out _));
    }

    [Fact]
    public void Generic_list_parser_works_for_any_type()
    {
        Assert.Equal([1, 2, 3], SpanParsing.ParseList<int>("1; 2;3;"));
        var points = SpanParsing.ParseList<GeoPoint>("1,2 | 3,4", '|');
        Assert.Equal(new GeoPoint(3, 4), points[1]);
    }
}

public class P069_ArrayPoolTests
{
    [Fact]
    public async Task Matches_one_shot_hash()
    {
        var data = new byte[250_000];
        Random.Shared.NextBytes(data);
        var expected = Convert.ToHexStringLower(SHA256.HashData(data));

        var actual = await FileHasher.Sha256Async(
            new MemoryStream(data), TestContext.Current.CancellationToken);
        Assert.Equal(expected, actual);
    }
}

public class P070_InlineArrayTests
{
    [Fact]
    public void Keeps_only_the_last_eight()
    {
        var r = new RecentReadings();
        Assert.Equal(0, r.Average());
        for (var i = 1; i <= 10; i++) r.Add(i);
        Assert.Equal(8, r.Count);
        Assert.Equal((3 + 10) / 2.0, r.Average());
    }

    [Fact]
    public unsafe void Is_stored_inline() =>
        Assert.Equal(8 * sizeof(double), sizeof(EightDoubles));
}

public class P071_VectorizationTests
{
    [Fact]
    public void Vector_and_scalar_sums_agree()
    {
        var values = Enumerable.Range(1, 1_003).Select(i => (float)i).ToArray();
        var expected = values.Sum();
        Assert.Equal(expected, Signal.SumVector128(values), 0.5f);
        Assert.Equal(expected, Signal.Sum(values), 0.5f);
    }

    [Fact]
    public void Normalizes_and_compares()
    {
        float[] v = [3, 4];
        Signal.Normalize(v);
        Assert.Equal([0.6f, 0.8f], v);
        Assert.Equal(1f, Signal.Similarity(v, [6, 8]), 1e-6f);
    }
}

public class P072_AotFriendlyTests
{
    [Fact]
    public void Both_factories_create_formatters()
    {
        Assert.Equal("1,2", Formatters.Create<CsvFormatter>().Format([1, 2]));
        Assert.IsType<CsvFormatter>(Formatters.FromName("CSV"));
        Assert.Null(Formatters.FromName("pdf"));
    }
}

public class P073_MetricsTests
{
    [Fact]
    public void Publishes_counter_values()
    {
        using var metrics = new OrderMetrics();
        long placed = 0;
        string? region = null;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Meter.Name == OrderMetrics.MeterName) l.EnableMeasurementEvents(inst);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, _) =>
        {
            placed += value;
            region = tags[0].Value as string;
        });
        listener.Start();

        metrics.OrderPlaced("eu", TimeSpan.FromMilliseconds(120));
        metrics.OrderPlaced("eu", TimeSpan.FromMilliseconds(80));

        Assert.Equal(2, placed);
        Assert.Equal("eu", region);
    }
}

public class P075_SourceGeneratorTests
{
    [Fact]
    public void Generated_method_lists_properties()
    {
        var invoice = new Invoice
        {
            Number = "INV-7", Total = 12.5m, Due = new DateOnly(2026, 12, 1),
        };
        Assert.Equal(
            "Invoice { Number = INV-7, Total = 12.5, Due = 12/01/2026 }",
            invoice.Describe());
    }
}
