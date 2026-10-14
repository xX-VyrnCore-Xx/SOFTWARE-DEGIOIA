using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

/// <summary>Anomalie che il calendario segnala per non farle dimenticare.</summary>
public class CalendarAlertsTests
{
    private static DailyMovement Movement(string code, DateTime date, int hours, int minutes = 0, string policy = "Ferie") =>
        new("Rossi Mario", "0000001", policy, code, date, hours, minutes);

    private static DailyMovement Presence(DateTime date, int hours) =>
        new("Rossi Mario", "0000001", AttendanceMovementGenerator.PresencePolicy, "01", date, hours, 0);

    [Fact]
    public void ADayAboveTheStandardHoursNeedsChecking()
    {
        // 40 ore su 3 giorni: 13h 20m al giorno.
        var movements = new[]
        {
            Movement("FE", new DateTime(2026, 9, 7), 13, 20),
            Movement("FE", new DateTime(2026, 9, 8), 13, 20)
        };

        var alerts = CalendarAlerts.Find(movements, GenerationOptions.Default, null);

        Assert.Equal(2, alerts.Count);
        Assert.All(alerts, alert => Assert.Equal(AlertSeverity.Check, alert.Severity));
        Assert.Contains("13h 20m", alerts[0].Text);
        Assert.Contains("8h", alerts[0].Text);
    }

    [Fact]
    public void ANormalDayIsNotFlaggedAndPresenceRowsDoNotCountAsAnotherCode()
    {
        var day = new DateTime(2026, 9, 9);
        var movements = new[] { Movement("FE", day, 4), Presence(day, 4) };

        Assert.Empty(CalendarAlerts.Find(movements, new GenerationOptions(FillPresence: true), null));
    }

    [Fact]
    public void ADayWithTwoDifferentCodesIsOnlyANotice()
    {
        var day = new DateTime(2026, 9, 10);
        var movements = new[] { Movement("FE", day, 4), Movement("PF", day, 2, policy: "Permesso") };

        var alert = Assert.Single(CalendarAlerts.Find(movements, GenerationOptions.Default, null));

        Assert.Equal(AlertSeverity.Notice, alert.Severity);
        Assert.Contains("FE, PF", alert.Text);
    }

    [Fact]
    public void WeekendAndHolidayMovementsAreNoticesUnlessSaturdaysAreWorkingDays()
    {
        var saturday = new DateTime(2026, 9, 12);
        var sunday = new DateTime(2026, 9, 13);
        var movements = new[] { Movement("FE", saturday, 8), Movement("FE", sunday, 8) };

        var normal = CalendarAlerts.Find(movements, GenerationOptions.Default, null);
        var withSaturdays = CalendarAlerts.Find(movements, new GenerationOptions(IncludeSaturdays: true), null);

        Assert.Equal(2, normal.Count);
        Assert.Contains(normal, alert => alert.Text.Contains("sabato"));
        Assert.Contains(normal, alert => alert.Text.Contains("domenica"));
        var onlySunday = Assert.Single(withSaturdays);
        Assert.Contains("domenica", onlySunday.Text);
    }

    [Fact]
    public void ThePartTimeDayOfTheEmployeeIsTheReference()
    {
        var day = new DateTime(2026, 9, 14);
        var movements = new[] { Movement("FE", day, 6) };
        var profiles = new Dictionary<string, EmployeeProfile> { ["Rossi Mario"] = new(4m, null, null) };

        var alert = Assert.Single(CalendarAlerts.Find(movements, GenerationOptions.Default, profiles));

        Assert.Equal(AlertSeverity.Check, alert.Severity);
        Assert.Contains("4h", alert.Text);
    }
}
