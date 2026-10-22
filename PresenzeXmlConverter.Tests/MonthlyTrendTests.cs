using System.Windows;
using System.Windows.Media.Imaging;
using PresenzeXmlConverter.Wpf;

namespace PresenzeXmlConverter.Tests;

public class MonthlyTrendTests
{
    private static MonthlyTotal Total(string company, int year, int month, string code, string name, int presence, int absence) =>
        new(company, new DateTime(year, month, 1), code, name, presence, absence);

    private static readonly MonthlyTotal[] Sample =
    [
        Total("AZ001", 2026, 1, "E001", "Rossi Mario", 9600, 480),
        Total("AZ001", 2026, 1, "E002", "Bianchi Anna", 9600, 0),
        Total("AZ001", 2026, 2, "E001", "Rossi Mario", 8640, 960),
        Total("AZ001", 2026, 2, "E002", "Bianchi Anna", 9600, 0),
        Total("AZ001", 2026, 3, "E001", "Rossi Mario", 7200, 2400),
        Total("AZ001", 2026, 3, "E002", "Bianchi Anna", 9120, 0),
        Total("AZ001", 2026, 3, "E003", "Verdi Luca", 4800, 0)
    ];

    [Fact]
    public void TheSeriesSumsEachMonthInOrderAndKeepsTheLatestOnes()
    {
        var series = MonthlyTrend.Series(Sample);

        Assert.Equal(3, series.Count);
        Assert.Equal(new DateTime(2026, 1, 1), series[0].Month);
        Assert.Equal(19680, series[0].TotalMinutes);
        Assert.Equal(480, series[0].AbsenceMinutes);
        Assert.Equal(2, series[0].Employees);
        Assert.Equal(3, series[2].Employees);
        Assert.Equal(2, MonthlyTrend.Series(Sample, 2).Count);
        Assert.Equal(new DateTime(2026, 2, 1), MonthlyTrend.Series(Sample, 2)[0].Month);
        Assert.Empty(MonthlyTrend.Series([]));
    }

    [Fact]
    public void TheChangesCompareTheLatestMonthWithTheOneBeforeAndSortByDifference()
    {
        var changes = MonthlyTrend.Changes(Sample);

        Assert.Equal(new[] { "Verdi Luca", "Rossi Mario", "Bianchi Anna" }, changes.Select(change => change.EmployeeName));
        var rossi = changes.Single(change => change.EmployeeCode == "E001");
        Assert.Equal(9600, rossi.TotalMinutes);
        Assert.Equal(9600, rossi.PreviousMinutes);
        Assert.Equal(0, rossi.Delta);
        Assert.Equal(1440, rossi.AbsenceDelta);
        var bianchi = changes.Single(change => change.EmployeeCode == "E002");
        Assert.Equal(-480, bianchi.Delta);
        Assert.Equal(-5, bianchi.Percent);
        var verdi = changes.Single(change => change.EmployeeCode == "E003");
        Assert.Equal(0, verdi.PreviousMinutes);
        Assert.Null(verdi.Percent);
        Assert.Empty(MonthlyTrend.Changes([]));
    }

    [Fact]
    public void WithASingleMonthEveryoneIsNewAndThereIsNoPreviousMonth()
    {
        var one = Sample.Where(item => item.Month.Month == 3).ToArray();

        Assert.Null(MonthlyTrend.LatestMonths(one)!.Value.Previous);
        Assert.All(MonthlyTrend.Changes(one), change => Assert.Equal(0, change.PreviousMinutes));
        Assert.Null(MonthlyTrend.LatestMonths([]));
    }

    [Theory]
    [InlineData(null, "—")]
    [InlineData(0, "0%")]
    [InlineData(12, "+12%")]
    [InlineData(-5, "−5%")]
    public void PercentagesAreWrittenWithTheirSign(int? percent, string expected) =>
        Assert.Equal(expected, MonthlyTrend.PercentText(percent));

    [Fact]
    public void HoursAreWrittenInItalian()
    {
        Assert.Equal("8 h", MonthlyTrend.Hours(480));
        Assert.Equal("7,5 h", MonthlyTrend.Hours(450));
        Assert.Equal("—", MonthlyTrend.SignedHours(0));
        Assert.Equal("+1 h", MonthlyTrend.SignedHours(60));
        Assert.Equal("−8 h", MonthlyTrend.SignedHours(-480));
        Assert.Equal("Marzo 2026", MonthlyTrend.MonthName(new DateTime(2026, 3, 9)));
    }

    [Fact]
    public void TheSummaryBecomesTotalsPerEmployeeAndMonth()
    {
        var rows = new[]
        {
            new MonthlySummaryRow("Rossi Mario", "E001", new DateTime(2026, 3, 1), 9600, 480, null, null, []),
            new MonthlySummaryRow("Senza Codice", string.Empty, new DateTime(2026, 3, 1), 100, 0, null, null, [])
        };

        var totals = MonthlyTrend.FromSummary(" AZ001 ", rows);

        var total = Assert.Single(totals);
        Assert.Equal("AZ001", total.CompanyCode);
        Assert.Equal(10080, total.TotalMinutes);
    }

    [Fact]
    public void TheDatabaseKeepsTheMonthsAndReplacesAMonthExportedAgain()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var database = new LocalDatabase(Path.Combine(folder, "app.db"));
            database.SaveMonthlyTotals("AZ001", Sample);
            database.SaveMonthlyTotals("AZ002", [Total("AZ002", 2026, 3, "E900", "Altro Dipendente", 100, 0)]);

