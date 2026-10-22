namespace PresenzeXmlConverter.Tests;

public class RequestChecksTests
{
    private static AttendanceRequest Request(string name, string policy, string from, string to, int row, decimal hours = 8m) =>
        new(name, policy, DateTime.Parse(from, System.Globalization.CultureInfo.InvariantCulture), DateTime.Parse(to, System.Globalization.CultureInfo.InvariantCulture), hours, row);

    [Fact]
    public void RequestsOfTheSamePersonThatOverlapAreReported()
    {
        var findings = RequestChecks.Review(new[]
        {
            Request("Rossi Mario", "Ferie", "2026-03-02", "2026-03-06", 2),
            Request("ROSSI  MARIO", "Malattia", "2026-03-05", "2026-03-10", 3),
            Request("Bianchi Anna", "Ferie", "2026-03-05", "2026-03-10", 4)
        });

        var finding = Assert.Single(findings);
        Assert.Equal(MovementSeverity.Warning, finding.Severity);
        Assert.Contains("righe 2 (Ferie) e 3 (Malattia)", finding.Message);
        Assert.Contains("dal 05/03/2026 al 06/03/2026", finding.Message);
    }

    [Fact]
    public void AnIdenticalRequestIsReportedAsPossiblyDoubled()
    {
        var findings = RequestChecks.Review(new[]
        {
            Request("Rossi Mario", "Ferie", "2026-03-02", "2026-03-03", 2),
            Request("Rossi Mario", "ferie", "2026-03-02", "2026-03-03", 5)
        });

        Assert.Contains("stessa richiesta", Assert.Single(findings).Message);
    }

    [Fact]
    public void ConsecutiveRequestsDoNotOverlap()
    {
        var findings = RequestChecks.Review(new[]
        {
            Request("Rossi Mario", "Ferie", "2026-03-02", "2026-03-06", 2),
            Request("Rossi Mario", "Ferie", "2026-03-09", "2026-03-10", 3)
        });

        Assert.Empty(findings);
    }

    [Fact]
    public void ALongIllnessIsReportedAndAdjacentOnesAreJoined()
    {
        var findings = RequestChecks.Review(new[]
        {
            Request("Rossi Mario", "Malattia", "2026-03-01", "2026-03-20", 2),
            Request("Rossi Mario", "Malattia", "2026-03-21", "2026-04-10", 3),
            Request("Bianchi Anna", "Malattia", "2026-03-01", "2026-03-30", 4),
            Request("Verdi Luca", "Ferie", "2026-01-01", "2026-03-30", 5)
        });

        var finding = Assert.Single(findings);
        Assert.Contains("“Rossi Mario”", finding.Message);
        Assert.Contains("41 giorni", finding.Message);
        Assert.Contains("riga 2, 3", finding.Message);
    }

    [Fact]
    public void AnIllnessOfExactlyThirtyDaysIsFine() =>
        Assert.Empty(RequestChecks.Review(new[] { Request("Rossi Mario", "Malattia", "2026-03-01", "2026-03-30", 2) }));

    [Theory]
    [InlineData(1000, 12, 1000, 12, false)]
    [InlineData(600, 12, 1000, 12, true)]
    [InlineData(1500, 12, 1000, 12, true)]
    [InlineData(1000, 7, 1000, 12, true)]
    [InlineData(800, 10, 1000, 12, false)]
    public void ADeliveryFarFromTheLastOneIsNoted(int currentHours, int currentEmployees, int previousHours, int previousEmployees, bool expected)
    {
        var current = new MovementTotals(40, currentEmployees, 20, currentHours * 60);
        var previous = new ExportRecord(DateTime.UtcNow.AddDays(-30), "a.xml", "a.xml", "AZ001", previousEmployees, 40, previousHours * 60);

        var findings = RequestChecks.CompareWithPreviousDelivery(current, previous);

        Assert.Equal(expected, findings.Count == 1);
        Assert.All(findings, finding => Assert.Equal(MovementSeverity.Note, finding.Severity));
    }

    [Fact]
    public void WithoutAPreviousDeliveryThereIsNothingToCompare() =>
        Assert.Empty(RequestChecks.CompareWithPreviousDelivery(new MovementTotals(10, 3, 5, 600), null));

    [Theory]
    [InlineData("Malattia", true)]
    [InlineData("Sick leave", true)]
    [InlineData("Permessi retribuiti (Milestone)", false)]
    [InlineData("Ferie", false)]
    public void OnlyRealIllnessCountsAsSick(string policy, bool expected) =>
        Assert.Equal(expected, WorkbookInspection.IsSickPolicy(policy));
}
