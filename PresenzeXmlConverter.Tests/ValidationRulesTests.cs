namespace PresenzeXmlConverter.Tests;

public class ValidationRulesTests
{
    private static AttendanceRequest Request(string name, string policy, string from, string to, int row) =>
        new(name, policy, DateTime.Parse(from, System.Globalization.CultureInfo.InvariantCulture), DateTime.Parse(to, System.Globalization.CultureInfo.InvariantCulture), 8m, row);

    [Fact]
    public void TheDefaultRulesKeepTheBehaviourOfBefore()
    {
        var rules = ValidationRules.Default;

        Assert.True(rules.IsDefault);
        Assert.True(rules.IsValid);
        Assert.Equal(30, rules.LongIllnessDays);
        Assert.Equal(30, rules.ChangePercent);
        Assert.True(rules.CheckOverlaps);
        Assert.Equal(0, rules.LongAbsenceDays);
        Assert.Equal("di serie", rules.Describe());
    }

    [Fact]
    public void RulesSurviveTheJsonRoundTripAndBrokenOnesAreIgnored()
    {
        var rules = new ValidationRules(20, 15, 50, false, 180m, "Rossi Mario\nBianchi Anna");

        Assert.Equal(rules, ValidationRules.TryParse(rules.ToJson()));
        Assert.Null(ValidationRules.TryParse(string.Empty));
        Assert.Null(ValidationRules.TryParse("{non json"));
        Assert.Null(ValidationRules.TryParse("{\"LongIllnessDays\":0}"));
        Assert.Null(ValidationRules.TryParse("{\"ChangePercent\":1}"));
        // Un testo vecchio senza i campi nuovi vale come le soglie di serie.
        Assert.Equal(ValidationRules.Default, ValidationRules.TryParse("{}"));
    }

    [Fact]
    public void TheExpectedEmployeesAreSplitAndDeduplicated()
    {
        var rules = ValidationRules.Default with { ExpectedEmployees = "Rossi Mario\r\n\r\nBianchi Anna; rossi mario ;" };

        Assert.Equal(new[] { "Rossi Mario", "Bianchi Anna" }, rules.ExpectedList);
    }

    [Fact]
    public void ALongerIllnessThresholdSilencesTheDefaultWarning()
    {
        var requests = new[] { Request("Rossi Mario", "Malattia", "2026-03-01", "2026-04-10", 2) };

        Assert.Single(RequestChecks.Review(requests));
        Assert.Empty(RequestChecks.Review(requests, ValidationRules.Default with { LongIllnessDays = 60 }));
        Assert.Single(RequestChecks.Review(requests, ValidationRules.Default with { LongIllnessDays = 40 }));
    }

    [Fact]
    public void LongAbsencesOtherThanIllnessAreCheckedOnlyWhenAskedFor()
    {
        var requests = new[]
        {
            Request("Rossi Mario", "Ferie", "2026-08-03", "2026-08-14", 2),
            Request("Rossi Mario", "Ferie", "2026-08-15", "2026-08-20", 3),
            Request("Rossi Mario", "Malattia", "2026-09-01", "2026-09-20", 4)
        };

        Assert.Empty(RequestChecks.Review(requests));
        var findings = RequestChecks.Review(requests, ValidationRules.Default with { LongAbsenceDays = 15 });
        var finding = Assert.Single(findings);
        Assert.Contains("Ferie per 18 giorni di fila", finding.Message);
        Assert.Contains("riga 2, 3", finding.Message);
    }

    [Fact]
    public void OverlapsCanBeTurnedOff()
    {
        var requests = new[]
        {
            Request("Rossi Mario", "Ferie", "2026-03-02", "2026-03-06", 2),
            Request("Rossi Mario", "Permesso", "2026-03-05", "2026-03-05", 3)
        };

        Assert.Single(RequestChecks.Review(requests));
        Assert.Empty(RequestChecks.Review(requests, ValidationRules.Default with { CheckOverlaps = false }));
    }

    [Fact]
    public void MissingExpectedEmployeesAreReportedWhateverTheNameOrder()
    {
        var requests = new[] { Request("Rossi Mario", "Ferie", "2026-03-02", "2026-03-02", 2) };
        var rules = ValidationRules.Default with { ExpectedEmployees = "Mario Rossi\nBianchi Anna" };

        var finding = Assert.Single(RequestChecks.CheckExpectedEmployees(requests, rules));

        Assert.Contains("Bianchi Anna", finding.Message);
        Assert.Empty(RequestChecks.CheckExpectedEmployees(requests, ValidationRules.Default));
    }

