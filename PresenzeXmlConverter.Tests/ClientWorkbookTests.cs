using ClosedXML.Excel;

namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Il foglio vero del cliente: intestazione alla riga 3, due colonne «Policy type» e
/// «Policy», periodo in testa di AGOSTO ma richieste di SETTEMBRE. L'app diceva
/// «nessun movimento» perché scartava tutte le richieste come fuori periodo.
/// </summary>
public class ClientWorkbookTests
{
    private static string CreateClientLikeWorkbook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cliente-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Italy Payroll Attendence");
        sheet.Cell(1, 1).Value = "From Date";
        sheet.Cell(1, 2).Value = new DateTime(2026, 8, 1);
        sheet.Cell(2, 1).Value = "To Date";
        sheet.Cell(2, 2).Value = new DateTime(2026, 8, 31);
        string[] headers =
        [
            "Display name", "Policy type", "Policy", "Request Period", "Start date", "End date",
            "Total duration", "Requested on", "Waiting for approver", "Description", "Attachment", "Duration in range"
        ];
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(3, column + 1).Value = headers[column];
        }
        (string Name, DateTime Start, DateTime End, int Hours)[] rows =
        [
            ("Antenori Vincenzo", new DateTime(2026, 9, 17), new DateTime(2026, 9, 18), 16),
            ("Antenori Vincenzo", new DateTime(2026, 9, 24), new DateTime(2026, 9, 24), 8),
            ("Bertani Sofia", new DateTime(2026, 9, 24), new DateTime(2026, 9, 24), 4),
        ];
        for (var index = 0; index < rows.Length; index++)
        {
            var row = 4 + index;
            sheet.Cell(row, 1).Value = rows[index].Name;
            sheet.Cell(row, 2).Value = "Vacation (hours)";
            sheet.Cell(row, 3).Value = "IT vacation (hours)";
            sheet.Cell(row, 4).Value = rows[index].Start;
            sheet.Cell(row, 5).Value = rows[index].Start;
            sheet.Cell(row, 6).Value = rows[index].End;
            sheet.Cell(row, 7).Value = rows[index].Hours;
            sheet.Cell(row, 12).Value = rows[index].Hours;
        }
        workbook.SaveAs(path);
        return path;
    }

    [Fact]
    public void PolicyColumnWinsOverPolicyTypeWhenTheSheetHasBoth()
    {
        var path = CreateClientLikeWorkbook();
        try
        {
            var inspection = AttendanceWorkbookReader.Inspect(path);
            var columns = inspection.SuggestedColumns;
            Assert.NotNull(columns);
            Assert.Equal(2, columns!.Policy);
            var requests = inspection.ReadRequests(columns);
            Assert.All(requests, request => Assert.Equal("IT vacation (hours)", request.Policy));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ASheetPeriodThatContainsNoRequestIsIgnoredInsteadOfDiscardingEverything()
    {
        var path = CreateClientLikeWorkbook();
        try
        {
            var inspection = AttendanceWorkbookReader.Inspect(path);
            Assert.Equal(new DateTime(2026, 8, 1), inspection.PeriodStart);
            var requests = inspection.ReadRequests(inspection.SuggestedColumns!);

            var result = AttendanceMovementGenerator.Generate(
                requests,
                new Dictionary<string, string> { ["Antenori Vincenzo"] = "0000001", ["Bertani Sofia"] = "0000002" },
                new Dictionary<string, string> { ["IT vacation (hours)"] = "001" },
                inspection.PeriodStart,
                inspection.PeriodEnd);

            Assert.Equal(4, result.Movements.Count);
            Assert.Contains(result.Warnings, warning => warning.Contains("non contiene nessuna richiesta", StringComparison.Ordinal));
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("fuori dal periodo", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ARequestOutsideAPeriodThatOtherRequestsShareIsStillExcluded()
    {
        var inside = new AttendanceRequest("A", "P", new DateTime(2026, 9, 7), new DateTime(2026, 9, 7), 8m, 4);
        var outside = new AttendanceRequest("A", "P", new DateTime(2026, 10, 5), new DateTime(2026, 10, 5), 8m, 5);

        var result = AttendanceMovementGenerator.Generate(
            [inside, outside],
            new Dictionary<string, string> { ["A"] = "0000001" },
            new Dictionary<string, string> { ["P"] = "001" },
            new DateTime(2026, 9, 1),
            new DateTime(2026, 9, 30));

        Assert.Single(result.Movements);
        Assert.Contains(result.Warnings, warning => warning.Contains("fuori dal periodo", StringComparison.Ordinal));
    }
}

/// <summary>
/// I giorni lavorativi senza assenza vanno comunque contati come presenza con il
/// giustificativo universale 01 (codice ufficiale 01: ore ordinarie).
/// </summary>
public class PresenceFillTests
{
    private static AttendanceRequest Request(string policy, DateTime start, DateTime end, decimal hours) =>
        new("Rossi Mario", policy, start, end, hours, 4);

    private static GenerationResult Run(params AttendanceRequest[] requests) =>
        AttendanceMovementGenerator.Generate(
            requests,
            new Dictionary<string, string> { ["Rossi Mario"] = "0000001" },
            new Dictionary<string, string> { ["Ferie"] = "FE" },
            null,
            null,
            new GenerationOptions(FillPresence: true));

    [Fact]
    public void EveryWorkingDayWithoutAbsenceGetsThePresenceCode()
    {
        // Settembre 2026: 22 giorni lavorativi lun-ven; 3 di ferie (mar 15, mer 16, gio 17).
        var result = Run(Request("Ferie", new DateTime(2026, 9, 15), new DateTime(2026, 9, 17), 24m));

        var absences = result.Movements.Where(movement => movement.ReasonCode == "FE").ToArray();
        var presences = result.Movements.Where(movement => movement.ReasonCode == "01").ToArray();
        Assert.Equal(3, absences.Length);
        Assert.Equal(19, presences.Length);
        Assert.All(presences, movement =>
        {
            Assert.Equal("01", movement.OfficialCode);
            Assert.Equal(8, movement.Hours);
            Assert.NotEqual(DayOfWeek.Saturday, movement.Date.DayOfWeek);
            Assert.NotEqual(DayOfWeek.Sunday, movement.Date.DayOfWeek);
        });
        Assert.DoesNotContain(presences, movement => absences.Any(absence => absence.Date == movement.Date));
    }

    [Fact]
    public void AHalfDayAbsenceLeavesTheResidualHoursAsPresence()
    {
        var result = Run(Request("Ferie", new DateTime(2026, 9, 16), new DateTime(2026, 9, 16), 4m));

        var presence = Assert.Single(result.Movements, movement =>
            movement.ReasonCode == "01" && movement.Date == new DateTime(2026, 9, 16));
        Assert.Equal(4, presence.Hours);
        Assert.Equal(0, presence.Minutes);
    }

    [Fact]
    public void NationalHolidaysAndExtraClosuresGetNoPresence()
    {
        var options = new GenerationOptions(
            FillPresence: true,
            ExtraHolidays: [new DateTime(2026, 9, 21)]);
        var result = AttendanceMovementGenerator.Generate(
            [Request("Ferie", new DateTime(2026, 9, 15), new DateTime(2026, 9, 15), 8m)],
            new Dictionary<string, string> { ["Rossi Mario"] = "0000001" },
            new Dictionary<string, string> { ["Ferie"] = "FE" },
            null,
            null,
            options);

        Assert.DoesNotContain(result.Movements, movement => movement.Date == new DateTime(2026, 9, 21));
    }

    [Fact]
    public void TheWriterGivesTheOfficialCodeItsOwnElementAndTheXmlStaysValid()
    {
        var result = Run(Request("Ferie", new DateTime(2026, 9, 15), new DateTime(2026, 9, 15), 8m));
        var xml = TrriPwXmlWriter.Create("000198", result.Movements);
        var document = System.Xml.Linq.XDocument.Parse(xml);
        var presence = document.Descendants("Movimento")
            .First(item => (string?)item.Element("CodGiustificativoRilPres") == "01");

        Assert.Equal("01", (string?)presence.Element("CodGiustificativoUfficiale"));
        Assert.Empty(TrriPwXmlValidator.Validate(System.Text.Encoding.UTF8.GetBytes(xml)));
    }

    [Fact]
    public void WithoutTheOptionNothingIsAdded()
    {
        var result = AttendanceMovementGenerator.Generate(
            [Request("Ferie", new DateTime(2026, 9, 15), new DateTime(2026, 9, 15), 8m)],
            new Dictionary<string, string> { ["Rossi Mario"] = "0000001" },
            new Dictionary<string, string> { ["Ferie"] = "FE" },
            null,
            null);
        Assert.Single(result.Movements);
    }
}

/// <summary>Part-time e rapporto di lavoro: le ore e i giorni propri del dipendente.</summary>
public class EmployeeProfileTests
{
    private static readonly Dictionary<string, string> Codes = new() { ["Rossi Mario"] = "0000001", ["Verdi Anna"] = "0000002" };
    private static readonly Dictionary<string, string> Policies = new() { ["Ferie"] = "FE" };

    private static GenerationResult Run(
        IReadOnlyList<AttendanceRequest> requests,
        Dictionary<string, EmployeeProfile> profiles) =>
        AttendanceMovementGenerator.Generate(
            requests, Codes, Policies, null, null, new GenerationOptions(FillPresence: true), profiles);

    [Fact]
    public void APartTimerGetsHerOwnHoursAsPresence()
    {
        var result = Run(
            [new AttendanceRequest("Verdi Anna", "Ferie", new DateTime(2026, 9, 15), new DateTime(2026, 9, 15), 4m, 4)],
            new Dictionary<string, EmployeeProfile> { ["Verdi Anna"] = new(DailyHours: 4m) });

        var presence = result.Movements.Where(movement => movement.ReasonCode == "01").ToArray();
        Assert.All(presence, movement => Assert.Equal(4, movement.Hours));
        // Il 15 la giornata da 4 ore è tutta di ferie: nessuna presenza.
        Assert.DoesNotContain(presence, movement => movement.Date == new DateTime(2026, 9, 15));
        Assert.Equal(21, presence.Length);
    }

    [Fact]
    public void NoPresenceIsWrittenBeforeHiringOrAfterTheEndOfTheContract()
    {
        var result = Run(
            [new AttendanceRequest("Rossi Mario", "Ferie", new DateTime(2026, 9, 16), new DateTime(2026, 9, 16), 8m, 4)],
            new Dictionary<string, EmployeeProfile>
            {
                ["Rossi Mario"] = new(StartDate: new DateTime(2026, 9, 10), EndDate: new DateTime(2026, 9, 25))
            });

        var presence = result.Movements.Where(movement => movement.ReasonCode == "01").ToArray();
        Assert.Equal(new DateTime(2026, 9, 10), presence.Min(movement => movement.Date));
        Assert.Equal(new DateTime(2026, 9, 25), presence.Max(movement => movement.Date));
        Assert.DoesNotContain(presence, movement => movement.Date == new DateTime(2026, 9, 16));
    }

    [Fact]
    public void AnEmployeeWithoutAProfileKeepsTheStandardRules()
    {
        var result = Run(
            [new AttendanceRequest("Rossi Mario", "Ferie", new DateTime(2026, 9, 16), new DateTime(2026, 9, 16), 8m, 4)],
            []);
        Assert.Equal(21, result.Movements.Count(movement => movement.ReasonCode == "01"));
    }

    [Fact]
    public void ProfilesRoundTripThroughTheDatabaseAndAnEmptyOneIsDeleted()
    {
        var path = Path.Combine(Path.GetTempPath(), $"profili-{Guid.NewGuid():N}.db");
        try
        {
            {
                var database = new LocalDatabase(path);
                database.SaveEmployeeProfile("Verdi Anna", new EmployeeProfile(4.5m, new DateTime(2026, 3, 1), null));
                var read = database.GetEmployeeProfiles();
                Assert.Equal(4.5m, read["verdi anna"].DailyHours);
                Assert.Equal(new DateTime(2026, 3, 1), read["Verdi Anna"].StartDate);
                Assert.Null(read["Verdi Anna"].EndDate);

                database.SaveEmployeeProfile("Verdi Anna", new EmployeeProfile());
                Assert.Empty(database.GetEmployeeProfiles());
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var file in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(path) + "*"))
            {
                File.Delete(file);
            }
        }
    }
}

/// <summary>I controlli dell'area di revisione non devono trattare le presenze aggiunte dall'app come richieste del foglio.</summary>
public class PresenceChecksTests
{
    private static readonly Dictionary<string, string> Codes = new() { ["Rossi Mario"] = "0000001" };
    private static readonly Dictionary<string, string> Policies = new() { ["Ferie"] = "FE" };
    private static readonly AttendanceRequest[] Requests =
    [
        new("Rossi Mario", "Ferie", new DateTime(2026, 9, 16), new DateTime(2026, 9, 16), 4m, 4)
    ];

    private static GenerationResult Generate(GenerationOptions options) =>
        AttendanceMovementGenerator.Generate(Requests, Codes, Policies, null, null, options);

    [Fact]
    public void PresenceRowsAreNotReportedAsUnconfiguredCodesOrExtraReasons()
    {
        var options = new GenerationOptions(FillPresence: true);
        var movements = Generate(options).Movements;

        var findings = MovementChecks.Review(
            movements, Policies, ["Rossi Mario"], null, null, options.HoursPerDay, includeSaturdays: true);

        Assert.DoesNotContain(findings, finding => finding.Severity == MovementSeverity.Error);
        Assert.DoesNotContain(findings, finding => finding.Message.Contains("più giustificativi", StringComparison.Ordinal));
    }

    [Fact]
    public void ComparisonWithTheSourceIgnoresThePresenceAddedByTheApp()
    {
        var movements = Generate(new GenerationOptions(FillPresence: true)).Movements;
        Assert.Empty(MovementChecks.CompareWithSource(movements, Requests));
    }

    [Fact]
    public void AFullMonthMatchesItsTheoreticalHours()
    {
        var options = new GenerationOptions(FillPresence: true);
        var movements = Generate(options).Movements;
        Assert.Empty(MovementChecks.CheckMonthlyHours(movements, options, null, null, null));
    }

    [Fact]
    public void AMissingDayIsReportedWithTheHoursThatAreOff()
    {
        var options = new GenerationOptions(FillPresence: true);
        var movements = Generate(options).Movements
            .Where(movement => movement.Date != new DateTime(2026, 9, 21))
            .ToList();

        var finding = Assert.Single(MovementChecks.CheckMonthlyHours(movements, options, null, null, null));
        Assert.Equal(MovementSeverity.Warning, finding.Severity);
        Assert.Contains("Rossi Mario", finding.Message, StringComparison.Ordinal);
        Assert.Contains("8h", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMonthlyCheckIsSilentWhenPresenceIsOff()
    {
        var options = new GenerationOptions();
        var movements = Generate(options).Movements;
        Assert.Empty(MovementChecks.CheckMonthlyHours(movements, options, null, null, null));
    }

    [Fact]
    public void APartTimerIsComparedWithHerOwnHours()
    {
        var options = new GenerationOptions(FillPresence: true);
        var profiles = new Dictionary<string, EmployeeProfile> { ["Rossi Mario"] = new(DailyHours: 4m) };
        var movements = AttendanceMovementGenerator.Generate(Requests, Codes, Policies, null, null, options, profiles).Movements;
        Assert.Empty(MovementChecks.CheckMonthlyHours(movements, options, profiles, null, null));
    }
}
