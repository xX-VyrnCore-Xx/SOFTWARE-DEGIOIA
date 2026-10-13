using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Consegne tracciate nel database (impronta, file sorgente, copia dell'XML),
/// migrazione dello schema e nome proposto per il file esportato.
/// </summary>
public class ConversionAndDataTests
{
    private static DailyMovement Movement(string employee, string reason, int day, int hours = 8, int minutes = 0) =>
        new("Mario Rossi", employee, "Ferie", reason, new DateTime(2026, 8, day), hours, minutes, 3);

    /* --------------------------- Impronta e nome ------------------------- */

    [Fact]
    public void TheFingerprintIgnoresRowOrderAndLetterCaseButNotContent()
    {
        var a = new[] { Movement("1001", "F01", 10), Movement("1002", "F01", 11) };
        var reordered = new[] { Movement("1002", "f01", 11), Movement("1001", "F01", 10) };
        var otherHours = new[] { Movement("1001", "F01", 10), Movement("1002", "F01", 11, 4) };

        Assert.Equal(ExportHistory.Fingerprint("az001", a), ExportHistory.Fingerprint(" AZ001 ", reordered));
        Assert.NotEqual(ExportHistory.Fingerprint("AZ001", a), ExportHistory.Fingerprint("AZ001", otherHours));
        Assert.NotEqual(ExportHistory.Fingerprint("AZ001", a), ExportHistory.Fingerprint("AZ002", a));
        Assert.Equal(64, ExportHistory.Fingerprint("AZ001", a).Length);
    }

    [Fact]
    public void TheSuggestedFileNameCarriesCompanyAndPeriod()
    {
        var sameMonth = new[] { Movement("1001", "F01", 3), Movement("1001", "F01", 28) };
        var acrossMonths = new[]
        {
            Movement("1001", "F01", 3),
            new DailyMovement("Mario Rossi", "1001", "Ferie", "F01", new DateTime(2026, 9, 2), 8, 0, 4)
        };

        Assert.Equal("TRRIPW_AZ001_2026-08.xml", ExportHistory.SuggestFileName("az001", @"C:\x\presenze.xlsx", sameMonth));
        Assert.Equal("TRRIPW_AZ001_20260803-20260902.xml", ExportHistory.SuggestFileName("AZ001", null, acrossMonths));
        Assert.Equal("TRRIPW_2026-08.xml", ExportHistory.SuggestFileName(" ", null, sameMonth));
        Assert.Equal("presenze_agosto_TRRIPW.xml", ExportHistory.SuggestFileName("AZ001", "presenze_agosto.xlsx", []));
        Assert.Equal("presenze_TRRIPW.xml", ExportHistory.SuggestFileName("", null, []));
    }

    /* ------------------------ Consegne nel database ---------------------- */

    [Fact]
    public void ADeliveryKeepsItsFingerprintSourceAndACompressedCopyOfTheXml()
    {
        using var folder = new TempDirectory();
        var database = new LocalDatabase(folder.PathOf("settings.db"));
        var xml = System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><Movimenti>" + new string('x', 5000) + "</Movimenti>");
        var record = new ExportRecord(
            new DateTime(2026, 8, 31, 9, 0, 0, DateTimeKind.Utc),
            "TRRIPW_AZ001_2026-08.xml",
            @"C:\lavoro\TRRIPW_AZ001_2026-08.xml",
            "AZ001",
            2,
            14,
            6720,
            0,
            "abc123",
            "presenze.xlsx");

        var id = database.SaveExport(record, xml);

        var stored = Assert.Single(database.GetExports());
        Assert.Equal(id, stored.Id);
        Assert.Equal("abc123", stored.Fingerprint);
        Assert.Equal("presenze.xlsx", stored.SourceName);
        Assert.True(stored.HasStoredXml);
        Assert.Equal(xml.Length, stored.StoredBytes);
        Assert.Equal(xml, database.GetExportXml(id));
        Assert.Equal(id, database.FindExportByFingerprint("abc123")!.Id);
        Assert.Null(database.FindExportByFingerprint("diversa"));
        Assert.Null(database.FindExportByFingerprint(""));
    }

    [Fact]
    public void ADeliveryWithoutACopyStillAppearsInTheHistory()
    {
        using var folder = new TempDirectory();
        var database = new LocalDatabase(folder.PathOf("settings.db"));

        var id = database.SaveExport(new ExportRecord(DateTime.UtcNow, "a.xml", @"C:\a.xml", "AZ001", 1, 1, 60));

        Assert.False(Assert.Single(database.GetExports()).HasStoredXml);
        Assert.Null(database.GetExportXml(id));
    }

    [Fact]
    public void TheCopiesLeaveTogetherWithTheirHistoryEntries()
    {
        using var folder = new TempDirectory();
        var database = new LocalDatabase(folder.PathOf("settings.db"));
        long first = 0;
        for (var index = 0; index < ExportHistory.MaximumEntries + 3; index++)
        {
            var id = database.SaveExport(
                new ExportRecord(DateTime.UtcNow.AddMinutes(index), $"{index}.xml", $@"C:\{index}.xml", "AZ001", 1, 1, 60),
                [1, 2, 3, (byte)index]);
            if (index == 0)
            {
                first = id;
            }
        }

        Assert.Equal(ExportHistory.MaximumEntries, database.GetExportCount());
        // La voce più vecchia è uscita e con lei la sua copia: niente righe orfane.
        Assert.Null(database.GetExportXml(first));
        using var connection = new SqliteConnection($"Data Source={folder.PathOf("settings.db")};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ExportFiles WHERE ExportId NOT IN (SELECT Id FROM Exports);";
        Assert.Equal(0L, Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void AnOversizedXmlIsNotCopiedIntoTheDatabase()
    {
        using var folder = new TempDirectory();
        var database = new LocalDatabase(folder.PathOf("settings.db"));
        var huge = new byte[LocalDatabase.MaximumStoredXmlBytes + 1];

        var id = database.SaveExport(new ExportRecord(DateTime.UtcNow, "g.xml", @"C:\g.xml", "AZ001", 1, 1, 60), huge);

        Assert.Single(database.GetExports());
        Assert.False(database.GetExports()[0].HasStoredXml);
        Assert.Null(database.GetExportXml(id));
    }

    [Fact]
    public void ADatabaseFromTheOldSchemaIsUpgradedWithoutLosingTheHistory()
    {
        using var folder = new TempDirectory();
        var path = folder.PathOf("settings.db");
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE Settings (Key TEXT PRIMARY KEY NOT NULL, Value TEXT NOT NULL);
                CREATE TABLE Employees (Name TEXT PRIMARY KEY NOT NULL COLLATE NOCASE, Code TEXT NOT NULL);
                CREATE TABLE Policies (Name TEXT PRIMARY KEY NOT NULL COLLATE NOCASE, Code TEXT NOT NULL);
                CREATE TABLE Exports (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ExportedAtUtc TEXT NOT NULL,
                    FileName TEXT NOT NULL,
                    FilePath TEXT NOT NULL,
                    CompanyCode TEXT NOT NULL,
                    Employees INTEGER NOT NULL,
                    Movements INTEGER NOT NULL,
                    TotalMinutes INTEGER NOT NULL);
                INSERT INTO Employees VALUES ('Anna Verdi', '2002');
                INSERT INTO Exports (ExportedAtUtc, FileName, FilePath, CompanyCode, Employees, Movements, TotalMinutes)
                    VALUES ('2026-07-01T08:00:00.0000000Z', 'vecchio.xml', 'C:\vecchio.xml', 'AZ001', 3, 20, 9600);
                PRAGMA user_version = 2;
                """;
            command.ExecuteNonQuery();
        }

        var database = new LocalDatabase(path);

        // I dati di prima ci sono ancora, con le colonne nuove vuote.
        Assert.Equal("2002", database.GetEmployeeMappings()["Anna Verdi"]);
        var old = Assert.Single(database.GetExports());
        Assert.Equal("vecchio.xml", old.FileName);
        Assert.Equal(string.Empty, old.Fingerprint);
        Assert.False(old.HasStoredXml);
        Assert.Equal(LocalDatabase.CurrentSchemaVersion, database.GetHealth().SchemaVersion);
        // Prima di cambiare lo schema è stata tenuta una copia di sicurezza.
        Assert.Contains(database.ListBackups(), backup => backup.Path.Contains("prima-di-aggiornare", StringComparison.Ordinal));
        // Il database aggiornato funziona per le consegne nuove.
        var id = database.SaveExport(new ExportRecord(DateTime.UtcNow, "nuovo.xml", @"C:\n.xml", "AZ001", 1, 1, 60, 0, "f1", "s.xlsx"), [9, 9]);
        Assert.Equal(new byte[] { 9, 9 }, database.GetExportXml(id));
    }

    [Fact]
    public void TheShortBackupSummaryFitsTheNarrowSidebar()
    {
        var now = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal("nessun backup", new DatabaseHealth("x", 1, 3, 0, null, false).DescribeBackupsShort(now));
        Assert.Equal("3 backup · 5 h fa", new DatabaseHealth("x", 1, 3, 3, now.AddHours(-5), false).DescribeBackupsShort(now));
        Assert.True(new DatabaseHealth("x", 1, 3, 10, now.AddDays(-12), false).DescribeBackupsShort(now).Length <= 22);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"presenzeflow-conv-{Guid.NewGuid():N}");
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