    [Fact]
    public void TheMonthlyHoursCeilingIsCheckedPerEmployeeAndMonth()
    {
        var movements = new[]
        {
            new DailyMovement("Rossi Mario", "E001", "Ferie", "FE", new DateTime(2026, 3, 2), 8, 0),
            new DailyMovement("Rossi Mario", "E001", "Ferie", "FE", new DateTime(2026, 3, 3), 8, 30),
            new DailyMovement("Rossi Mario", "E001", "Ferie", "FE", new DateTime(2026, 4, 1), 8, 0),
            new DailyMovement("Bianchi Anna", "E002", "Ferie", "FE", new DateTime(2026, 3, 2), 8, 0)
        };

        Assert.Empty(MovementChecks.CheckMonthLimit(movements, 0m));
        var finding = Assert.Single(MovementChecks.CheckMonthLimit(movements, 16m));
        Assert.Contains("Rossi Mario", finding.Message);
        Assert.Contains("16h 30m", finding.Message);
        Assert.Equal(3, MovementChecks.CheckMonthLimit(movements, 7m).Count);
    }

    [Theory]
    [InlineData("30", "", "30", "", true)]
    [InlineData("20", "15", "50", "180", true)]
    [InlineData("0", "", "30", "", false)]
    [InlineData("abc", "", "30", "", false)]
    [InlineData("30", "400", "30", "", false)]
    [InlineData("30", "", "2", "", false)]
    [InlineData("30", "", "30", "800", false)]
    [InlineData("30", "", "30", "x", false)]
    public void TheFieldsAreValidatedBeforeSaving(string illness, string absence, string percent, string hours, bool valid)
    {
        var ok = DialogLogic.TryBuildValidationRules(illness, absence, percent, true, hours, string.Empty, out var rules, out var error);

        Assert.Equal(valid, ok);
        Assert.Equal(valid, error is null);
        if (valid)
        {
            Assert.True(rules.IsValid);
        }
    }

    [Fact]
    public void ThresholdsAreSavedPerCompanyAndTheOthersKeepTheDefaults()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var database = new LocalDatabase(Path.Combine(folder, "app.db"));
            var service = new CompaniesService(database);
            service.Save("AZ001", "Rossi Srl", string.Empty);
            service.Save("AZ002", "Bianchi", string.Empty);
            var rules = new ValidationRules(20, 15, 50, false, 180m, "Rossi Mario");

            service.SaveChecks(service.Load().First(company => company.Code == "AZ001"), rules);

            Assert.Equal(rules, ValidationRules.For(database, "az001"));
            Assert.Equal(ValidationRules.Default, ValidationRules.For(database, "AZ002"));
            Assert.Equal(ValidationRules.Default, ValidationRules.For(database, "NONESISTE"));
            Assert.Equal(ValidationRules.Default, ValidationRules.For(database, string.Empty));
            Assert.Contains("malattia oltre 20 g", rules.Describe());

            // Salvare di nuovo ragione sociale, regole di conversione o note non cancella le soglie.
            service.Save("AZ001", "Rossi Spa", "nuove note");
            service.SaveRules(service.Load().First(company => company.Code == "AZ001"), new CompanyRules(6m, true, false, ""));
            Assert.Equal(rules, ValidationRules.For(database, "AZ001"));

            service.SaveChecks(service.Load().First(company => company.Code == "AZ001"), ValidationRules.Default);
            Assert.Equal(ValidationRules.Default, ValidationRules.For(database, "AZ001"));
            Assert.Contains(database.GetActivities(), activity => activity.Title.Contains("Controlli dell'azienda AZ001"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task TheConversionUsesTheCompanyThresholds()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var database = new LocalDatabase(Path.Combine(folder, "app.db"));
            var service = new ConversionService(database) { CompanyCode = "AZ001" };
            var prompts = new TestPrompts();
            Assert.True(await service.LoadAsync(SampleWorkbook.Path, prompts));
            Assert.Equal(PreviewOutcome.Ready, await service.GenerateAsync(prompts));
            Assert.DoesNotContain(service.Findings, finding => finding.Message.Contains("tra i dipendenti attesi"));

            new CompaniesService(database).SaveChecks(
                new CompanyRecord("AZ001", string.Empty, string.Empty),
                ValidationRules.Default with { ExpectedEmployees = "Nome Che Non Esiste" });
            Assert.Equal(PreviewOutcome.Ready, await service.GenerateAsync(prompts));

            Assert.Contains(service.Findings, finding => finding.Message.Contains("Nome Che Non Esiste") && finding.Message.Contains("attesi"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private sealed class TestPrompts : IConversionPrompts
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
