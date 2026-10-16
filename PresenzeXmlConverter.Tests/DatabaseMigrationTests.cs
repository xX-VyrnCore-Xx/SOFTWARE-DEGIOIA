using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter.Tests;

public class DatabaseMigrationTests
{
    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "presenze-mig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void Cleanup(string directory)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static List<string> IndexNames(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' ORDER BY name;";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }
        return names;
    }

    [Fact]
    public void MigrationListIsConsecutiveAndDescribed()
    {
        Assert.Null(DatabaseMigrations.Validate());
        Assert.Equal(DatabaseMigrations.Latest, LocalDatabase.CurrentSchemaVersion);
        Assert.True(LocalDatabase.CurrentSchemaVersion > DatabaseMigrations.BaselineVersion);
        Assert.Empty(DatabaseMigrations.Pending(DatabaseMigrations.Latest));
        Assert.Equal(DatabaseMigrations.Steps.Count, DatabaseMigrations.Pending(DatabaseMigrations.BaselineVersion).Count());
    }

    [Fact]
    public void NewDatabaseHasTheCurrentVersionAndTheListIndexes()
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.db");
            var database = new LocalDatabase(path);
            Assert.Equal(LocalDatabase.CurrentSchemaVersion, database.GetHealth().SchemaVersion);
            var indexes = IndexNames(path);
            foreach (var expected in new[] { "Exports_Recent", "Exports_Company", "Activity_Recent", "Activity_Kind", "Exports_Fingerprint" })
            {
                Assert.Contains(expected, indexes);
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void DatabaseAtTheBaselineVersionKeepsItsDataAndGainsTheIndexes()
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.db");
            var first = new LocalDatabase(path);
            first.SaveEmployeeMapping("Anna Verdi", "2002");
            first.AddActivity(ActivityKind.Import, "Importato", "file.xlsx");
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    DROP INDEX Exports_Recent; DROP INDEX Exports_Company; DROP INDEX Activity_Recent; DROP INDEX Activity_Kind;
                    PRAGMA user_version = 5;
                    """;
                command.ExecuteNonQuery();
            }

            var reopened = new LocalDatabase(path);
            Assert.Equal("2002", reopened.GetEmployeeMappings()["Anna Verdi"]);
            Assert.Single(reopened.GetActivities());
            Assert.Equal(LocalDatabase.CurrentSchemaVersion, reopened.GetHealth().SchemaVersion);
            Assert.Contains("Exports_Recent", IndexNames(path));
            Assert.Contains("Activity_Kind", IndexNames(path));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void BackupsAreVerifiedAndDamagedOnesAreReported()
    {
        var directory = NewDirectory();
        try
        {
            var database = new LocalDatabase(Path.Combine(directory, "settings.db"));
            database.SaveEmployeeMapping("Anna Verdi", "2002");
            database.CreateBackup("prova");
            Assert.True(database.VerifyBackups().AllGood);
            Assert.Contains("1 backup controllati", database.VerifyBackups().Describe(), StringComparison.Ordinal);

            File.WriteAllText(Path.Combine(database.BackupDirectory, "settings-rotto-20200101-000000000.db"), "non è un database");
            var report = database.VerifyBackups();
            Assert.False(report.AllGood);
            Assert.Equal(1, report.Good);
            Assert.Single(report.Damaged);
            Assert.Contains("danneggiati", report.Describe(), StringComparison.Ordinal);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void OptimizeIsHarmlessAndRemovesOrphanedXmlCopies()
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.db");
            var database = new LocalDatabase(path);
            var id = database.SaveExport(new ExportRecord(DateTime.UtcNow, "a.xml", @"C:\a.xml", "AZ001", 1, 1, 60, 0, "f", "s.xlsx"), [1, 2, 3]);
            database.Optimize();
            Assert.Equal(new byte[] { 1, 2, 3 }, database.GetExportXml(id));
            Assert.Equal(1, database.GetExportCount());
        }
        finally
        {
            Cleanup(directory);
        }
    }
}
