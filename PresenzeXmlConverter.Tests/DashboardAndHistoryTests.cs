using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Dashboard e storico delle esportazioni: i conti mostrati dalle card e le
/// regole con cui le consegne vengono ricordate. È logica pura più il database
/// locale, quindi gira anche fuori da Windows.
/// </summary>
public class DashboardAndHistoryTests
{
    /* ------------------------------ Dashboard ---------------------------- */

    [Fact]
    public void DashboardCountsRequestsEmployeesAndMovements()
    {
        var requests = new[]
        {
            new AttendanceRequest("Rossi Mario", "Ferie", new DateTime(2026, 5, 11), new DateTime(2026, 5, 12), 16m, 2),
            new AttendanceRequest("Bianchi Anna", "Permesso", new DateTime(2026, 5, 12), new DateTime(2026, 5, 12), 2m, 3),
            new AttendanceRequest("Rossi Mario", "Malattia", new DateTime(2026, 5, 13), new DateTime(2026, 5, 13), 8m, 4)
        };
        var movements = new[]
        {
            new DailyMovement("Rossi Mario", "0012345", "Ferie", "01", new DateTime(2026, 5, 11), 8, 0),
            new DailyMovement("Rossi Mario", "0012345", "Ferie", "01", new DateTime(2026, 5, 12), 8, 0),
            new DailyMovement("Bianchi Anna", "7654321", "Permesso", "02", new DateTime(2026, 5, 12), 2, 0)
        };

        var summary = DashboardSummary.From(requests, movements, exports: 4);

        Assert.Equal(3, summary.Requests);
        Assert.Equal(2, summary.Employees);
        Assert.Equal(3, summary.Movements);
        Assert.Equal(2, summary.Days);
        Assert.Equal(18 * 60, summary.TotalMinutes);
        Assert.Equal(4, summary.Exports);
        Assert.Equal("3", summary.RequestsText);
        Assert.Equal("2", summary.EmployeesText);
        Assert.Equal("18 h 00 min", summary.TotalText);
        Assert.Equal("18h 00m", summary.CompactTotalText);
        Assert.True(summary.HasMovements);
    }

    [Fact]
    public void EmptyDashboardCountsNothingAndSaysSo()
    {
        var summary = DashboardSummary.From([], [], 0);

        Assert.Equal(0, summary.Movements);
        Assert.Equal(0, summary.Employees);
        Assert.Equal(0, summary.Requests);
        Assert.Equal(0, summary.Days);
        Assert.Equal(0, summary.TotalMinutes);
        Assert.Equal("0", summary.MovementsText);
        Assert.Equal("0h 00m", summary.CompactTotalText);
        Assert.False(summary.HasMovements);
        Assert.Equal("0h 00m", DashboardSummary.Empty.CompactTotalText);
    }

    [Fact]
    public void SourceDescriptionMatchesTheStateOfTheConversion()
    {
        Assert.Contains(
            "Nessun foglio importato",
            DashboardSummary.DescribeSource(null, false),
            StringComparison.Ordinal);
        Assert.Contains(
            "Genera l'anteprima",
            DashboardSummary.DescribeSource("presenze.xlsx", false),
            StringComparison.Ordinal);
        Assert.Contains(
            "puoi esportare",
            DashboardSummary.DescribeSource("presenze.xlsx", true),
            StringComparison.Ordinal);
    }

    /* -------------------------- Storico esportazioni --------------------- */

    [Theory]
    [InlineData(0, "oggi 14:32")]
    [InlineData(1, "ieri 14:32")]
    [InlineData(5, "02/05/2026 14:32")]
    public void ExportDayUsesNamesForTodayAndYesterday(int daysAgo, string expected)
    {
        var now = new DateTime(2026, 5, 7, 18, 0, 0);
        var moment = now.Date.AddDays(-daysAgo).AddHours(14).AddMinutes(32);

        Assert.Equal(expected, ExportHistory.DescribeDay(moment, now));
    }

    [Fact]
    public void ExportDayComparesCalendarDaysNotHours()
    {
        // Alle 07:00 di ieri sono passate meno di 24 ore, ma è comunque "ieri".
        var now = new DateTime(2026, 5, 7, 6, 0, 0);
        var moment = new DateTime(2026, 5, 6, 7, 0, 0);

        Assert.Equal("ieri 07:00", ExportHistory.DescribeDay(moment, now));
    }

