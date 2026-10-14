using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

/// <summary>Riepilogo mensile: totali per dipendente, ore teoriche e giorni scoperti, in CSV per Excel.</summary>
public class MonthlySummaryTests
{
    private static GenerationResult Run(GenerationOptions options, params AttendanceRequest[] requests) =>
        AttendanceMovementGenerator.Generate(
            requests,
            new Dictionary<string, string> { ["Rossi Mario"] = "0000001" },
            new Dictionary<string, string> { ["Ferie"] = "FE" },
            null,
            null,
            options);

    private static AttendanceRequest Holiday(DateTime start, DateTime end, decimal hours) =>
        new("Rossi Mario", "Ferie", start, end, hours, 4);

    [Fact]
    public void TotalsMatchTheTheoreticalHoursWhenNothingIsMissing()
    {
        var result = Run(
            new GenerationOptions(FillPresence: true),
            Holiday(new DateTime(2026, 9, 15), new DateTime(2026, 9, 17), 24m));

        var row = Assert.Single(MonthlySummary.Build(result.Movements, new GenerationOptions(FillPresence: true), null, null, null));

        Assert.Equal("Rossi Mario", row.EmployeeName);
        Assert.Equal(19 * 480, row.PresenceMinutes);
        Assert.Equal(3 * 480, row.AbsenceMinutes);
        Assert.Equal(22 * 480, row.ExpectedMinutes);
        Assert.Equal(0, row.UncoveredDays);
        var absence = Assert.Single(row.Absences);
        Assert.Equal("FE", absence.Code);
        Assert.Equal(3 * 480, absence.Minutes);
    }

    [Fact]
    public void DaysWithoutAnyMovementAreCountedAsUncovered()
    {
        var movements = Enumerable.Range(1, 20)
            .Select(day => new DateTime(2026, 9, day))
            .Where(date => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .Select(date => new DailyMovement("Rossi Mario", "0000001", AttendanceMovementGenerator.PresencePolicy, "01", date, 8, 0))
            .ToArray();

        var row = Assert.Single(MonthlySummary.Build(movements, new GenerationOptions(FillPresence: true), null, null, null));

        // Settembre 2026: 22 giorni lavorativi, 14 coperti fino al 20 → 8 scoperti.
        Assert.Equal(14, movements.Length);
        Assert.Equal(8, row.UncoveredDays);
        Assert.Equal(22 * 480, row.ExpectedMinutes);
    }

    [Fact]
    public void WithoutPresenceFillTheoreticalHoursAreNotReported()
    {
        var result = Run(
            new GenerationOptions(),
            Holiday(new DateTime(2026, 9, 15), new DateTime(2026, 9, 15), 8m));

        var row = Assert.Single(MonthlySummary.Build(result.Movements, new GenerationOptions(), null, null, null));

        Assert.Null(row.ExpectedMinutes);
        Assert.Null(row.UncoveredDays);
        Assert.Equal(0, row.PresenceMinutes);
        Assert.Equal(480, row.AbsenceMinutes);
    }

    [Fact]
    public void EmploymentRangeLimitsTheTheoreticalHours()
    {
        var movements = new[]
        {
            new DailyMovement("Rossi Mario", "0000001", AttendanceMovementGenerator.PresencePolicy, "01", new DateTime(2026, 9, 21), 8, 0)
        };
        var profiles = new Dictionary<string, EmployeeProfile>
        {
            ["Rossi Mario"] = new(null, new DateTime(2026, 9, 21), new DateTime(2026, 9, 22))
        };

        var row = Assert.Single(MonthlySummary.Build(movements, new GenerationOptions(FillPresence: true), profiles, null, null));

        Assert.Equal(2 * 480, row.ExpectedMinutes);
        Assert.Equal(1, row.UncoveredDays);
    }

    [Fact]
    public void CsvUsesSemicolonsDecimalCommaAndQuotesAwkwardFields()
    {
        var rows = new[]
        {
            new MonthlySummaryRow(
                "Rossi; Mario", "0000001", new DateTime(2026, 9, 1), 450, 480, 600, 1,
                new[] { ("FE", "Ferie", 480) })
        };

        var lines = MonthlySummary.ToCsv(rows).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Dipendente;Codice;Mese;Ore presenza;", lines[0]);
        Assert.Equal(
            "\"Rossi; Mario\";0000001;Settembre 2026;7,5;8;15,5;10;+5,5;1;FE (Ferie): 8",
            lines[1]);
    }
}
