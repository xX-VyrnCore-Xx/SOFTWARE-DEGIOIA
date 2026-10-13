using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter.Tests;

/// <summary>Regole di conversione per azienda: salvate nel database e applicate da sole.</summary>
public class CompanyRulesTests
{
    [Fact]
    public void RulesSurviveAJsonRoundTripAndProduceTheSameOptions()
    {
        var rules = new CompanyRules(7.5m, true, false, "02/06, 24/12");

        var restored = CompanyRules.TryParse(rules.ToJson());

        Assert.Equal(rules, restored);
        var options = restored!.ToOptions(2026);
        Assert.Equal(7.5m, options.HoursPerDay);
        Assert.True(options.IncludeSaturdays);
        Assert.False(options.AutoDetect);
        Assert.Equal(
            new[] { new DateTime(2026, 6, 2), new DateTime(2026, 12, 24) },
            options.ExtraHolidays!.OrderBy(day => day).ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("non è json")]
    [InlineData("{\"hoursPerDay\":0,\"includeSaturdays\":false,\"autoDetect\":true,\"holidays\":\"\"}")]
    [InlineData("{\"hoursPerDay\":30,\"includeSaturdays\":false,\"autoDetect\":true,\"holidays\":\"\"}")]
    public void UnreadableOrOutOfRangeRulesMeanNoRules(string json)
    {
        Assert.Null(CompanyRules.TryParse(json));
    }

    [Fact]
    public void RulesBuiltFromTheGeneralOptionsReproduceThem()
    {
        var general = new GenerationOptions(6m, true, [new DateTime(2026, 8, 14)], false);

        var rules = CompanyRules.From(general, 2026);

        Assert.True(GenerationSettings.AreEquivalent(general, rules.ToOptions(2026)));
    }

    [Fact]
    public void EachCompanyKeepsItsOwnRulesInTheDatabase()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"presenzeflow-rules-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var database = new LocalDatabase(Path.Combine(folder, "settings.db"));
            database.SaveCompany("AZ001", "Rossi Srl");
            database.SaveCompany("BB22", "Bianchi Spa");

            database.SaveCompanyRules("AZ001", new CompanyRules(6m, true, true, string.Empty));
            database.SaveCompanyRules("NEW01", new CompanyRules(8m, false, true, "01/05"));

            var companies = database.GetCompanies().ToDictionary(company => company.Code, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(6m, companies["AZ001"].Rules!.HoursPerDay);
            Assert.Null(companies["BB22"].Rules);
            Assert.Equal("01/05", companies["NEW01"].Rules!.Holidays);
            // Rinominare l'azienda non cancella le regole.
            database.SaveCompany("AZ001", "Rossi S.r.l.", "note");
            Assert.NotNull(database.GetCompanies().Single(company => company.Code == "AZ001").Rules);

            database.SaveCompanyRules("AZ001", null);
            Assert.Null(database.GetCompanies().Single(company => company.Code == "AZ001").Rules);
            Assert.Throws<InvalidDataException>(() => database.SaveCompanyRules("TROPPO7", null));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ADatabaseOfTheVersionBeforeGetsTheRulesColumn()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"presenzeflow-rules-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "settings.db");
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE Settings (Key TEXT PRIMARY KEY NOT NULL, Value TEXT NOT NULL);
                    CREATE TABLE Companies (Code TEXT PRIMARY KEY NOT NULL COLLATE NOCASE, Name TEXT NOT NULL DEFAULT '', Notes TEXT NOT NULL DEFAULT '', UpdatedUtc TEXT NOT NULL DEFAULT '');
                    INSERT INTO Companies VALUES ('AZ001', 'Rossi', '', '');
                    PRAGMA user_version = 4;
                    """;
                command.ExecuteNonQuery();
            }

            var database = new LocalDatabase(path);

            var company = Assert.Single(database.GetCompanies());
            Assert.Equal("Rossi", company.Name);
            Assert.Null(company.Rules);
            database.SaveCompanyRules("AZ001", new CompanyRules(8m, false, true, string.Empty));
            Assert.NotNull(Assert.Single(database.GetCompanies()).Rules);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(folder, recursive: true);
        }
    }
}
