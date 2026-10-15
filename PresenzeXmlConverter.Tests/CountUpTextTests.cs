using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public sealed class CountUpTextTests
{
    [Fact]
    public void EndpointsAreExact()
    {
        Assert.Equal("0", CountUpText.Frame("0", "0", 0.5f));
        Assert.Equal("1,234", CountUpText.Frame("0", "1,234", 1f));
        Assert.Equal("176h 30m", CountUpText.Frame("0h 00m", "176h 30m", 1.5f));
        Assert.Equal("0", CountUpText.Frame("0", "1,234", 0f));
    }

    [Fact]
    public void ThousandsKeepTheirSeparatorWhileCounting()
    {
        Assert.Equal("1,500", CountUpText.Frame("0", "3,000", 0.5f));
        Assert.Equal("600", CountUpText.Frame("0", "1,200", 0.5f));
    }

    [Fact]
    public void EachNumberCountsOnItsOwnAndKeepsLeadingZeros()
    {
        Assert.Equal("88h 15m", CountUpText.Frame("0h 00m", "176h 30m", 0.5f));
        Assert.Equal("10h 05m", CountUpText.Frame("0h 00m", "20h 10m", 0.5f));
    }

    [Fact]
    public void CountsDownWhenTheNewValueIsSmaller()
    {
        Assert.Equal("60", CountUpText.Frame("100", "20", 0.5f));
    }

    [Fact]
    public void MissingStartNumbersCountAsZeroAndTextWithoutNumbersIsUntouched()
    {
        Assert.Equal("5h 00m", CountUpText.Frame("nessun file", "10h 00m", 0.5f));
        Assert.Equal("nessun file", CountUpText.Frame("1", "nessun file", 0.3f));
        Assert.Equal(string.Empty, CountUpText.Frame("1", string.Empty, 0.3f));
    }

    [Fact]
    public void ValueNeverLeavesTheRangeBetweenStartAndEnd()
    {
        for (var step = 0; step <= 20; step++)
        {
            var shown = CountUpText.Frame("0", "9,876", step / 20f).Replace(",", string.Empty);
            var number = int.Parse(shown);
            Assert.InRange(number, 0, 9876);
        }
    }
}
