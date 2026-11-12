using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace MasteringModernCSharp.Ch08.Generators;

[Generator]
public sealed class DescribeGenerator : IIncrementalGenerator
{
    private const string AttributeName =
        "MasteringModernCSharp.Ch08.AutoDescribeAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext ctx)
    {
        // 1. Add the marker attribute to the user's compilation.
        ctx.RegisterPostInitializationOutput(static post =>
            post.AddSource("AutoDescribeAttribute.g.cs", """
                namespace MasteringModernCSharp.Ch08;

                [System.AttributeUsage(System.AttributeTargets.Class)]
                internal sealed class AutoDescribeAttribute
                    : System.Attribute;
                """));

        // 2. Find marked types, and keep only plain, equatable data
        //    so unchanged types are cached between keystrokes.
        var types = ctx.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            predicate: static (node, _) => true,
            transform: static (c, _) =>
                ToModel((INamedTypeSymbol)c.TargetSymbol));

        // 3. Emit one file per type.
        ctx.RegisterSourceOutput(types, static (spc, model) =>
            spc.AddSource($"{model.Name}.Describe.g.cs", Emit(model)));
    }

    private static TypeModel ToModel(INamedTypeSymbol type) => new(
        type.ContainingNamespace.ToDisplayString(),
        type.Name,
        new EquatableArray(type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public
                        && !p.IsStatic && p.GetMethod is not null)
            .Select(p => p.Name)
            .ToImmutableArray()));

    private static string Emit(TypeModel m)
    {
        var parts = string.Join(", ",
            m.Properties.Items.Select(p => $"{p} = {{{p}}}"));
        return $$$"""
            namespace {{{m.Namespace}}};

            partial class {{{m.Name}}}
            {
                public string Describe() =>
                    string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"{{{m.Name}}} {{ {{{parts}}} }}");
            }
            """;
    }

    private sealed record TypeModel(
        string Namespace, string Name, EquatableArray Properties);

    // ImmutableArray compares by reference; wrap it so the
    // incremental pipeline can tell when nothing changed.
    private sealed class EquatableArray(ImmutableArray<string> items)
        : System.IEquatable<EquatableArray>
    {
        public ImmutableArray<string> Items { get; } = items;

        public bool Equals(EquatableArray? other) =>
            other is not null && Items.SequenceEqual(other.Items);

        public override bool Equals(object? obj) =>
            Equals(obj as EquatableArray);

        public override int GetHashCode() =>
            Items.Aggregate(17, (h, s) => h * 31 + s.GetHashCode());
    }
}