            Assert.Equal(new[] { "AZ001", "AZ002" }, database.GetMonthlyCompanies());
            Assert.Equal(Sample.Length, database.GetMonthlyTotals("az001").Count);

            // Marzo esportato di nuovo, senza Verdi e con ore diverse: sostituisce solo marzo.
            database.SaveMonthlyTotals("AZ001", [Total("AZ001", 2026, 3, "E001", "Rossi Mario", 1, 2)]);
            var after = database.GetMonthlyTotals("AZ001");
            Assert.Equal(5, after.Count);
            Assert.DoesNotContain(after, item => item.EmployeeCode == "E003");
            Assert.Equal(3, after.Single(item => item.Month.Month == 3).TotalMinutes);
            Assert.Equal(2, after.Count(item => item.Month.Month == 1));

            database.SaveMonthlyTotals(string.Empty, Sample);
            database.SaveMonthlyTotals("TROPPOLUNGO", Sample);
            Assert.Equal(2, database.GetMonthlyCompanies().Count);
            Assert.Empty(database.GetMonthlyTotals("NESSUNA"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task ExportingConservesTheMonthlyTotalsOfTheCompany()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var database = new LocalDatabase(Path.Combine(folder, "app.db"));
            var service = new ConversionService(database) { CompanyCode = "AZ001" };
            var prompts = new MappingPrompts();
            Assert.True(await service.LoadAsync(SampleWorkbook.Path, prompts));
            Assert.Equal(PreviewOutcome.Ready, await service.GenerateAsync(prompts));
            Assert.Empty(database.GetMonthlyTotals("AZ001"));

            service.Export(Path.Combine(folder, "out.xml"));

            var saved = database.GetMonthlyTotals("AZ001");
            Assert.NotEmpty(saved);
            Assert.Equal(
                service.Movements.Sum(movement => (movement.Hours * 60) + movement.Minutes),
                saved.Sum(item => item.TotalMinutes));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private sealed class MappingPrompts : IConversionPrompts
    {
        public ColumnSelection? AskColumns(IReadOnlyList<string> headers) => null;

        public bool ConfirmCodeReplace(string employee, string fileCode, string savedCode) => true;

        public bool CompleteMappings(IReadOnlyList<string> missingEmployees, IReadOnlyList<string> missingPolicies, LocalDatabase database)
        {
            var index = 0;
            foreach (var employee in missingEmployees)
            {
                database.SaveEmployeeMapping(employee, $"E{++index:D3}");
            }
            foreach (var policy in missingPolicies)
            {
                database.SavePolicyMapping(policy, "FE");
            }
            return true;
        }

        public bool ConfirmAlreadyDelivered(ExportRecord previous) => true;

        public void ShowProblem(string title, string message)
        {
        }
    }
}

[Collection("Wpf")]
public class WpfTrendTests
{
    [Fact]
    public void TheTrendTabDrawsTheChartAndTheComparisonFromTheSavedMonths()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            WpfTestHost.Run(() =>
            {
                WpfApp.EnsureApplication();
                var database = new LocalDatabase(Path.Combine(folder, "app.db"));
                var service = new ConversionService(database) { CompanyCode = "AZ001" };
                var view = new HistoryView(service, () => null);
                var window = new Window
                {
                    Width = 1000,
                    Height = 700,
                    Content = view,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -4000,
                    Top = -4000
                };
                window.Show();
                view.OnShown();
                WpfTestHost.Pump(300);

                // Senza consegne: nessun grafico, un invito a esportare.
                view.Tabs.Select(1);
                WpfTestHost.Pump(450);
                Assert.Contains("Dopo la prima esportazione", view.Trend.SummaryText);
                Assert.Equal(0, view.Trend.ChartColumns);

                database.SaveMonthlyTotals("AZ001",
                [
                    new MonthlyTotal("AZ001", new DateTime(2026, 2, 1), "E001", "Rossi Mario", 9600, 0),
                    new MonthlyTotal("AZ001", new DateTime(2026, 3, 1), "E001", "Rossi Mario", 7200, 2400),
                    new MonthlyTotal("AZ001", new DateTime(2026, 3, 1), "E003", "Verdi Luca", 4800, 0)
                ]);
                view.Tabs.Select(0);
                view.Tabs.Select(1);
                WpfTestHost.Pump(450);

                Assert.Equal(2, view.Trend.ChartColumns);
                Assert.Contains("Marzo 2026 contro Febbraio 2026", view.Trend.SummaryText);
                var rows = view.Trend.Rows;
                Assert.Equal(2, rows.Count);
                Assert.Equal("Verdi Luca", rows[0].Employee);
                Assert.Equal("nuovo", rows[0].Delta);
                Assert.Equal("Rossi Mario", rows[1].Employee);

                var bitmap = WpfShellTests.Render(window);
                Assert.True(WpfShellTests.CountMagenta(bitmap) == 0, "Andamento: colori mancanti (magenta).");
                var directory = Environment.GetEnvironmentVariable("PRESENZEFLOW_SCREENSHOT_DIR");
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(directory, "wpf-Andamento.png"));
                    encoder.Save(stream);
                }
                window.Close();
            });
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
