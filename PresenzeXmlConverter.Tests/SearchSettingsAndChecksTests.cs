namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Verifica la ricerca senza accenti, la lettura delle impostazioni di
/// conversione, i controlli sui movimenti e gli aiuti dell'AI: tutta logica pura
/// che gira anche fuori da Windows.
/// </summary>
public class SearchSettingsAndChecksTests
{
    // ---------- Ricerca nei movimenti ----------

    [Theory]
    [InlineData("màrio", "Mario Rossi", true)]
    [InlineData("MARIO ROSSI", "Mario Rossi", true)]
    [InlineData("rossi-mario", "Rossi Mario", true)]
    [InlineData("perché", "Permesso perche' familiare", true)]
    [InlineData("bianchi", "Mario Rossi", false)]
    public void SearchIgnoresAccentsCaseAndPunctuation(string query, string haystack, bool expected)
    {
        Assert.Equal(expected, MovementSearch.Matches(haystack, MovementSearch.SplitTerms(query)));
    }

    [Fact]
    public void EveryTermMustMatchInAnyOrder()
    {
        var haystack = "Rossi Mario 0012345 Ferie 01 12/05/2026";

        Assert.True(MovementSearch.Matches(haystack, MovementSearch.SplitTerms("ferie mario")));
        Assert.True(MovementSearch.Matches(haystack, MovementSearch.SplitTerms("mario ferie 12/05")));
        Assert.False(MovementSearch.Matches(haystack, MovementSearch.SplitTerms("mario malattia")));
    }

    [Fact]
    public void EmptyQueryMatchesEverythingAndTermsAreDeduplicated()
    {
        Assert.Empty(MovementSearch.SplitTerms("   "));
        Assert.True(MovementSearch.Matches("qualsiasi cosa", MovementSearch.SplitTerms(null)));
        Assert.Equal(2, MovementSearch.SplitTerms("ferie, ferie mario").Count);
    }

    [Fact]
    public void TooManyTermsAreIgnoredInsteadOfSlowingTheGrid()
    {
        var terms = MovementSearch.SplitTerms("a b c d e f g h i l m n");

        Assert.Equal(8, terms.Count);
    }

    [Theory]
    [InlineData("0012345")]      // codice PAGHE
    [InlineData("12/05/2026")]   // data italiana
    [InlineData("2026-05-12")]   // data ISO
    [InlineData("maggio")]       // nome del mese
    [InlineData("07:30")]        // durata
    [InlineData("7.30")]
    [InlineData("450")]          // totale in minuti
    public void MovementSearchFindsCodesDatesAndDurations(string query)
    {
        var movement = new DailyMovement(
            "Rossi Mario", "0012345", "Ferie", "01", new DateTime(2026, 5, 12), 7, 30);

        Assert.True(MovementSearch.MatchesMovement(movement, MovementSearch.SplitTerms(query)));
    }

    [Fact]
    public void GridRowSearchUsesTheValuesShownInTheGrid()
    {
        var haystack = MovementSearch.Describe(
            employee: "Bianchi Anna",
            code: "7654321",
            dateText: "2026-05-12",
            policy: "Permesso",
            reason: "02",
            hoursText: "4",
            minutesText: "15",
            sourceText: "31");

        Assert.True(MovementSearch.Matches(haystack, MovementSearch.SplitTerms("12/05 bianchi")));
        Assert.True(MovementSearch.Matches(haystack, MovementSearch.SplitTerms("4:15")));
        Assert.True(MovementSearch.Matches(haystack, MovementSearch.SplitTerms("31")));
        Assert.False(MovementSearch.Matches(haystack, MovementSearch.SplitTerms("ferie")));
    }

    // ---------- Impostazioni di conversione ----------

    [Theory]
    [InlineData("8", 8)]
    [InlineData("7,5", 7.5)]
    [InlineData("6.25", 6.25)]
    [InlineData(" 24 ", 24)]
    public void HoursPerDayAcceptsCommonFormats(string text, double expected)
    {
        Assert.True(GenerationSettings.TryParseHoursPerDay(text, out var hours));
        Assert.Equal((decimal)expected, hours);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("25")]
    [InlineData("otto")]
    [InlineData("8 ore")]
    public void InvalidHoursPerDayFallsBackToTheDefault(string text)
    {
        Assert.False(GenerationSettings.TryParseHoursPerDay(text, out _));
        Assert.Equal(GenerationSettings.DefaultHoursPerDay, GenerationSettings.ParseHoursPerDay(text));
    }

