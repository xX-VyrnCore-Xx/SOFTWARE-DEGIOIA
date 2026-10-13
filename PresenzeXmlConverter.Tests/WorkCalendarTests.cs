namespace PresenzeXmlConverter.Tests;

/// <summary>Calendario e ore effettive ricavati dalle date di una richiesta.</summary>
public class WorkCalendarTests
{
    private static readonly GenerationOptions Auto = new(8m);

    private static WorkPlan Plan(string from, string to, decimal hours, GenerationOptions? options = null) =>
        WorkCalendar.Plan(DateTime.Parse(from), DateTime.Parse(to), hours, options ?? Auto);

    [Fact]
    public void FortyHoursFromMondayToSaturdayStayOnFiveWeekdays()
    {
        // 17/08/2026 è lunedì, 22/08 sabato: le 40 ore stanno nei 5 giorni feriali.
        var plan = Plan("2026-08-17", "2026-08-22", 40m);

        Assert.Equal(5, plan.Days.Count);
        Assert.DoesNotContain(plan.Days, day => day.DayOfWeek == DayOfWeek.Saturday);
        Assert.Equal(40m, plan.Hours);
        Assert.Null(plan.Note);
    }

    [Fact]
    public void FortyEightHoursNeedSaturdayToFitTheDay()
    {
        var plan = Plan("2026-08-17", "2026-08-22", 48m);

        Assert.Equal(6, plan.Days.Count);
        Assert.Contains(plan.Days, day => day.DayOfWeek == DayOfWeek.Saturday);
        Assert.Contains("sabato", plan.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void ASaturdayOnlyRequestIsKeptInsteadOfBeingDiscarded()
    {
        // 22/08/2026 è sabato.
        var plan = Plan("2026-08-22", "2026-08-22", 8m);

        Assert.Equal([new DateTime(2026, 8, 22)], plan.Days);
        Assert.Contains("sabato", plan.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void ADurationEqualToTheNumberOfDaysIsConvertedToHours()
    {
        // 5 "ore" su cinque giorni feriali: sono 5 giorni, cioè 40 ore.
        var plan = Plan("2026-08-17", "2026-08-21", 5m);

        Assert.Equal(40m, plan.Hours);
        Assert.Equal(5, plan.Days.Count);
        Assert.Contains("giorni", plan.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void ASingleDayDurationOfOneIsLeftAsOneHour()
    {
        // Un giorno solo: 1 può essere 1 ora di permesso, non si indovina.
        var plan = Plan("2026-08-10", "2026-08-10", 1m);

        Assert.Equal(1m, plan.Hours);
        Assert.Null(plan.Note);
    }

    [Fact]
    public void FullDaysAreNotMistakenForDaysCounts()
    {
        // 8 ore su 8 giorni feriali non è «8 giorni» (8 non è meno della giornata).
        var plan = Plan("2026-08-03", "2026-08-14", 8m);

        Assert.Equal(8m, plan.Hours);
    }

    [Fact]
    public void TheSaturdaySettingAlwaysCountsSaturday()
    {
        var plan = Plan("2026-08-17", "2026-08-22", 40m, new GenerationOptions(8m, IncludeSaturdays: true));

        Assert.Equal(6, plan.Days.Count);
    }

    [Fact]
    public void WithoutAutoDetectionNothingIsGuessed()
    {
        var off = new GenerationOptions(8m, AutoDetect: false);

        var overflow = Plan("2026-08-17", "2026-08-22", 48m, off);
        var saturdayOnly = Plan("2026-08-22", "2026-08-22", 8m, off);
        var daysCount = Plan("2026-08-17", "2026-08-21", 5m, off);

        Assert.Equal(5, overflow.Days.Count);
        Assert.Empty(saturdayOnly.Days);
        Assert.Equal(5m, daysCount.Hours);
    }

    [Fact]
    public void TheGeneratorUsesThePlan()
    {
        var requests = new[]
        {
            new AttendanceRequest("Mario Rossi", "Ferie", new DateTime(2026, 8, 17), new DateTime(2026, 8, 22), 48m, 3),
            new AttendanceRequest("Anna Verdi", "Permesso", new DateTime(2026, 8, 22), new DateTime(2026, 8, 22), 4m, 4)
        };

        var result = AttendanceMovementGenerator.Generate(
            requests,
            new Dictionary<string, string> { ["Mario Rossi"] = "1001", ["Anna Verdi"] = "1002" },
            new Dictionary<string, string> { ["Ferie"] = "01", ["Permesso"] = "02" },
            null,
            null,
            Auto);

        Assert.Equal(6, result.Movements.Count(movement => movement.EmployeeCode == "1001"));
        Assert.All(result.Movements.Where(movement => movement.EmployeeCode == "1001"), movement => Assert.Equal(8, movement.Hours));
        var saturday = Assert.Single(result.Movements, movement => movement.EmployeeCode == "1002");
        Assert.Equal(4, saturday.Hours);
        Assert.Contains(result.Warnings, warning => warning.Contains("sabato", StringComparison.Ordinal));
    }
}
