using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Aggiornamenti sempre collegati (token incorporato dalla CI, tentativi, limite di
/// richieste) e database sempre disponibile (backup, ripristino, disinstallazione
/// con scelta di conservare o eliminare i dati).
/// </summary>
public class UpdatesAndDataTests
{
    /* --------------------------- Token incorporato ----------------------- */

    [Fact]
    public void EmbeddedTokenRoundTripsThroughTheObfuscation()
    {
        const string token = "github_pat_11ABCDEFG0abcdefghij_klmnopqrstuvwxyz0123456789";
        var (payload, key) = EmbeddedUpdateToken.Encode(token);

        Assert.DoesNotContain(token, payload, StringComparison.Ordinal);
        Assert.Equal(token, EmbeddedUpdateToken.Decode(payload, key));
    }

    [Fact]
    public void EmbeddedTokenUsesADifferentKeyForEveryBuild()
    {
        var first = EmbeddedUpdateToken.Encode("ghp_abcdefghijklmnopqrstuvwxyz0123456789");
        var second = EmbeddedUpdateToken.Encode("ghp_abcdefghijklmnopqrstuvwxyz0123456789");

        Assert.NotEqual(first.Key, second.Key);
        Assert.NotEqual(first.Payload, second.Payload);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("QUJD", "")]
    [InlineData("", "QUJD")]
    [InlineData("non-base64!", "QUJD")]
    [InlineData("QUJD", "non-base64!")]
    public void MissingOrUnreadableEmbeddedValuesProduceNoToken(string payload, string key)
    {
        Assert.Null(EmbeddedUpdateToken.Decode(payload, key));
    }

    [Fact]
    public void TheCommittedSourceCarriesNoTokenAndTheInjectionPatternsMatchIt()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            return;
        }

        var source = File.ReadAllText(Path.Combine(root, "PresenzeXmlConverter", "EmbeddedUpdateToken.cs"));
        // Nei sorgenti i valori sono vuoti: il token non finisce mai nel repository...
        Assert.Contains("private const string Payload = \"\";", source, StringComparison.Ordinal);
        Assert.Contains("private const string Key = \"\";", source, StringComparison.Ordinal);
        // ...e le espressioni usate dallo script di iniezione trovano le due costanti.
        Assert.Matches(new Regex("private const string Payload = \"[^\"]*\";"), source);
        Assert.Matches(new Regex("private const string Key = \"[^\"]*\";"), source);
    }

    [Fact]
    public void TheReleaseWorkflowInjectsTheTokenBeforeCompilingAndVerifiesIt()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            return;
        }

        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        var inject = workflow.IndexOf("Inject-UpdateToken.ps1", StringComparison.Ordinal);
        var publish = workflow.IndexOf("dotnet publish", StringComparison.Ordinal);
        Assert.True(inject > 0, "Il workflow di rilascio non inietta il token di aggiornamento.");
        Assert.True(inject < publish, "Il token va iniettato PRIMA della compilazione.");
        Assert.Contains("secrets.PRESENZEFLOW_GITHUB_TOKEN", workflow, StringComparison.Ordinal);
        // Un token scaduto non deve essere distribuito: viene provato prima.
        Assert.Contains("releases?per_page=1", workflow, StringComparison.Ordinal);

        var script = File.ReadAllText(Path.Combine(root, "tools", "Inject-UpdateToken.ps1"));
        // Il token arriva dall'ambiente, mai da un parametro (finirebbe nei log).
        Assert.Contains("$env:PRESENZEFLOW_GITHUB_TOKEN", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[string]$Token", script, StringComparison.Ordinal);
    }

    [Fact]
    public void UserTokensAreTriedBeforeTheEmbeddedOneWithoutDuplicates()
    {
        var previous = Environment.GetEnvironmentVariable(UpdateService.TokenEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(UpdateService.TokenEnvironmentVariable, "token-comune");
            UpdateService.SessionToken = "token-comune";

            var tokens = UpdateService.ResolveTokens();

            Assert.Equal("token-comune", tokens[0]);
            Assert.Equal(1, tokens.Count(token => token == "token-comune"));
            // Il token "utente" non comprende mai quello incorporato.
            Assert.Equal("token-comune", UpdateService.ResolveUserToken());
        }
        finally
        {
            Environment.SetEnvironmentVariable(UpdateService.TokenEnvironmentVariable, previous);
            UpdateService.SessionToken = null;
        }
    }

    /* ------------------------ Limite di richieste ------------------------ */

    [Fact]
    public void ARateLimitedForbiddenIsNotMistakenForABadToken()
    {
        using var limited = new HttpResponseMessage(HttpStatusCode.Forbidden);
        limited.Headers.Add("X-RateLimit-Remaining", "0");
        using var denied = new HttpResponseMessage(HttpStatusCode.Forbidden);
        using var tooMany = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        tooMany.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
        using var ok = new HttpResponseMessage(HttpStatusCode.OK);

        Assert.True(UpdateService.IsRateLimited(limited));
        Assert.True(UpdateService.IsRateLimited(tooMany));
        Assert.False(UpdateService.IsRateLimited(denied));
        Assert.False(UpdateService.IsRateLimited(ok));
    }

    [Fact]
    public void NetworkFailuresAreFlaggedAsTransientSoTheyAreRetriedSoon()
    {
        Assert.True(UpdateCheckResult.Failed("rete", transient: true).IsTransient);
        Assert.False(UpdateCheckResult.Failed("token").IsTransient);
        Assert.False(UpdateCheckResult.UpToDate.IsTransient);
    }

    /* ----------------------------- Database ------------------------------ */

    [Fact]
    public void BackupsAreConsistentCopiesAndOnlyTheNewestAreKept()
    {
        using var folder = new TempFolder();
        var database = new LocalDatabase(folder.PathOf("settings.db"));
        database.SaveEmployeeMapping("Mario Rossi", "1001");

        var backup = database.CreateBackup();
        Assert.True(File.Exists(backup));
        Assert.Empty(Directory.GetFiles(database.BackupDirectory, "*.tmp"));

        // Il backup è un database completo con gli stessi dati.
        var restored = new LocalDatabase(backup);
        Assert.Equal("1001", restored.GetEmployeeMappings()["Mario Rossi"]);

        for (var index = 0; index < LocalDatabase.MaximumBackups + 3; index++)
        {
            database.CreateBackup($"prova{index}");
        }
        Assert.Equal(LocalDatabase.MaximumBackups, database.ListBackups().Count);
    }

    [Fact]
    public void ABackupIsMadeOnlyWhenTheLastOneIsOldEnough()
    {
        using var folder = new TempFolder();
        var database = new LocalDatabase(folder.PathOf("settings.db"));

        Assert.NotNull(database.BackupIfDue(TimeSpan.FromHours(20)));
        Assert.Null(database.BackupIfDue(TimeSpan.FromHours(20)));
        Assert.Single(database.ListBackups());
    }

    [Fact]
    public void ACorruptDatabaseIsSetAsideAndRestoredFromTheLatestBackup()
    {
        using var folder = new TempFolder();
        var path = folder.PathOf("settings.db");
        var database = new LocalDatabase(path);
        database.SaveEmployeeMapping("Anna Verdi", "2002");
        database.SaveSetting("company-code", "AZ001");
        database.CreateBackup();
        database.Checkpoint();

        File.WriteAllText(path, "questo non è un database SQLite");
        foreach (var extra in new[] { path + "-wal", path + "-shm" })
        {
            if (File.Exists(extra))
            {
                File.Delete(extra);
            }
        }

        var opened = LocalDatabase.OpenWithRecovery(path);

        Assert.NotNull(opened.Note);
        Assert.Contains("ripristinato", opened.Note, StringComparison.Ordinal);
        Assert.Equal("2002", opened.Database.GetEmployeeMappings()["Anna Verdi"]);
        Assert.Equal("AZ001", opened.Database.GetSetting("company-code"));
        // Il file danneggiato non viene buttato: resta a disposizione.
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(folder.Path, "corrupt")));
    }

    [Fact]
    public void ACorruptDatabaseWithoutBackupsIsRecreatedEmptyInsteadOfStoppingTheApp()
    {
        using var folder = new TempFolder();
        var path = folder.PathOf("settings.db");
        File.WriteAllText(path, "contenuto illeggibile");

        var opened = LocalDatabase.OpenWithRecovery(path);

        Assert.NotNull(opened.Note);
        Assert.Contains("nuovo", opened.Note, StringComparison.Ordinal);
        Assert.Empty(opened.Database.GetEmployeeMappings());
        opened.Database.SaveSetting("prova", "ok");
        Assert.Equal("ok", opened.Database.GetSetting("prova"));
    }

    [Fact]
    public void AHealthyDatabaseOpensWithoutAnyNote()
    {
        using var folder = new TempFolder();

        var opened = LocalDatabase.OpenWithRecovery(folder.PathOf("settings.db"));

        Assert.Null(opened.Note);
    }

    [Fact]
    public void ANewerDatabaseIsNeverOverwrittenByTheRecovery()
    {
        using var folder = new TempFolder();
        var path = folder.PathOf("settings.db");
        _ = new LocalDatabase(path);
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA user_version = {LocalDatabase.CurrentSchemaVersion + 1};";
            command.ExecuteNonQuery();
        }

        var opened = LocalDatabase.OpenWithRecovery(path);

        // Si lavora su un database temporaneo e lo si dice; il file originale resta com'è.
        Assert.NotNull(opened.Note);
        Assert.True(opened.Database.GetHealth().IsTemporary);
        Assert.False(Directory.Exists(Path.Combine(folder.Path, "corrupt")));
        Assert.True(File.Exists(path));
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            Assert.Equal(
                LocalDatabase.CurrentSchemaVersion + 1,
                Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture));
        }
        File.Delete(opened.Database.DatabasePath);
    }

    [Fact]
    public void TheHealthSummaryReportsBackupsAndAge()
    {
        var now = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

        var none = new DatabaseHealth(@"C:\dati\settings.db", 20 * 1024, 2, 0, null, false);
        var recent = new DatabaseHealth(@"C:\dati\settings.db", 3 * 1024 * 1024, 2, 3, now.AddHours(-5), false);
        var temporary = new DatabaseHealth(@"C:\temp\settings.db", 2048, 2, 1, now.AddMinutes(-3), true);

        Assert.Equal("Database collegato · schema 2 · 20 KB · nessun backup", none.Describe(now));
        Assert.Equal("Database collegato · schema 2 · 3,0 MB · 3 backup (ultimo 5 h fa)", recent.Describe(now).Replace('.', ','));
        Assert.Contains("TEMPORANEO", temporary.Describe(now), StringComparison.Ordinal);
        Assert.Contains("3 min fa", temporary.Describe(now), StringComparison.Ordinal);
    }

    /* --------------------------- Disinstallazione ------------------------- */

    [Fact]
    public void UninstallingAsksWhetherToKeepOrDeleteTheDataAndNeverDeletesBySurprise()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            return;
        }

        var script = File.ReadAllText(Path.Combine(root, "Installer.iss")).Replace("\r\n", "\n");
        // Domanda esplicita con tre esiti e conferma doppia prima di cancellare.
        Assert.Contains("Vuoi CONSERVARE i dati di PresenzeFlow?", script, StringComparison.Ordinal);
        Assert.Contains("MB_YESNOCANCEL", script, StringComparison.Ordinal);
        Assert.Contains("MB_DEFBUTTON2", script, StringComparison.Ordinal);
        // In silenzioso si conserva, salvo richiesta esplicita.
        Assert.Contains("/DELETEDATA", script, StringComparison.Ordinal);
        Assert.Contains("/KEEPDATA", script, StringComparison.Ordinal);
        // Eliminando si toglie tutta la cartella dei dati (database, backup, token), ma solo se il nome è quello atteso.
        Assert.Contains("DelTree(DataDirectoryPath, True, True, True)", script, StringComparison.Ordinal);
        Assert.Contains("function DataDirectoryIsSafeToDelete", script, StringComparison.Ordinal);
        Assert.Contains("'PresenzeXmlConverter'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDataFolderUsedByTheAppIsTheOneTheInstallerManages()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            return;
        }

        var script = File.ReadAllText(Path.Combine(root, "Installer.iss"));
        Assert.Equal("PresenzeXmlConverter", Path.GetFileName(LocalDatabase.DataDirectory));
        Assert.Contains(@"{localappdata}\DeGioia\PresenzeXmlConverter", script, StringComparison.Ordinal);
        Assert.Equal(
            Path.Combine(LocalDatabase.DataDirectory, "settings.db"),
            LocalDatabase.DefaultDatabasePath);
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Installer.iss")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return null;
    }

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"presenzeflow-dati-{Guid.NewGuid():N}");
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