    [Fact]
    public void HolidaysAreReadFromTheFormatsPeopleActuallyWrite()
    {
        var days = GenerationSettings.ParseHolidaysDetailed(
            "02/06, 24/12/2026; 2026-12-31\n01-05 | 2026.11.02",
            2026,
            out var ignored);

        Assert.Empty(ignored);
        Assert.Equal(
            new[]
            {
                new DateTime(2026, 5, 1),
                new DateTime(2026, 6, 2),
                new DateTime(2026, 11, 2),
                new DateTime(2026, 12, 24),
                new DateTime(2026, 12, 31)
            },
            days);
    }

    [Fact]
    public void UnreadableHolidayEntriesAreReportedAndDoNotBreakTheConversion()
    {
        var days = GenerationSettings.ParseHolidaysDetailed("32/13, pasqua, 24/12", 2026, out var ignored);

        Assert.Equal([new DateTime(2026, 12, 24)], days);
        Assert.Equal(["32/13", "pasqua"], ignored);
    }

    [Fact]
    public void HolidayFormattingKeepsTheYearOnlyWhenItIsDifferent()
    {
        var text = GenerationSettings.FormatHolidays(
            [new DateTime(2027, 1, 1), new DateTime(2026, 6, 2), new DateTime(2026, 6, 2)],
            2026);

        Assert.Equal("02/06, 01/01/2027", text);
    }

