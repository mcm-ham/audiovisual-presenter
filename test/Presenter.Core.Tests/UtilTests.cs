namespace Presenter.Core.Tests;

public class UtilTests
{
    [Theory]
    [InlineData("bottom left", "Bottom Left")]
    [InlineData("HELLO", "Hello")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void ToFirstUpper_Works(string? input, string expected)
    {
        Assert.Equal(expected, input.ToFirstUpper());
    }

    [Fact]
    public void Parse_ValidInt_Parses()
    {
        Assert.Equal(42, "42".Parse<int?>());
    }

    [Fact]
    public void Parse_Invalid_ReturnsDefault()
    {
        Assert.Null("abc".Parse<int?>());
        Assert.Null(((object?)null).Parse<int?>());
    }

    [Theory]
    [InlineData(0, 0, 5, false, "5")]
    [InlineData(0, 2, 30, false, "2:30")]
    [InlineData(1, 5, 3, false, "01:05:03")]
    [InlineData(0, 0, 9, true, "+9")]
    public void FormatTimeSpan_Works(int h, int m, int s, bool sign, string expected)
    {
        Assert.Equal(expected, new TimeSpan(h, m, s).FormatTimeSpan(sign));
    }

    [Fact]
    public void FindIndex_ReturnsMinusOneWhenNotFound()
    {
        Assert.Equal(-1, new[] { 1, 2, 3 }.FindIndex(i => i == 9));
        Assert.Equal(1, new[] { 1, 2, 3 }.FindIndex(i => i == 2));
    }

    [Fact]
    public void ToNullIfEmpty_Works()
    {
        Assert.Null("".ToNullIfEmpty());
        Assert.Equal("x", "x".ToNullIfEmpty());
    }
}
