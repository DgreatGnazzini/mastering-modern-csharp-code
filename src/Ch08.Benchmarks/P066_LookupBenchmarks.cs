using System.Collections.Frozen;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace MasteringModernCSharp.Ch08.Benchmarks;

// Which collection should hold a set of allowed IDs?
// Measure it instead of guessing.
[MemoryDiagnoser]
[ShortRunJob]
public class LookupBenchmarks
{
    private List<int> _list = [];
    private HashSet<int> _hashSet = [];
    private FrozenSet<int> _frozen = FrozenSet<int>.Empty;
    private int[] _queries = [];

    [Params(10, 1_000)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var ids = Enumerable.Range(0, Size).Select(i => i * 7).ToArray();
        _list = [.. ids];
        _hashSet = [.. ids];
        _frozen = ids.ToFrozenSet();
        _queries = Enumerable.Range(0, 100).Select(i => i * 5).ToArray();
    }

    [Benchmark(Baseline = true)]
    public int List() => _queries.Count(_list.Contains);

    [Benchmark]
    public int HashSet() => _queries.Count(_hashSet.Contains);

    [Benchmark]
    public int Frozen() => _queries.Count(_frozen.Contains);
}

public static class Program
{
    // dotnet run -c Release --project src/Ch08.Benchmarks
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
            .Run(args);
}
