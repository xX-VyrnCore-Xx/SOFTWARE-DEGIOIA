using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public sealed class CodesBackupTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vp-codes-" + Guid.NewGuid().ToString("N"));

    public CodesBackupTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
        }
    }

    private LocalDatabase NewDatabase(string name) => new(Path.Combine(_folder, name + ".db"));

    private static LocalDatabase Fill(LocalDatabase database)
    {
        database.SaveEmployeeMapping("Rossi Mario", "0001234");
        database.SaveEmployeeMapping("Bianchi Anna", "0005678");
        database.SavePolicyMapping("Ferie", "FE");
        database.SavePolicyMapping("Permesso", "PE");
        database.SaveEmployeeProfile("Rossi Mario", new EmployeeProfile(6m, new DateTime(2026, 3, 1), null));
        database.SaveCompany("AZ001", "Rossi Srl", "cliente storico");
        database.SaveCompanyRules("AZ001", new CompanyRules(7.5m, true, false, "12-24,12-25"));
        database.SaveCompany("AZ002", "Verdi Spa");
        return database;
    }

    [Fact]
    public void ExportAndImportOnAnotherDatabaseReproducesEverything()
    {
        var source = Fill(NewDatabase("a"));
        var json = CodesBackup.Read(source, new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc)).ToJson();

        Assert.True(CodesBackup.TryParse(json, out var backup, out var error), error);
        var target = NewDatabase("b");
        var report = backup.ApplyTo(target);

        Assert.Empty(report.Skipped);
        Assert.Equal(source.GetEmployeeMappings(), target.GetEmployeeMappings());
        Assert.Equal(source.GetPolicyMappings(), target.GetPolicyMappings());
        Assert.Equal(source.GetEmployeeProfiles(), target.GetEmployeeProfiles());
        Assert.Equal(source.GetCompanies(), target.GetCompanies());
        Assert.Equal(7, report.Added);
        Assert.Equal(0, report.Updated);
    }

    [Fact]
    public void ImportingTwiceChangesNothing()
    {
        var source = Fill(NewDatabase("a"));
        Assert.True(CodesBackup.TryParse(CodesBackup.Read(source, DateTime.UtcNow).ToJson(), out var backup, out _));
        var target = NewDatabase("b");
        backup.ApplyTo(target);

        var second = backup.ApplyTo(target);

        Assert.Equal(0, second.Changed);
        Assert.Equal(7, second.Unchanged);
        Assert.Contains("Nessuna novità", second.Describe());
    }

    [Fact]
    public void ImportUpdatesDifferentCodesAndKeepsWhatIsNotInTheFile()
    {
        var source = NewDatabase("a");
        source.SaveEmployeeMapping("Rossi Mario", "0009999");
        Assert.True(CodesBackup.TryParse(CodesBackup.Read(source, DateTime.UtcNow).ToJson(), out var backup, out _));
        var target = Fill(NewDatabase("b"));

        var report = backup.ApplyTo(target);

        Assert.Equal(1, report.Updated);
        Assert.Equal("0009999", target.GetEmployeeMappings()["Rossi Mario"]);
        Assert.Equal("0005678", target.GetEmployeeMappings()["Bianchi Anna"]);
        Assert.Equal(2, target.GetPolicyMappings().Count);
        Assert.Equal(2, target.GetCompanies().Count);
    }

    [Fact]
    public void ReorderedNameIsTheSamePersonAndIsNotDuplicated()
    {
        var source = NewDatabase("a");
        source.SaveEmployeeMapping("Mario Rossi", "0001234");
        Assert.True(CodesBackup.TryParse(CodesBackup.Read(source, DateTime.UtcNow).ToJson(), out var backup, out _));
        var target = NewDatabase("b");
        target.SaveEmployeeMapping("Rossi Mario", "0001234");

        var report = backup.ApplyTo(target);

        Assert.Equal(1, report.Unchanged);
        Assert.Single(target.GetEmployeeMappings());
    }

    [Fact]
    public void InvalidEntriesAreSkippedOneByOneWithoutStoppingTheRest()
    {
        var backup = new CodesBackup(
            CodesBackup.CurrentVersion,
            DateTime.UtcNow,
            [new CodePair("Rossi Mario", "0001234"), new CodePair("Troppo Lungo", "12345678901"), new CodePair("", "1")],
            [new CodePair("Ferie", "FERIE")],
            [],
            [new CompanyEntry("CODICE-TROPPO-LUNGO", "X", "", null), new CompanyEntry("AZ001", "Rossi Srl", "", null)]);
        var target = NewDatabase("b");

        var report = backup.ApplyTo(target);

        Assert.Equal(4, report.Skipped.Count);
        Assert.Equal(2, report.Added);
        Assert.Contains("Rossi Mario", target.GetEmployeeMappings().Keys);
        Assert.Contains("AZ001", target.GetCompanies().Select(company => company.Code));
        Assert.Contains("scartati", report.Describe());
    }

    [Theory]
    [InlineData("")]
    [InlineData("non è json")]
    [InlineData("{\"format\":\"altro\",\"version\":1}")]
    [InlineData("[1,2,3]")]
    public void NotACodesFileIsRefusedWithAReason(string text)
    {
        Assert.False(CodesBackup.TryParse(text, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void FileFromANewerVersionIsRefused()
    {
        var json = new CodesBackup(CodesBackup.CurrentVersion, DateTime.UtcNow, [], [], [], []).ToJson()
            .Replace("\"Version\": 1", "\"Version\": 99", StringComparison.Ordinal);

        Assert.False(CodesBackup.TryParse(json, out _, out var error));
        Assert.Contains("più recente", error);
    }

    [Fact]
    public void DescribeAndFileNameAreReadable()
    {
        var backup = CodesBackup.Read(Fill(NewDatabase("a")), DateTime.UtcNow);

        Assert.Equal("2 dipendenti · 2 giustificativi · 2 aziende · 1 profili orari", backup.Describe());
        Assert.Equal("Vyrn-Presence-codici-2026-10-15.json", CodesBackup.SuggestFileName(new DateTime(2026, 10, 15)));
    }
}
