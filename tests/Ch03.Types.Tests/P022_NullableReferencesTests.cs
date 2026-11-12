using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MasteringModernCSharp.Ch03.Tests;

public class P022_NullableReferencesTests
{
    private static SubscriberDirectory Directory()
    {
        var directory = new SubscriberDirectory();
        directory.Add(new("1", "Ada", "ada@example.com"));
        directory.Add(new("2", "Lin", null));
        return directory;
    }

    [Fact]
    public void Find_returns_null_when_missing() =>
        Assert.Null(Directory().Find("99"));

    [Fact]
    public void TryFind_sets_the_out_value_on_success()
    {
        Assert.True(Directory().TryFind("1", out var found));
        Assert.Equal("Ada", found.Name);   // no warning: NotNullWhen
        Assert.False(Directory().TryFind("99", out _));
    }

    [Fact]
    public void Fallback_is_used_for_missing_email()
    {
        Assert.Equal("ada@example.com",
                     Directory().EmailOf("1", "none"));
        Assert.Equal("none", Directory().EmailOf("2", "none"));
        Assert.Null(Directory().EmailOf("2", null));
    }

    [Fact]
    public void Public_API_still_checks_null_at_run_time() =>
        Assert.Throws<ArgumentNullException>(
            () => new SubscriberDirectory().Add(null!));

    [Fact]
    public void Newsletter_handles_missing_email()
    {
        var newsletter = new Newsletter();
        Assert.Throws<InvalidOperationException>(
            () => newsletter.Compose(new("1", "Ada", "a@b.c")));

        newsletter.Configure("news@shop.example");
        Assert.True(newsletter.IsConfigured);
        Assert.Contains("To: a@b.c",
            newsletter.Compose(new("1", "Ada", "a@b.c")));
        Assert.Equal("(no email for Lin)",
            newsletter.Compose(new("2", "Lin", null)));
    }

    // Compiles a snippet and returns the nullable warnings.
    private static string[] NullableWarnings(string code)
    {
        var references = ((string)AppContext.GetData(
                "TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("Snippet",
            [CSharpSyntaxTree.ParseText(code)], references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        return [.. compilation.GetDiagnostics()
            .Where(d => d.Id.StartsWith("CS86"))
            .Select(d => d.Id)];
    }

    #region p022
    [Fact]
    public void Compiler_warns_about_unchecked_null()
    {
        const string code = """
            #nullable enable
            class C
            {
                string? Find() => null;
                int Bad() => Find().Length;          // CS8602
                int Good() => Find()?.Length ?? 0;
                void Assign() { string s = Find(); } // CS8600
            }
            """;
        Assert.Equal(["CS8602", "CS8600"], NullableWarnings(code));
    }
    #endregion
}
