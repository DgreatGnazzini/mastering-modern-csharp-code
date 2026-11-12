using MasteringModernCSharp.Ch02;

namespace MasteringModernCSharp.Ch02.Tests;

public class P007_ExtensionPropertiesTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("hello", false)]
    public void IsBlank_detects_empty_and_whitespace(string input, bool expected) =>
        Assert.Equal(expected, input.IsBlank);

    [Fact]
    public void WordCount_ignores_extra_whitespace() =>
        Assert.Equal(3, "  modern   C#\tbook ".WordCount);

    [Fact]
    public void Truncate_adds_ellipsis_only_when_needed()
    {
        Assert.Equal("short", "short".Truncate(10));
        Assert.Equal("Masteri…", "Mastering Modern C#".Truncate(8));
    }
}
