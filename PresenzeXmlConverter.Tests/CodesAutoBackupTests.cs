using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public sealed class CodesAutoBackupTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vp-auto-" + Guid.NewGuid().ToString("N"));

    public CodesAutoBackupTests() => Directory.CreateDirectory(_folder);

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

    private LocalDatabase NewDatabase(string name = "a") => new(Path.Combine(_folder, name + ".db"));

    [Fact]
    public void EmptyDatabaseWritesNothing()
    {
        var database = NewDatabase();

        Assert.Null(CodesAutoBackup.Run(database, new DateTime(2026, 10, 15, 9, 0, 0)));
        Assert.Empty(CodesAutoBackup.List(CodesAutoBackup.FolderFor(database)));
    }

    [Fact]
    public void FirstRunWritesAFileThatCanBeImportedOnAnotherDatabase()
    {
        var database = NewDatabase();
        database.SaveEmployeeMapping("Rossi Mario", "0001234");
        database.SavePolicyMapping("Ferie", "FE");
        database.SaveCompany("AZ001", "Rossi Srl");

        var path = CodesAutoBackup.Run(database, new DateTime(2026, 10, 15, 9, 0, 0));

        Assert.NotNull(path);
        Assert.EndsWith("Vyrn-Presence-codici-auto-2026-10-15.json", path);
        Assert.True(CodesBackup.TryParse(File.ReadAllText(path!), out var backup, out var error), error);
        var other = NewDatabase("b");
        backup.ApplyTo(other);
        Assert.Equal(database.GetEmployeeMappings(), other.GetEmployeeMappings());
        Assert.Equal(database.GetCompanies(), other.GetCompanies());
    }

    [Fact]
    public void UnchangedDataDoesNotWriteAgainEvenOnAnotherDay()
    {
        var database = NewDatabase();
        database.SaveEmployeeMapping("Rossi Mario", "0001234");
        CodesAutoBackup.Run(database, new DateTime(2026, 10, 15, 9, 0, 0));

        Assert.Null(CodesAutoBackup.Run(database, new DateTime(2026, 10, 15, 18, 0, 0)));
        Assert.Null(CodesAutoBackup.Run(database, new DateTime(2026, 10, 16, 9, 0, 0)));
        Assert.Single(CodesAutoBackup.List(CodesAutoBackup.FolderFor(database)));
    }

    [Fact]
    public void ChangesTheSameDayReplaceTheFileAndANewDayAddsOne()
    {
        var database = NewDatabase();
        database.SaveEmployeeMapping("Rossi Mario", "0001234");
        CodesAutoBackup.Run(database, new DateTime(2026, 10, 15, 9, 0, 0));

        database.SaveEmployeeMapping("Bianchi Anna", "0005678");
        var sameDay = CodesAutoBackup.Run(database, new DateTime(2026, 10, 15, 18, 0, 0));
        Assert.NotNull(sameDay);
        Assert.Single(CodesAutoBackup.List(CodesAutoBackup.FolderFor(database)));
        Assert.Contains("Bianchi Anna", File.ReadAllText(sameDay!));

        database.SaveEmployeeMapping("Verdi Luca", "0009999");
        CodesAutoBackup.Run(database, new DateTime(2026, 10, 16, 9, 0, 0));
        var files = CodesAutoBackup.List(CodesAutoBackup.FolderFor(database));
        Assert.Equal(2, files.Count);
        Assert.EndsWith("2026-10-16.json", files[0]);
    }

    [Fact]
    public void OnlyTheLatestCopiesAreKept()
    {
        var database = NewDatabase();
        for (var day = 1; day <= 10; day++)
        {
            database.SaveEmployeeMapping("Dipendente " + day, day.ToString("0000000"));
            CodesAutoBackup.Run(database, new DateTime(2026, 10, day, 9, 0, 0), keep: 3);
        }

        var files = CodesAutoBackup.List(CodesAutoBackup.FolderFor(database));
        Assert.Equal(3, files.Count);
        Assert.EndsWith("2026-10-10.json", files[0]);
        Assert.EndsWith("2026-10-08.json", files[2]);
    }

    [Fact]
    public void ABrokenLatestCopyIsReplacedNotTrusted()
    {
        var database = NewDatabase();
        database.SaveEmployeeMapping("Rossi Mario", "0001234");
        var path = CodesAutoBackup.Run(database, new DateTime(2026, 10, 15, 9, 0, 0))!;
        File.WriteAllText(path, "{ rovinato");

        var again = CodesAutoBackup.Run(database, new DateTime(2026, 10, 15, 10, 0, 0));

        Assert.NotNull(again);
        Assert.True(CodesBackup.TryParse(File.ReadAllText(again!), out _, out _));
    }
}
