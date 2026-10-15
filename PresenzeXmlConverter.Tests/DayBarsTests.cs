using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public class DayBarsTests
{
    private static readonly DayTotal[] Week =
    [
        new(new DateTime(2026, 9, 1), 480, 0),
        new(new DateTime(2026, 9, 2), 240, 240),
        new(new DateTime(2026, 9, 4), 0, 480),
        new(new DateTime(2026, 9, 7), 120, 0)
    ];

    [Fact]
    public void NoDataOrNoRoomGivesNoBars()
    {
        Assert.Empty(DayBars.Layout([], 200, 50, 1f));
        Assert.Empty(DayBars.Layout(Week, 0, 50, 1f));
        Assert.Empty(DayBars.Layout(Week, 200, 0, 1f));
    }

    [Fact]
    public void ColumnsFollowCalendarDaysSoWeekendsStayEmpty()
    {
        var bars = DayBars.Layout(Week, 700, 50, 1f);
        Assert.Equal(4, bars.Count);
        // 1-7 settembre: sette colonne da 100 px; il 3, 5 e 6 restano vuoti.
        Assert.Equal(0, bars[0].X);
        Assert.Equal(100, bars[1].X);
        Assert.Equal(300, bars[2].X);
        Assert.Equal(600, bars[3].X);
    }

    [Fact]
    public void TallestDayFillsThePlotAndOthersAreProportional()
    {
        var bars = DayBars.Layout(Week, 700, 60, 1f);
        Assert.Equal(60, bars[0].Height);
        Assert.Equal(60, bars[1].Height);
        Assert.Equal(15, bars[3].Height);
    }

    [Fact]
    public void AbsencePartIsProportionalAndNeverOverflows()
    {
        var bars = DayBars.Layout(Week, 700, 60, 1f);
        Assert.Equal(30, bars[1].AbsenceHeight);
        Assert.Equal(30, bars[1].PresenceHeight);
        Assert.Equal(60, bars[2].AbsenceHeight);
        Assert.Equal(0, bars[2].PresenceHeight);
        Assert.Equal(0, bars[0].AbsenceHeight);
    }

    [Fact]
    public void TinyDaysStillShowOnePixel()
    {
        var days = new DayTotal[] { new(new DateTime(2026, 9, 1), 6000, 0), new(new DateTime(2026, 9, 2), 1, 0) };
        Assert.Equal(1, DayBars.Layout(days, 100, 40, 1f)[1].Height);
    }

    [Fact]
    public void BarsAreAlwaysInsideThePlot()
    {
        var bars = DayBars.Layout(Week, 333, 47, 1f);
        Assert.All(bars, bar =>
        {
            Assert.InRange(bar.X, 0, 333);
            Assert.True(bar.X + bar.Width <= 333);
            Assert.InRange(bar.Height, 1, 47);
        });
    }

    [Fact]
    public void GrowStartsFlatAndEndsFull()
    {
        Assert.All(DayBars.Layout(Week, 700, 60, 0f), bar => Assert.Equal(1, bar.Height));
        Assert.Equal(60, DayBars.Layout(Week, 700, 60, 1f)[0].Height);
    }

    [Fact]
    public void CascadeStartsOnTheLeft()
    {
        Assert.True(DayBars.Progress(0.3f, 0, 10) > DayBars.Progress(0.3f, 9, 10));
        Assert.Equal(1f, DayBars.Progress(1f, 9, 10), 3);
        Assert.Equal(0f, DayBars.Progress(0f, 0, 10), 3);
        Assert.Equal(1f, DayBars.Progress(0.6f, 0, 1), 3);
    }

    [Fact]
    public void InsightsCountEveryDayWithPresenceAndAbsence()
    {
        var movements = new[]
        {
            new DailyMovement("Rossi", "1", AttendanceMovementGenerator.PresencePolicy, "01", new DateTime(2026, 9, 1), 8, 0),
            new DailyMovement("Verdi", "2", AttendanceMovementGenerator.PresencePolicy, "01", new DateTime(2026, 9, 1), 4, 30),
            new DailyMovement("Verdi", "2", "Ferie", "FE", new DateTime(2026, 9, 1), 3, 30),
            new DailyMovement("Rossi", "1", "Ferie", "FE", new DateTime(2026, 9, 3), 8, 0)
        };
        var insights = DashboardInsights.From(movements, []);
        Assert.Equal(2, insights.Days.Count);
        Assert.Equal(new DayTotal(new DateTime(2026, 9, 1), (12 * 60) + 30, 210), insights.Days[0]);
        Assert.Equal(new DayTotal(new DateTime(2026, 9, 3), 0, 480), insights.Days[1]);
        Assert.False(insights.SameAs(DashboardInsights.From(movements.Take(3).ToArray(), [])));
    }
}
