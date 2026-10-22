using System.Windows;
using System.Windows.Media.Imaging;
using PresenzeXmlConverter.Wpf;

namespace PresenzeXmlConverter.Tests;

public class LeaveBalanceTests
{
    private static AbsenceTotal Used(int year, int month, string code, string name, string policy, string reason, int minutes) =>
        new("AZ001", new DateTime(year, month, 1), code, name, policy, reason, minutes);

    [Theory]
    [InlineData("Ferie = 26 giorni", "Ferie", 26, true)]
    [InlineData("Ferie=26", "Ferie", 26, true)]
    [InlineData("Permessi = 104 ore", "Permessi", 104, false)]
    [InlineData("Permessi retribuiti = 104h", "Permessi retribuiti", 104, false)]
    [InlineData("ROL = 7,5 gg", "ROL", 7.5, true)]
    [InlineData("Ex festività = 32 ora", "Ex festività", 32, false)]
    public void RulesAreReadWithTheirUnit(string line, string policy, double amount, bool inDays)
    {
        Assert.True(LeaveRule.TryParseLines(line, out var rules, out var error));
        Assert.Null(error);
        var rule = Assert.Single(rules);
        Assert.Equal(policy, rule.Policy);
        Assert.Equal((decimal)amount, rule.Amount);
        Assert.Equal(inDays, rule.InDays);
    }

