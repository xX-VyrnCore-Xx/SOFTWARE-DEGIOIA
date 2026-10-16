namespace PresenzeXmlConverter.Tests;

public class TrendChipTests
{
    [Fact]
    public void MoreHoursThanLastTimeIsAPositiveRoundedPercentage()
    {
        var trend = TrendChip.Compare(7842 * 60, 7606 * 60);
        Assert.NotNull(trend);
        Assert.Equal(TrendTone.Up, trend!.Tone);
        Assert.Equal("+3,1% sull'ultima esportazione", trend.Text);
    }

    [Fact]
    public void FewerHoursIsANegativePercentage()
    {
        var trend = TrendChip.Compare(900, 1000);
        Assert.Equal(TrendTone.Down, trend!.Tone);
        Assert.Equal("−10,0% sull'ultima esportazione", trend.Text);
    }

    [Fact]
    public void SameTotalIsNeutral()
    {
        var trend = TrendChip.Compare(1000, 1000);
        Assert.Equal(TrendTone.Same, trend!.Tone);
        Assert.Equal("come l'ultima esportazione", trend.Text);
    }

    [Theory]
    [InlineData(1000, null)]
    [InlineData(1000, 0)]
    [InlineData(0, 1000)]
    [InlineData(-5, 1000)]
    public void NoSensibleComparisonGivesNothing(int current, int? previous) =>
        Assert.Null(TrendChip.Compare(current, previous));

    [Fact]
    public void TinyChangesRoundToTheNearestTenth()
    {
        // 10,04 % → 10,0 ; 0,04 % → «come l'ultima».
        Assert.Equal("+10,0% sull'ultima esportazione", TrendChip.Compare(110040, 100000)!.Text);
        Assert.Equal(TrendTone.Same, TrendChip.Compare(100040, 100000)!.Tone);
    }
}
