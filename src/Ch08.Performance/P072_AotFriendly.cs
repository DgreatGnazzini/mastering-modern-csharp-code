using System.Diagnostics.CodeAnalysis;

namespace MasteringModernCSharp.Ch08;

public interface IReportFormatter
{
    string Format(IReadOnlyList<decimal> values);
}

public sealed class CsvFormatter : IReportFormatter
{
    public string Format(IReadOnlyList<decimal> values) =>
        string.Join(",", values);
}

public static class Formatters
{
    // The annotation tells the trimmer to keep the public
    // parameterless constructor of whatever T is used here.
    public static IReportFormatter Create<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        T>()
        where T : IReportFormatter =>
        (IReportFormatter)Activator.CreateInstance(typeof(T))!;

    // Better still: no reflection at all. A lookup of factories
    // is trim-safe and AOT-safe by construction.
    private static readonly Dictionary<string, Func<IReportFormatter>>
        ByName = new(StringComparer.OrdinalIgnoreCase)
        {
            ["csv"] = () => new CsvFormatter(),
        };

    public static IReportFormatter? FromName(string name) =>
        ByName.TryGetValue(name, out var create) ? create() : null;
}
