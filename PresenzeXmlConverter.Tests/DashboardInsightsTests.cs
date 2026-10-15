using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

/// <summary>I numeri dell'analisi nella dashboard: presenza, assenze per giustificativo e giorni da controllare.</summary>
public class DashboardInsightsTests
{
    private static DailyMovement Absence(string code, string policy, int day, int hours) =>
        new("Rossi Mario", "0000001", policy, code, new DateTime(2026, 9, day), hours, 0);

    private static DailyMovement Presence(int day, int hours) =>
        new("Rossi Mario", "0000001", AttendanceMovementGenerator.PresencePolicy, "01", new DateTime(2026, 9, day), hours, 0);

    [Fact]
    public void WithoutMovementsThereIsNothingToShow()
    {
        var insights = DashboardInsights.From([], []);

        Assert.False(insights.HasData);
        Assert.Equal(0, insights.AbsenceRatio);
    }

    [Fact]
    public void PresenceAndAbsencesAreSeparatedAndSortedByWeight()
    {
        var movements = new[]
        {
            Absence("PF", "Permesso", 2, 2),
            Absence("FE", "Ferie", 3, 8),
            Absence("FE", "Ferie", 4, 8),
            Presence(7, 8),
            Presence(8, 8)
        };

        var insights = DashboardInsights.From(movements, []);

        Assert.Equal(16 * 60, insights.PresenceMinutes);
        Assert.Equal(18 * 60, insights.AbsenceMinutes);
        Assert.Equal(["FE", "PF"], insights.Absences.Select(share => share.Code));
        Assert.Equal(16 * 60, insights.Absences[0].Minutes);
        Assert.Equal(18.0 / 34.0, insights.AbsenceRatio, 3);
    }

    [Fact]
    public void ColorsFollowTheOrderOfFirstAppearanceLikeTheCalendar()
    {
        var movements = new[]
        {
            Absence("PF", "Permesso", 2, 2),
            Absence("FE", "Ferie", 3, 8)
        };

        var insights = DashboardInsights.From(movements, []);

        // «PF» compare per primo nei movimenti: colore 0 anche se pesa meno.
        Assert.Equal(0, insights.Absences.Single(share => share.Code == "PF").ColorIndex);
        Assert.Equal(1, insights.Absences.Single(share => share.Code == "FE").ColorIndex);
    }

    [Fact]
    public void DaysToCheckAreCountedOncePerEmployeeAndDay()
    {
        var day = new DateTime(2026, 9, 7);
        var alerts = new[]
        {
            new CalendarAlert("Rossi Mario", day, AlertSeverity.Check, "oltre la giornata"),
            new CalendarAlert("Rossi Mario", day, AlertSeverity.Notice, "più giustificativi"),
            new CalendarAlert("Rossi Mario", day.AddDays(1), AlertSeverity.Notice, "sabato"),
            new CalendarAlert("Bianchi Luca", day, AlertSeverity.Notice, "domenica")
        };

        var insights = DashboardInsights.From([Presence(7, 8)], alerts);

        Assert.Equal(1, insights.CheckDays);
        Assert.Equal(2, insights.NoticeDays);
    }

    [Fact]
    public void SameAsComparesNumbersAndAbsencesNotListReferences()
    {
        var shares = new[] { new AbsenceShare("FE", "Ferie", 480, 0) };
        var first = new DashboardInsights(960, 480, shares, 1, 2);
        var second = new DashboardInsights(960, 480, new[] { new AbsenceShare("FE", "Ferie", 480, 0) }, 1, 2);

        Assert.True(first.SameAs(second));
        Assert.False(first.SameAs(first with { CheckDays = 2 }));
        Assert.False(first.SameAs(first with { Absences = [] }));
    }
}