    [Fact]
    public void SettingsSurviveASaveAndReloadCycle()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"presenzeflow-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.db");
        try
        {
            var database = new LocalDatabase(path);
            var options = new GenerationOptions(7.5m, true, [new DateTime(2026, 12, 24)]);
            GenerationSettings.Save(database, options, 2026);

            var reloaded = GenerationSettings.Read(database);
            Assert.True(GenerationSettings.AreEquivalent(options, reloaded));
            Assert.Equal(7.5m, reloaded.HoursPerDay);
            Assert.True(reloaded.IncludeSaturdays);
            Assert.Equal([new DateTime(2026, 12, 24)], reloaded.ExtraHolidays);
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
    public void SettingsEquivalenceComparesValuesNotListInstances()
    {
        var first = new GenerationOptions(8m, false, [new DateTime(2026, 1, 2), new DateTime(2026, 3, 4)]);
        var second = new GenerationOptions(8m, false, [new DateTime(2026, 3, 4), new DateTime(2026, 1, 2)]);

        Assert.True(GenerationSettings.AreEquivalent(first, second));
        Assert.False(GenerationSettings.AreEquivalent(first, second with { HoursPerDay = 7m }));
        Assert.False(GenerationSettings.AreEquivalent(first, second with { IncludeSaturdays = true }));
        Assert.False(GenerationSettings.AreEquivalent(first, new GenerationOptions(8m, false, [])));
    }

    [Fact]
    public void BatchSettingsAreReadAndWrittenWithTheAwaitedKeys()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"presenzeflow-batch-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.db");
        try
        {
            var database = new LocalDatabase(path);
            database.SaveSettings(new Dictionary<string, string>
            {
                ["a"] = "1",
                ["b"] = "2"
            });

            var values = database.GetSettings("a", "b", "mancante");
            Assert.Equal("1", values["a"]);
            Assert.Equal("2", values["b"]);
            Assert.Equal(string.Empty, values["mancante"]);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    // ---------- Generazione con le nuove opzioni ----------

    [Fact]
    public void SaturdaysAreOnlyUsedWhenRequested()
    {
        var request = new AttendanceRequest(
            "Rossi Mario", "Ferie", new DateTime(2026, 5, 9), new DateTime(2026, 5, 9), 4m, 7);
        var employees = new Dictionary<string, string> { ["Rossi Mario"] = "0012345" };
        var policies = new Dictionary<string, string> { ["Ferie"] = "01" };

        var standard = AttendanceMovementGenerator.Generate([request], employees, policies, null, null);
        Assert.Empty(standard.Movements);
        var onlyWarning = Assert.Single(standard.Warnings);
        Assert.Contains("nessun giorno lavorativo", onlyWarning, StringComparison.Ordinal);

        var withSaturdays = AttendanceMovementGenerator.Generate(
            [request],
            employees,
            policies,
            null,
            null,
            new GenerationOptions(8m, IncludeSaturdays: true));
        Assert.Single(withSaturdays.Movements);
        Assert.Equal(new DateTime(2026, 5, 9), withSaturdays.Movements[0].Date);
    }

    [Fact]
    public void ExtraHolidaysRemoveTheWholeDayFromTheDistribution()
    {
        var request = new AttendanceRequest(
            "Rossi Mario", "Ferie", new DateTime(2026, 5, 11), new DateTime(2026, 5, 12), 8m, 9);
        var result = AttendanceMovementGenerator.Generate(
            [request],
            new Dictionary<string, string> { ["Rossi Mario"] = "0012345" },
            new Dictionary<string, string> { ["Ferie"] = "01" },
            null,
            null,
            new GenerationOptions(8m, false, [new DateTime(2026, 5, 11)]));

        var movement = Assert.Single(result.Movements);
        Assert.Equal(new DateTime(2026, 5, 12), movement.Date);
        Assert.Equal(8, movement.Hours);
    }

    [Fact]
    public void HoursBeyondTheStandardDayProduceAReadableWarning()
    {
        var request = new AttendanceRequest(
            "Rossi Mario", "Ferie", new DateTime(2026, 5, 12), new DateTime(2026, 5, 12), 12m, 21);
        var result = AttendanceMovementGenerator.Generate(
            [request],
            new Dictionary<string, string> { ["Rossi Mario"] = "0012345" },
            new Dictionary<string, string> { ["Ferie"] = "01" },
            null,
            null,
            new GenerationOptions(8m));

        Assert.Contains(
            result.Warnings,
            warning => warning.Contains("superano la giornata di 8", StringComparison.Ordinal));
    }

    // ---------- Controlli sui movimenti ----------

    [Fact]
    public void SummarizeCountsEmployeesDaysAndMinutes()
    {
        var movements = new[]
        {
            new DailyMovement("Rossi Mario", "0012345", "Ferie", "01", new DateTime(2026, 5, 12), 8, 0),
            new DailyMovement("Rossi Mario", "0012345", "Permesso", "02", new DateTime(2026, 5, 12), 1, 30),
            new DailyMovement("Bianchi Anna", "7654321", "Ferie", "01", new DateTime(2026, 5, 13), 4, 15)
        };

        var totals = MovementChecks.Summarize(movements);
        Assert.Equal(3, totals.Movements);
        Assert.Equal(2, totals.Employees);
        Assert.Equal(2, totals.Days);
        Assert.Equal(13 * 60 + 45, totals.TotalMinutes);
        Assert.Equal("13 h 45 min", totals.TotalText);
        Assert.Equal(MovementTotals.Empty, MovementChecks.Summarize([]));
    }

    [Fact]
    public void FindingsAreCountedBySeverity()
    {
        var counts = MovementChecks.CountBySeverity(
        [
            new MovementFinding(MovementSeverity.Warning, "attenzione"),
            new MovementFinding(MovementSeverity.Error, "errore"),
            new MovementFinding(MovementSeverity.Note, "nota"),
            new MovementFinding(MovementSeverity.Error, "altro errore")
        ]);

        Assert.Equal(2, counts.Errors);
        Assert.Equal(1, counts.Warnings);
        Assert.Equal(1, counts.Notes);
    }

    [Fact]
    public void DailyHoursOptionChangesWhatIsFlaggedAsOverTheStandardDay()
    {
        var movements = new[]
        {
            new DailyMovement("Rossi Mario", "0012345", "Ferie", "01", new DateTime(2026, 5, 12), 9, 0)
        };

        var withEightHours = MovementChecks.Review(
            movements,
            new Dictionary<string, string> { ["Ferie"] = "01" },
            ["Rossi Mario"],
            null,
            null,
            8m);
        Assert.Contains(withEightHours, finding => finding.Message.Contains("giornata standard", StringComparison.Ordinal));

        var withNineHours = MovementChecks.Review(
            movements,
            new Dictionary<string, string> { ["Ferie"] = "01" },
            ["Rossi Mario"],
            null,
            null,
            9m);
        Assert.DoesNotContain(withNineHours, finding => finding.Message.Contains("giornata standard", StringComparison.Ordinal));
    }

    [Fact]
    public void CodesThatAreNotConfiguredAreErrors()
    {
        var movements = new[]
        {
            new DailyMovement("Rossi Mario", "0012345", "Ferie", "99", new DateTime(2026, 5, 12), 8, 0)
        };

        var findings = MovementChecks.Review(
            movements,
            new Dictionary<string, string> { ["Ferie"] = "01" },
            ["Rossi Mario"],
            null,
            null);

        Assert.Contains(findings, finding =>
            finding.Severity == MovementSeverity.Error &&
            finding.Message.Contains("99", StringComparison.Ordinal));
    }

    [Fact]
    public void EmployeesWithoutMovementsAndDeclaredHoursDifferencesAreReported()
    {
        var movements = new[]
        {
            new DailyMovement("Rossi Mario", "0012345", "Ferie", "01", new DateTime(2026, 5, 12), 4, 0)
        };
        var requests = new[]
        {
            new AttendanceRequest("Rossi Mario", "Ferie", new DateTime(2026, 5, 12), new DateTime(2026, 5, 12), 8m, 4),
            new AttendanceRequest("Bianchi Anna", "Ferie", new DateTime(2026, 5, 13), new DateTime(2026, 5, 13), 8m, 5)
        };

        var findings = MovementChecks.Review(
            movements,
            new Dictionary<string, string> { ["Ferie"] = "01" },
            requests.Select(request => request.EmployeeName).ToArray(),
            null,
            null);

        Assert.Contains(findings, finding => finding.Message.Contains("Bianchi Anna", StringComparison.Ordinal));
        Assert.Contains(
            MovementChecks.CompareWithSource(movements, requests),
            finding => finding.Message.Contains("nel foglio 8h 00m", StringComparison.Ordinal));
    }

    [Fact]
    public void EmptyMovementSetsAreReportedAsErrors()
    {
        var findings = MovementChecks.Review([], new Dictionary<string, string>(), [], null, null);

        var finding = Assert.Single(findings);
        Assert.Equal(MovementSeverity.Error, finding.Severity);
    }

    // ---------- Aiuti dell'AI locale ----------

    [Theory]
    [InlineData("Il codice 07 non è fra quelli configurati", true)]
    [InlineData("F01 usato due volte", true)]
    [InlineData("Il dipendente ha 7h nel giorno 12/05", false)]
    [InlineData("Nessuna anomalia rilevata", false)]
    public void MessagesMentioningUnknownCodesAreRejected(string message, bool expected)
    {
        Assert.Equal(
            expected,
            LocalAiAssistant.MentionsUnexpectedCode(message, new HashSet<string>(["01", "02"], StringComparer.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ReviewSummaryContainsTotalsAndIndividualMovements()
    {
        var movements = new[]
        {
            new DailyMovement("Rossi Mario", "0012345", "Ferie", "01", new DateTime(2026, 5, 12), 7, 30),
            new DailyMovement("Bianchi Anna", "7654321", "Permesso", "02", new DateTime(2026, 5, 13), 1, 0)
        };

        var summary = LocalAiAssistant.BuildReviewSummary(movements);

        Assert.Contains("Movimenti totali: 2", summary, StringComparison.Ordinal);
        Assert.Contains("Rossi Mario: 7h 30m su 1 giorni", summary, StringComparison.Ordinal);
        Assert.Contains("2026-05-13 | Bianchi Anna | 7654321 | 02 | 1h00", summary, StringComparison.Ordinal);
        Assert.Empty(LocalAiAssistant.BuildReviewSummary([]));
    }

    [Theory]
    [InlineData("qwen2.5:3b", "qwen2.5:3b", true)]
    [InlineData("qwen2.5:3b", "qwen2.5:1.5b", false)]
    [InlineData("llama3.2:latest", "llama3.2", true)]
    [InlineData("qwen2.5:3b", "", false)]
    public void ModelReferencesAreComparedWithTheirTag(string installed, string preference, bool expected)
    {
        Assert.Equal(expected, LocalAiAssistant.MatchesModelReference(installed, preference));
    }

    [Theory]
    [InlineData("qwen3:4b", true)]
    [InlineData("deepseek-r1:1.5b", true)]
    [InlineData("qwen2.5:3b", false)]
    public void ReasoningModelsAreRecognized(string model, bool expected)
    {
        Assert.Equal(expected, LocalAiAssistant.SupportsThinking(model));
    }

    [Theory]
    [InlineData("  Bearer ghp_abc123\n", "ghp_abc123")]
    [InlineData("\"github_pat_xyz\"", "github_pat_xyz")]
    [InlineData("token github_pat_1 2 3", "github_pat_123")]
    [InlineData(null, "")]
    public void TokensAreCleanedBeforeUse(string? raw, string expected)
    {
        Assert.Equal(expected, UpdateService.SanitizeToken(raw));
    }

    [Fact]
    public void EnvironmentTokenWinsOverTheSessionTokenWhichWinsOverTheFile()
    {
        var previous = Environment.GetEnvironmentVariable(UpdateService.TokenEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(UpdateService.TokenEnvironmentVariable, "token-ambiente");
            UpdateService.SessionToken = "token-sessione";
            Assert.Equal("token-ambiente", UpdateService.ResolveToken());
            Assert.Contains("variabile d'ambiente", UpdateService.DescribeTokenSource(), StringComparison.Ordinal);

            Environment.SetEnvironmentVariable(UpdateService.TokenEnvironmentVariable, null);
            Assert.Equal("token-sessione", UpdateService.ResolveToken());
            Assert.Equal("impostato in questa sessione", UpdateService.DescribeTokenSource());
        }
        finally
        {
            Environment.SetEnvironmentVariable(UpdateService.TokenEnvironmentVariable, previous);
            UpdateService.SessionToken = null;
        }
    }
}
