namespace PresenzeXmlConverter.Tests;

public class DashboardLayoutTests
{
    [Fact]
    public void AnEmptyDashboardShowsOnlyTheConversionCard()
    {
        var layout = DashboardLayout.For(hasRequests: false, hasPreview: false, hasExports: false);
        Assert.False(layout.ShowMetrics);
        Assert.False(layout.ShowBottom);
        Assert.False(layout.ShowInsights);
    }

    [Fact]
    public void PastExportsAloneShowTheirListWithoutTheMonthAnalysis()
    {
        var layout = DashboardLayout.For(false, false, true);
        Assert.False(layout.ShowMetrics);
        Assert.True(layout.ShowBottom);
        Assert.False(layout.ShowInsights);
    }

    [Fact]
    public void AFileWithoutPreviewShowsTheIndicatorsOnly()
    {
        var layout = DashboardLayout.For(true, false, false);
        Assert.True(layout.ShowMetrics);
        Assert.False(layout.ShowBottom);
    }

    [Fact]
    public void AReadyPreviewShowsEverything()
    {
        var layout = DashboardLayout.For(true, true, true);
        Assert.True(layout.ShowMetrics);
        Assert.True(layout.ShowBottom);
        Assert.True(layout.ShowInsights);
    }
}