    [Theory]
    [InlineData(0, "0 h 00 min")]
    [InlineData(90, "1 h 30 min")]
    [InlineData(2550, "42 h 30 min")]
    public void ExportMinutesAreReadable(int minutes, string expected)
    {
        Assert.Equal(expected, ExportHistory.DescribeMinutes(minutes));
    }

    [Fact]
    public void ExportContentsSummariseMovementsEmployeesAndHours()
    {
        var record = new ExportRecord(
            DateTime.UtcNow,
            "presenze_TRRIPW.xml",
            @"C:\temp\presenze_TRRIPW.xml",
            "AZ001",
            Employees: 35,
            Movements: 72,
            TotalMinutes: 2550);

        Assert.Equal("72 movimenti · 35 dipendenti · 42 h 30 min", ExportHistory.DescribeContents(record));
        Assert.Equal("42 h 30 min", record.TotalText);
    }

    [Fact]
    public void HistorySummaryNamesTheLastExport()
    {
        var now = new DateTime(2026, 5, 7, 18, 0, 0);
        var exports = new[]
        {
            new ExportRecord(now.AddDays(-5).ToUniversalTime(), "vecchio.xml", @"C:\a\vecchio.xml", "AZ001", 1, 1, 60),
            new ExportRecord(now.AddHours(-2).ToUniversalTime(), "nuovo.xml", @"C:\a\nuovo.xml", "AZ001", 1, 2, 120)
        };

        Assert.Equal("Nessuna esportazione registrata.", ExportHistory.DescribeSummary([], now));
        Assert.Equal(
            "1 esportazione registrata · ultima oggi 16:00.",
            ExportHistory.DescribeSummary([exports[1]], now));
        Assert.Equal(
            "2 esportazioni registrate · ultima oggi 16:00.",
            ExportHistory.DescribeSummary(exports, now));
    }

    /* ------------------------- Persistenza locale ------------------------ */

    [Fact]
    public void ExportHistorySurvivesTheSessionAndIsOrderedNewestFirst()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"presenzeflow-exports-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.db");
        try
        {
            var database = new LocalDatabase(path);
            var older = new ExportRecord(
                new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc),
                "primo.xml",
                @"C:\lavoro\primo.xml",
                "AZ001",
                2,
                3,
                480);
            var newer = new ExportRecord(
                new DateTime(2026, 5, 2, 9, 30, 0, DateTimeKind.Utc),
                "secondo.xml",
                @"C:\lavoro\secondo.xml",
                "AZ001",
                4,
                9,
                1500);
            database.SaveExport(older);
            database.SaveExport(newer);

            // Riapre il database come farebbe un nuovo avvio dell'app.
            var reopened = new LocalDatabase(path);
            var exports = reopened.GetExports();

            Assert.Equal(2, reopened.GetExportCount());
            Assert.Equal(2, exports.Count);
            Assert.Equal("secondo.xml", exports[0].FileName);
            Assert.Equal("primo.xml", exports[1].FileName);
            Assert.Equal(4, exports[0].Employees);
            Assert.Equal(9, exports[0].Movements);
            Assert.Equal(1500, exports[0].TotalMinutes);
            Assert.Equal("AZ001", exports[0].CompanyCode);
            Assert.Equal(@"C:\lavoro\secondo.xml", exports[0].FilePath);

            reopened.ClearExports();
            Assert.Equal(0, reopened.GetExportCount());
            Assert.Empty(reopened.GetExports());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void SettingsAndMappingsAreIndependentFromTheExportHistory()
    {
        // Preferenze, abbinamenti e storico convivono nello stesso database:
        // salvare un'esportazione non deve toccare gli altri dati.
        var directory = Path.Combine(Path.GetTempPath(), $"presenzeflow-schema-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.db");
        try
        {
            var database = new LocalDatabase(path);
            database.SaveSetting("company-code", "AZ001");
            database.SaveEmployeeMapping("Rossi Mario", "0012345");

            var reopened = new LocalDatabase(path);

            Assert.Equal("AZ001", reopened.GetSetting("company-code"));
            Assert.Equal("0012345", Assert.Single(reopened.GetEmployeeMappings()).Value);
            Assert.Equal(0, reopened.GetExportCount());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ANewerSchemaIsRefusedInsteadOfBeingOpenedHalfWay()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"presenzeflow-schemax-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.db");
        try
        {
            Directory.CreateDirectory(directory);
            _ = new LocalDatabase(path);
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA user_version = {LocalDatabase.CurrentSchemaVersion + 1};";
                command.ExecuteNonQuery();
            }

            Assert.Throws<InvalidDataException>(() => new LocalDatabase(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