    [Theory]
    [InlineData("Ferie")]
    [InlineData("Ferie =")]
    [InlineData("= 26")]
    [InlineData("Ferie = abc")]
    [InlineData("Ferie = 0")]
    [InlineData("Ferie = 99999")]
    [InlineData("Ferie = 26\nferie = 10")]
    public void BadLinesAreRefusedWithAMessage(string text)
    {
        Assert.False(LeaveRule.TryParseLines(text, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void ARuleSurvivesTheJsonAndTheTextRoundTrip()
    {
        Assert.True(LeaveRule.TryParseLines("Ferie = 26 giorni\r\nPermessi = 104 ore", out var rules, out _));

        Assert.Equal(rules, LeaveRule.TryParseJson(LeaveRule.ToJson(rules)));
        Assert.True(LeaveRule.TryParseLines(LeaveRule.ToLines(rules), out var again, out _));
        Assert.Equal(rules, again);
        Assert.Empty(LeaveRule.TryParseJson("{non json"));
        Assert.Empty(LeaveRule.TryParseJson(string.Empty));
    }

    [Fact]
    public void ADayRuleUsesTheStandardDayOfTheCompany()
    {
        var days = new LeaveRule("Ferie", 26m, true);
        var hours = new LeaveRule("Permessi", 104m, false);

        Assert.Equal(26 * 8 * 60, days.EntitlementMinutes(8m));
        Assert.Equal(26 * 6 * 60, days.EntitlementMinutes(6m));
        Assert.Equal(104 * 60, hours.EntitlementMinutes(6m));
        Assert.True(days.Matches("Ferie anno corrente", "FE"));
        Assert.True(days.Matches("Altro", "ferie"));
        Assert.False(days.Matches("Malattia", "MA"));
    }

    [Fact]
    public void TheBalanceAddsTheMonthsOfTheYearAndFlagsWhoIsOverOrLow()
    {
        var rules = new[] { new LeaveRule("Ferie", 10m, true) };
        var usage = new[]
        {
            Used(2026, 1, "E001", "Rossi Mario", "Ferie", "FE", 40 * 60),
            Used(2026, 2, "E001", "Rossi Mario", "Ferie", "FE", 48 * 60),
            Used(2026, 3, "E002", "Bianchi Anna", "Ferie", "FE", 79 * 60),
            Used(2026, 3, "E003", "Verdi Luca", "Ferie", "FE", 10 * 60),
            Used(2026, 3, "E003", "Verdi Luca", "Malattia", "MA", 80 * 60),
            Used(2025, 12, "E003", "Verdi Luca", "Ferie", "FE", 80 * 60)
        };

        var rows = LeaveBalance.Build(rules, 8m, usage, 2026);

        Assert.Equal(3, rows.Count);
        var rossi = rows.Single(row => row.EmployeeCode == "E001");
        Assert.Equal(88 * 60, rossi.UsedMinutes);
        Assert.Equal(80 * 60, rossi.EntitlementMinutes);
        Assert.Equal(LeaveStatus.Over, rossi.Status);
        Assert.Equal(-8 * 60, rossi.RemainingMinutes);
        Assert.Equal(LeaveStatus.Low, rows.Single(row => row.EmployeeCode == "E002").Status);
        Assert.Equal(LeaveStatus.Ok, rows.Single(row => row.EmployeeCode == "E003").Status);
        Assert.Equal(new[] { 2026, 2025 }, LeaveBalance.Years(usage));
        Assert.Empty(LeaveBalance.Build([], 8m, usage, 2026));
    }

    [Fact]
    public void ThePreviewAddsToWhatWasAlreadyDeliveredButReplacesItsOwnMonths()
    {
        var rules = new[] { new LeaveRule("Ferie", 10m, true) };
        var saved = new[]
        {
            Used(2026, 1, "E001", "Rossi Mario", "Ferie", "FE", 40 * 60),
            Used(2026, 2, "E001", "Rossi Mario", "Ferie", "FE", 30 * 60)
        };

        // Il file rifà febbraio con 8 ore: 40 + 8 = 48 su 80, nessun avviso.
        Assert.Empty(LeaveBalance.CheckPreview(rules, 8m, saved, [Used(2026, 2, "E001", "Rossi Mario", "Ferie", "FE", 8 * 60)]));

        // Il file porta marzo con 48 ore: 40 + 30 + 48 = 118 su 80, oltre.
        var findings = LeaveBalance.CheckPreview(rules, 8m, saved, [Used(2026, 3, "E001", "Rossi Mario", "Ferie", "FE", 48 * 60)]);
        var finding = Assert.Single(findings);
        Assert.Equal(MovementSeverity.Warning, finding.Severity);
        Assert.Contains("Rossi Mario (E001)", finding.Message);
        Assert.Contains("usate 118 h su 80 h", finding.Message);

        // Quasi esaurite: nota, non avviso.
        var low = LeaveBalance.CheckPreview(rules, 8m, saved, [Used(2026, 3, "E001", "Rossi Mario", "Ferie", "FE", 6 * 60)]);
        Assert.Equal(MovementSeverity.Note, Assert.Single(low).Severity);
        Assert.Empty(LeaveBalance.CheckPreview([], 8m, saved, [Used(2026, 3, "E001", "Rossi Mario", "Ferie", "FE", 6 * 60)]));
    }

    [Fact]
    public void TheDatabaseKeepsTheRulesAndTheUsagePerCompany()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var database = new LocalDatabase(Path.Combine(folder, "app.db"));
            var service = new CompaniesService(database);
            service.Save("AZ001", "Rossi Srl", string.Empty);
            LeaveRule.TryParseLines("Ferie = 26 giorni", out var rules, out _);

            service.SaveLeave(service.Load().Single(), rules);

            Assert.Equal(rules, service.Load().Single().Leave);
            service.Save("AZ001", "Rossi Spa", "note");
            service.SaveRules(service.Load().Single(), new CompanyRules(6m, true, false, ""));
            service.SaveChecks(service.Load().Single(), ValidationRules.Default with { LongIllnessDays = 40 });
            Assert.Equal(rules, service.Load().Single().Leave);

            var usage = new[]
            {
                Used(2026, 1, "E001", "Rossi Mario", "Ferie", "FE", 480),
                Used(2026, 1, "E001", "Rossi Mario", "Permesso", "PE", 120),
                Used(2026, 2, "E001", "Rossi Mario", "Ferie", "FE", 960)
            };
            database.SaveAbsenceTotals("AZ001", usage);
            Assert.Equal(3, database.GetAbsenceTotals("az001").Count);

            // Gennaio esportato di nuovo con meno ore sostituisce solo gennaio.
            database.SaveAbsenceTotals("AZ001", [Used(2026, 1, "E001", "Rossi Mario", "Ferie", "FE", 60)]);
            var after = database.GetAbsenceTotals("AZ001");
            Assert.Equal(2, after.Count);
            Assert.Equal(60, after.Single(item => item.Month.Month == 1).Minutes);

            service.SaveLeave(service.Load().Single(), []);
            Assert.Null(service.Load().Single().Leave);
            Assert.Empty(database.GetAbsenceTotals("NESSUNA"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task TheConversionWarnsWhoExceedsTheEntitlementAndExportKeepsTheUsage()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var database = new LocalDatabase(Path.Combine(folder, "app.db"));
            var service = new ConversionService(database) { CompanyCode = "AZ001" };
            var prompts = new MappingPrompts();
            Assert.True(await service.LoadAsync(SampleWorkbook.Path, prompts));
            Assert.Equal(PreviewOutcome.Ready, await service.GenerateAsync(prompts));
            Assert.DoesNotContain(service.Findings, finding => finding.Message.Contains("spettanti"));

            // Spettanza minuscola per ogni giustificativo del foglio: chiunque la supera.
            var policies = service.Movements.Select(movement => movement.Policy).Distinct().ToArray();
            LeaveRule.TryParseLines(string.Join(Environment.NewLine, policies.Select(policy => $"{policy} = 1 ore")), out var rules, out _);
            new CompaniesService(database).SaveLeave(new CompanyRecord("AZ001", string.Empty, string.Empty), rules);
            Assert.Equal(PreviewOutcome.Ready, await service.GenerateAsync(prompts));
            Assert.Contains(service.Findings, finding => finding.Message.Contains("spettanti"));

            service.Export(Path.Combine(folder, "out.xml"));
            Assert.NotEmpty(database.GetAbsenceTotals("AZ001"));
            var year = database.GetAbsenceTotals("AZ001").First().Month.Year;
            Assert.NotEmpty(service.LeaveBalanceFor("AZ001", year));
            Assert.Empty(service.LeaveBalanceFor("AZ002", year));
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
public class WpfLeaveTests
{
    [Fact]
    public void TheLeaveDialogChecksTheLinesBeforeSaving()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            LeaveRule.TryParseLines("Ferie = 26 giorni", out var existing, out _);
            var dialog = new CompanyLeaveWindow(new CompanyRecord("AZ001", "Rossi Srl", string.Empty, null, null, existing));
            dialog.Window.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Window.Left = -4000;
            dialog.Window.Top = -4000;
            dialog.Window.Show();
            WpfTestHost.Pump(300);
            Assert.Contains("Ferie = 26 giorni", dialog.Field.Text);

            dialog.Field.Text = "Ferie 26";
            dialog.Window.Confirm();
            Assert.False(dialog.Window.Accepted);
            Assert.Contains("quantità", dialog.Window.ErrorText);

            dialog.Field.Text = "Ferie = 26 giorni\nPermessi = 104 ore";
            dialog.Window.Confirm();
            Assert.True(dialog.Window.Accepted);
            Assert.Equal(2, dialog.Rules.Count);
        });
    }

    [Fact]
    public void TheBalanceTabListsWhoIsOverFirst()
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
                view.Tabs.Select(2);
                WpfTestHost.Pump(450);
                Assert.Contains("Ferie e permessi", view.Balance.SummaryText);

                var companies = new CompaniesService(database);
                companies.Save("AZ001", "Rossi Srl", string.Empty);
                LeaveRule.TryParseLines("Ferie = 10 giorni", out var rules, out _);
                companies.SaveLeave(companies.Load().Single(), rules);
                database.SaveAbsenceTotals("AZ001",
                [
                    new AbsenceTotal("AZ001", new DateTime(2026, 3, 1), "E001", "Verdi Luca", "Ferie", "FE", 40 * 60),
                    new AbsenceTotal("AZ001", new DateTime(2026, 3, 1), "E002", "Rossi Mario", "Ferie", "FE", 88 * 60)
                ]);
                var diagnostic = string.Join(",", database.GetCompanies().Select(item => item.Code + ":" + (item.Leave?.Count ?? -1))) +
                                 " assenze=" + database.GetAbsenceTotals("AZ001").Count;
                view.Tabs.Select(0);
                view.Tabs.Select(2);
                WpfTestHost.Pump(450);

                var rows = view.Balance.Rows;
                Assert.True(rows.Count == 2, "Residui: " + view.Balance.SummaryText + " | " + diagnostic);
                Assert.Equal("Rossi Mario", rows[0].Employee);
                Assert.Equal("Oltre", rows[0].Status);
                Assert.Equal("−8 h", rows[0].Remaining);
                Assert.Equal("In regola", rows[1].Status);
                Assert.Equal("40 h", rows[1].Remaining);

                var bitmap = WpfShellTests.Render(window);
                Assert.True(WpfShellTests.CountMagenta(bitmap) == 0, "Residui: colori mancanti (magenta).");
                var directory = Environment.GetEnvironmentVariable("PRESENZEFLOW_SCREENSHOT_DIR");
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(directory, "wpf-Residui.png"));
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
