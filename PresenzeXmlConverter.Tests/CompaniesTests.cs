using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter.Tests;

/// <summary>Anagrafica delle aziende: salvata nel database, mai persa e senza duplicati.</summary>
public class CompaniesTests
{
    [Fact]
    public void CompaniesAreSavedUpdatedAndRemoved()
    {
        using var folder = new TempDirectory();
        var database = new LocalDatabase(folder.PathOf("settings.db"));

        database.SaveCompany("az001", "Rossi Srl", "sede di Milano");
        database.SaveCompany("BB22", "Bianchi Spa");
        database.SaveCompany("AZ001", "Rossi S.r.l.");

        var companies = database.GetCompanies();
        Assert.Equal(2, companies.Count);
        var rossi = Assert.Single(companies, company => company.Code.Equals("AZ001", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Rossi S.r.l.", rossi.Name);
        Assert.Equal(string.Empty, rossi.Notes);
        Assert.Equal("AZ001 · Rossi S.r.l.".Replace("AZ001", rossi.Code), rossi.Label);

        Assert.True(database.DeleteCompany("bb22"));
        Assert.False(database.DeleteCompany("bb22"));
        Assert.Single(database.GetCompanies());
    }

    [Fact]
    public void ACodeUsedInAConversionDoesNotEraseTheCompanyDetails()
    {
        using var folder = new TempDirectory();
        var database = new LocalDatabase(folder.PathOf("settings.db"));
        database.SaveCompany("AZ001", "Rossi Srl", "note");

        database.SaveCompany("AZ001", string.Empty, string.Empty, keepExistingDetails: true);
        database.SaveCompany("NEW01", string.Empty, string.Empty, keepExistingDetails: true);

        var companies = database.GetCompanies().ToDictionary(company => company.Code, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Rossi Srl", companies["AZ001"].Name);
        Assert.Equal("note", companies["AZ001"].Notes);
        Assert.Equal(string.Empty, companies["NEW01"].Name);
        Assert.Equal("NEW01", companies["NEW01"].Label);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TROPPO7")]
    public void InvalidCompanyCodesAreRejected(string code)
    {
        using var folder = new TempDirectory();
        var database = new LocalDatabase(folder.PathOf("settings.db"));

        Assert.Throws<InvalidDataException>(() => database.SaveCompany(code, "X"));
        Assert.Empty(database.GetCompanies());
    }

    [Fact]
    public void TheCompanyAlreadyUsedByAnOlderVersionEntersTheRegistry()
    {
        using var folder = new TempDirectory();
        var path = folder.PathOf("settings.db");
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE Settings (Key TEXT PRIMARY KEY NOT NULL, Value TEXT NOT NULL);
                INSERT INTO Settings VALUES ('company-code', 'AZ777');
                PRAGMA user_version = 3;
                """;
            command.ExecuteNonQuery();
        }

        var database = new LocalDatabase(path);

        var company = Assert.Single(database.GetCompanies());
        Assert.Equal("AZ777", company.Code);
    }

    [Fact]
    public void CompaniesSurviveABackupAndRestore()
    {
        using var folder = new TempDirectory();
        var database = new LocalDatabase(folder.PathOf("settings.db"));
        database.SaveCompany("AZ001", "Rossi Srl");

        var backup = database.CreateBackup("prova");
        var restored = new LocalDatabase(backup);

        Assert.Equal("Rossi Srl", Assert.Single(restored.GetCompanies()).Name);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"presenzeflow-comp-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string PathOf(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
