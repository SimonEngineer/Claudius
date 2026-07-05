using Weaver.Scraping;
using Xunit;

namespace Weaver.Tests;

public class FieldTransformsTests
{
    private static string? Apply(string? value, params FieldTransform[] transforms) =>
        FieldTransforms.Apply(value, transforms);

    [Fact]
    public void Trim_Lowercase_Uppercase_Chain()
    {
        Assert.Equal("hello", Apply("  HeLLo  ", new FieldTransform("trim"), new FieldTransform("lowercase")));
        Assert.Equal("HELLO", Apply("hello", new FieldTransform("uppercase")));
    }

    [Fact]
    public void StripHtml_RemovesTags()
    {
        Assert.Equal("Sale 20% off", Apply("<b>Sale</b> <i>20% off</i>", new FieldTransform("stripHtml")));
    }

    [Fact]
    public void RegexExtract_WholeMatch_AndFirstGroup()
    {
        Assert.Equal("42", Apply("Order #42 shipped", new FieldTransform("regexExtract", @"\d+")));
        Assert.Equal("42", Apply("Order #42 shipped", new FieldTransform("regexExtract", @"#(\d+)")));
        Assert.Null(Apply("no digits here", new FieldTransform("regexExtract", @"\d+")));
    }

    [Fact]
    public void RegexExtract_InvalidPattern_PassesThrough()
    {
        Assert.Equal("value", Apply("value", new FieldTransform("regexExtract", "([")));
    }

    [Theory]
    [InlineData("$1,234.56", "1234.56")]
    [InlineData("1.234,56 kr", "1234.56")]
    [InlineData("Price: 99", "99")]
    [InlineData("1,234", "1234")]
    [InlineData("-12.5", "-12.5")]
    public void ParseNumber_NormalizesFormats(string input, string expected)
    {
        Assert.Equal(expected, Apply(input, new FieldTransform("parseNumber")));
    }

    [Fact]
    public void ParseNumber_NoDigits_YieldsNull()
    {
        Assert.Null(Apply("sold out", new FieldTransform("parseNumber")));
    }

    [Fact]
    public void NullInput_StaysNull_AndUnknownKindPassesThrough()
    {
        Assert.Null(Apply(null, new FieldTransform("trim")));
        Assert.Equal("x", Apply("x", new FieldTransform("someFutureKind")));
    }

    [Fact]
    public void Parse_MalformedJson_YieldsEmptyList()
    {
        Assert.Empty(FieldTransforms.Parse("not json"));
        Assert.Empty(FieldTransforms.Parse(null));
        Assert.Single(FieldTransforms.Parse("""[{"kind":"trim"}]"""));
    }
}
