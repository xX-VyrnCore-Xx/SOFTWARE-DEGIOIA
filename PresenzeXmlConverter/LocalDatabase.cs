using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter;

/// <summary>Un backup del database: file, momento di creazione (UTC) e dimensione.</summary>
internal sealed record DatabaseBackup(string Path, DateTime CreatedUtc, long SizeBytes);

/// <summary>Database aperto e, se serviva, il racconto di che cosa è stato fatto per riaprirlo.</summary>
internal sealed record LocalDatabaseOpenResult(LocalDatabase Database, string? Note);

/// <summary>Stato del database in una forma adatta a essere mostrata all'utente.</summary>
internal sealed record DatabaseHealth(
    string Path,
    long SizeBytes,
    int SchemaVersion,
    int BackupCount,
    DateTime? LastBackupUtc,
    bool IsTemporary)
{
    public string Describe(DateTime nowUtc)
    {
        var size = SizeBytes < 1024 * 1024
            ? $"{Math.Max(1, SizeBytes / 1024)} KB"
            : $"{SizeBytes / (1024.0 * 1024.0):0.0} MB";
        var backups = BackupCount == 0
            ? "nessun backup"
            : $"{BackupCount} backup (ultimo {DescribeAge(nowUtc)})";
        var storage = IsTemporary ? " · TEMPORANEO: si perde alla pulizia di Windows" : string.Empty;
        return $"Database collegato · schema {SchemaVersion} · {size} · {backups}{storage}";
    }

    /// <summary>Riga breve per la barra laterale ("3 backup · 5 h fa").</summary>
    public string DescribeBackupsShort(DateTime nowUtc) =>
        BackupCount == 0 ? "nessun backup" : $"{BackupCount} backup · {DescribeAge(nowUtc)}";

    private string DescribeAge(DateTime nowUtc)
    {
        if (LastBackupUtc is not { } last)
        {
            return "mai";
        }
        var age = nowUtc - last;
        if (age < TimeSpan.FromMinutes(1))
        {
            return "adesso";
        }
        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes} min fa";
        }
        if (age < TimeSpan.FromDays(1))
        {
            return $"{(int)age.TotalHours} h fa";
        }
        return $"{(int)age.TotalDays} g fa";
    }
}

internal sealed class LocalDatabase
{
    /// <summary>
    /// Versione dello schema: 1 = preferenze e abbinamenti, 2 = storico delle
    /// esportazioni, 3 = impronta e file sorgente di ogni esportazione e copia
    /// dell'XML consegnato, 4 = anagrafica delle aziende, 5 = regole di conversione per azienda. Un database di versione superiore è stato scritto da
    /// una build più recente e non va aperto a metà.
    /// </summary>
    public const int CurrentSchemaVersion = 5;

    /// <summary>Quanti backup restano: i più vecchi vengono eliminati.</summary>
    public const int MaximumBackups = 10;

    private readonly string _connectionString;

    /// <summary>Cartella dei dati dell'utente (la stessa che l'installer chiede se conservare o eliminare).</summary>
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeGioia",
        "PresenzeXmlConverter");

    public static string DefaultDatabasePath => Path.Combine(DataDirectory, "settings.db");

    public LocalDatabase()
        : this(DefaultDatabasePath)
    {
    }

    /// <summary>Percorso completo del file del database.</summary>
    public string DatabasePath { get; }

    /// <summary>Cartella dei backup automatici, accanto al database.</summary>
    public string BackupDirectory => Path.Combine(Path.GetDirectoryName(DatabasePath)!, "backups");

    internal LocalDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath))
            ?? throw new ArgumentException("Percorso del database non valido.", nameof(databasePath));
        Directory.CreateDirectory(directory);
        DatabasePath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString();
        Initialize();
    }

    public string? GetSetting(string key)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    /// <summary>
    /// Legge più preferenze con una sola connessione e una sola query. All'avvio
    /// servono diverse impostazioni: aprirne una per ciascuna era uno spreco.
    /// Le chiavi assenti sono presenti nel risultato con valore vuoto.
    /// </summary>
    public Dictionary<string, string> GetSettings(params string[] keys)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (keys.Length == 0)
        {
            return result;
        }
        foreach (var key in keys)
        {
            result[key] = string.Empty;
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var placeholders = new string[keys.Length];
        for (var index = 0; index < keys.Length; index++)
        {
            placeholders[index] = $"$key{index}";
            command.Parameters.AddWithValue($"$key{index}", keys[index]);
        }
        command.CommandText =
            $"SELECT Key, Value FROM Settings WHERE Key IN ({string.Join(", ", placeholders)});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result[reader.GetString(0)] = reader.GetString(1);
        }
        return result;
    }

    /// <summary>Salva più preferenze in un'unica transazione.</summary>
    public void SaveSettings(IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0)
        {
            return;
        }
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var pair in values)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Settings (Key, Value) VALUES ($key, $value)
                ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
                """;
            command.Parameters.AddWithValue("$key", pair.Key);
            command.Parameters.AddWithValue("$value", pair.Value ?? string.Empty);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void SaveSetting(string key, string value)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Settings (Key, Value) VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    public Dictionary<string, string> GetEmployeeMappings() => ReadMappings("Employees");

    public Dictionary<string, string> GetPolicyMappings() => ReadMappings("Policies");

    /// <summary>Ore giornaliere e periodo del rapporto dei dipendenti che ne hanno di propri.</summary>
    public Dictionary<string, EmployeeProfile> GetEmployeeProfiles()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name, DailyHours, StartDate, EndDate FROM EmployeeProfiles;";
        using var reader = command.ExecuteReader();
        var profiles = new Dictionary<string, EmployeeProfile>(StringComparer.OrdinalIgnoreCase);
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        while (reader.Read())
        {
            decimal? hours = !reader.IsDBNull(1) &&
                             decimal.TryParse(reader.GetString(1), System.Globalization.NumberStyles.Number, culture, out var parsedHours)
                ? parsedHours
                : null;
            DateTime? Date(int column) => !reader.IsDBNull(column) &&
                                          DateTime.TryParseExact(reader.GetString(column), "yyyy-MM-dd", culture,
                                              System.Globalization.DateTimeStyles.None, out var day)
                ? day
                : null;
            var profile = new EmployeeProfile(hours, Date(2), Date(3));
            if (!profile.IsEmpty)
            {
                profiles[reader.GetString(0)] = profile;
            }
        }
        return profiles;
    }

    /// <summary>Salva il profilo del dipendente; un profilo vuoto lo cancella (vale la regola generale).</summary>
    public void SaveEmployeeProfile(string name, EmployeeProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(profile);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.Parameters.AddWithValue("$name", name.Trim());
        if (profile.IsEmpty)
        {
            command.CommandText = "DELETE FROM EmployeeProfiles WHERE Name = $name;";
        }
        else
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            command.CommandText = """
                INSERT INTO EmployeeProfiles (Name, DailyHours, StartDate, EndDate)
                VALUES ($name, $hours, $start, $end)
                ON CONFLICT(Name) DO UPDATE SET
                    DailyHours = excluded.DailyHours,
                    StartDate = excluded.StartDate,
                    EndDate = excluded.EndDate;
                """;
            command.Parameters.AddWithValue("$hours", profile.DailyHours is { } hours ? hours.ToString(culture) : DBNull.Value);
            command.Parameters.AddWithValue("$start", profile.StartDate is { } start ? start.ToString("yyyy-MM-dd", culture) : DBNull.Value);
            command.Parameters.AddWithValue("$end", profile.EndDate is { } end ? end.ToString("yyyy-MM-dd", culture) : DBNull.Value);
        }
        command.ExecuteNonQuery();
    }

    public void SaveEmployeeMapping(string name, string code) => SaveMapping("Employees", name, code);

    /// <summary>Toglie il dipendente dagli abbinamenti, con le sue ore e il suo periodo (se c'erano). True se c'era.</summary>
    public bool DeleteEmployeeMapping(string name)
    {
        name = (name ?? string.Empty).Trim();
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM EmployeeProfiles WHERE Name = $name;
            DELETE FROM Employees WHERE Name = $name;
            """;
        command.Parameters.AddWithValue("$name", name);
        var removed = command.ExecuteNonQuery() > 0;
        transaction.Commit();
        return removed;
    }

    /// <summary>Toglie il giustificativo dagli abbinamenti. True se c'era.</summary>
    public bool DeletePolicyMapping(string policy)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Policies WHERE Name = $name;";
        command.Parameters.AddWithValue("$name", (policy ?? string.Empty).Trim());
        return command.ExecuteNonQuery() > 0;
    }

    public void SavePolicyMapping(string policy, string code) => SaveMapping("Policies", policy, code);

    private void Initialize()
    {
        using var connection = OpenConnection();
        using (var pragmas = connection.CreateCommand())
        {
            pragmas.CommandText = "PRAGMA journal_mode = WAL;";
            pragmas.ExecuteScalar();
        }

        using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "PRAGMA user_version;";
        var schemaVersion = Convert.ToInt32(versionCommand.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        if (schemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Il database Vyrn Presence usa una versione non supportata ({schemaVersion}).");
        }

        if (schemaVersion > 0 && schemaVersion < CurrentSchemaVersion)
        {
            // Prima di cambiare lo schema di un database con dati si tiene una copia.
            TryCreateBackup("prima-di-aggiornare");
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY NOT NULL,
                Value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Employees (
                Name TEXT PRIMARY KEY NOT NULL COLLATE NOCASE,
                Code TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Policies (
                Name TEXT PRIMARY KEY NOT NULL COLLATE NOCASE,
                Code TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS EmployeeProfiles (
                Name TEXT PRIMARY KEY NOT NULL COLLATE NOCASE,
                DailyHours TEXT,
                StartDate TEXT,
                EndDate TEXT
            );
            CREATE TABLE IF NOT EXISTS Companies (
                Code TEXT PRIMARY KEY NOT NULL COLLATE NOCASE,
                Name TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                UpdatedUtc TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS Exports (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ExportedAtUtc TEXT NOT NULL,
                FileName TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                CompanyCode TEXT NOT NULL,
                Employees INTEGER NOT NULL,
                Movements INTEGER NOT NULL,
                TotalMinutes INTEGER NOT NULL,
                Fingerprint TEXT NOT NULL DEFAULT '',
                SourceName TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS Exports_ExportedAtUtc ON Exports (ExportedAtUtc DESC);
            CREATE TABLE IF NOT EXISTS ExportFiles (
                ExportId INTEGER PRIMARY KEY NOT NULL REFERENCES Exports(Id) ON DELETE CASCADE,
                XmlGz BLOB NOT NULL,
                RawBytes INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Activity (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AtUtc TEXT NOT NULL,
                Kind TEXT NOT NULL,
                Title TEXT NOT NULL,
                Detail TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS Activity_AtUtc ON Activity (AtUtc DESC);
            """;
        command.ExecuteNonQuery();

        // Database creato da una versione precedente: le colonne nuove si aggiungono
        // (mai si ricrea la tabella: lo storico esistente resta intatto).
        EnsureColumn(connection, "Companies", "RulesJson", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "Exports", "Fingerprint", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, "Exports", "SourceName", "TEXT NOT NULL DEFAULT ''");
        using (var fingerprintIndex = connection.CreateCommand())
        {
            fingerprintIndex.CommandText =
                "CREATE INDEX IF NOT EXISTS Exports_Fingerprint ON Exports (Fingerprint);";
            fingerprintIndex.ExecuteNonQuery();
        }

        // L'azienda già usata nelle versioni precedenti (solo il codice, salvato fra le
        // preferenze) entra nell'anagrafica: nessuno deve reinserirla.
        using (var seedCompany = connection.CreateCommand())
        {
            seedCompany.CommandText = """
                INSERT OR IGNORE INTO Companies (Code, Name, Notes, UpdatedUtc)
                SELECT Value, '', '', $now FROM Settings
                WHERE Key = 'company-code' AND length(trim(Value)) BETWEEN 1 AND 6;
                """;
            seedCompany.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            seedCompany.ExecuteNonQuery();
        }

        using var updateVersion = connection.CreateCommand();
        updateVersion.CommandText = $"PRAGMA user_version = {CurrentSchemaVersion};";
        updateVersion.ExecuteNonQuery();

        // Il controllo d'integrità è importante ma non serve a ogni avvio:
        // viene eseguito la prima volta e poi al massimo una volta a settimana,
        // così l'apertura dell'app resta rapida.
        if (ShouldRunIntegrityCheck(connection))
        {
            using var integrityCheck = connection.CreateCommand();
            integrityCheck.CommandText = "PRAGMA quick_check;";
            var integrityResult = Convert.ToString(
                integrityCheck.ExecuteScalar(),
                System.Globalization.CultureInfo.InvariantCulture);
            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                // Resta un InvalidDataException (come prima), ma marcata: il ripristino
                // distingue un file danneggiato da uno scritto da una build più nuova.
                var integrityFailure = new InvalidDataException(
                    $"Il database Vyrn Presence non ha superato il controllo di integrità: {integrityResult}");
                integrityFailure.Data[IntegrityMarker] = true;
                throw integrityFailure;
            }
            RecordIntegrityCheck(connection);
        }
    }

    private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
    {
        using var info = connection.CreateCommand();
        info.CommandText = $"PRAGMA table_info({table});";
        using (var reader = info.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }
        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
    }

    private const string IntegrityCheckSetting = "integrity-check-at";
    private const string IntegrityMarker = "presenzeflow.integrity";

    private static bool ShouldRunIntegrityCheck(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", IntegrityCheckSetting);
        var raw = command.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }
        return !DateTime.TryParse(
                   raw,
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.RoundtripKind,
                   out var last) ||
               DateTime.UtcNow - last > TimeSpan.FromDays(7);
    }

    private static void RecordIntegrityCheck(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Settings (Key, Value) VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("$key", IntegrityCheckSetting);
        command.Parameters.AddWithValue("$value", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA busy_timeout = 5000;
            PRAGMA foreign_keys = ON;
            PRAGMA secure_delete = ON;
            PRAGMA synchronous = FULL;
            """;
        command.ExecuteNonQuery();
        return connection;
    }

    /* ------------------- Apertura, backup e ripristino ------------------- */

    /// <summary>
    /// Apre il database e non si arrende al primo problema, così l'app parte
    /// sempre: un file danneggiato viene messo da parte (cartella "corrupt") e
    /// sostituito dall'ultimo backup valido; se non c'è nemmeno quello se ne crea
    /// uno nuovo; se la cartella dei dati non è utilizzabile si ripiega su un
    /// database temporaneo. La nota restituita racconta che cosa è successo.
    /// </summary>
    public static LocalDatabaseOpenResult OpenWithRecovery(string? databasePath = null)
    {
        var path = databasePath ?? DefaultDatabasePath;
        try
        {
            return new LocalDatabaseOpenResult(new LocalDatabase(path), null);
        }
        catch (Exception first) when (IsOpenFailure(first))
        {
            var failure = first;
            if (IsCorruption(first))
            {
                try
                {
                    Quarantine(path);
                    var restored = RestoreLatestBackup(path);
                    var database = new LocalDatabase(path);
                    var note = restored is null
                        ? "Il database locale era danneggiato e non c'erano backup validi: ne è stato creato uno nuovo. " +
                          "Il vecchio file è conservato nella cartella \"corrupt\" dei dati."
                        : $"Il database locale era danneggiato: è stato ripristinato dall'ultimo backup valido ({restored}). " +
                          "Il vecchio file è conservato nella cartella \"corrupt\" dei dati.";
                    return new LocalDatabaseOpenResult(database, note);
                }
                catch (Exception second) when (IsOpenFailure(second))
                {
                    failure = second;
                }
            }

            var fallbackPath = Path.Combine(Path.GetTempPath(), "PresenzeFlow-dati-temporanei", "settings.db");
            var fallback = new LocalDatabase(fallbackPath);
            return new LocalDatabaseOpenResult(
                fallback,
                $"Impossibile aprire il database locale ({failure.Message}). Per ora si lavora con un database " +
                $"temporaneo: abbinamenti e storico di questa sessione potrebbero andare persi. Cartella dei dati: {DataDirectory}");
        }
    }

    private static bool IsOpenFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SqliteException or InvalidDataException;

    /// <summary>
    /// File danneggiato (SQLITE_CORRUPT = 11, SQLITE_NOTADB = 26) o con contenuto
    /// che non supera il controllo d'integrità. Un database "più nuovo" di questa
    /// build NON è danneggiato e non va mai sovrascritto.
    /// </summary>
    private static bool IsCorruption(Exception exception) =>
        exception.Data.Contains(IntegrityMarker) || exception is SqliteException { SqliteErrorCode: 11 or 26 };

    /// <summary>Sposta il database danneggiato (e i file WAL/SHM) in "corrupt", senza cancellarlo.</summary>
    private static void Quarantine(string databasePath)
    {
        var folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!, "corrupt");
        Directory.CreateDirectory(folder);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var source = databasePath + suffix;
            if (File.Exists(source))
            {
                File.Move(source, Path.Combine(folder, $"settings-{stamp}.db{suffix}"), overwrite: true);
            }
        }
    }

    /// <summary>Ripristina il backup valido più recente; restituisce il suo nome (null se non ce n'è).</summary>
    private static string? RestoreLatestBackup(string databasePath)
    {
        var folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!, "backups");
        if (!Directory.Exists(folder))
        {
            return null;
        }
        foreach (var file in new DirectoryInfo(folder).GetFiles("*.db").OrderByDescending(file => file.LastWriteTimeUtc))
        {
            if (!IsHealthyDatabaseFile(file.FullName))
            {
                continue;
            }
            File.Copy(file.FullName, databasePath, overwrite: true);
            return file.Name;
        }
        return null;
    }

    /// <summary>Il file si apre, supera il controllo d'integrità e ha uno schema che questa build conosce.</summary>
    private static bool IsHealthyDatabaseFile(string path)
    {
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            var result = Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            command.CommandText = "PRAGMA user_version;";
            var version = Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            return version <= CurrentSchemaVersion;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    /// <summary>
    /// Copia consistente del database (anche con l'app in uso) nella cartella dei
    /// backup. Il file nasce con un nome provvisorio e prende quello definitivo
    /// solo a copia finita: un backup a metà non esiste mai.
    /// </summary>
    public string CreateBackup(string label = "")
    {
        Directory.CreateDirectory(BackupDirectory);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var name = label.Length == 0 ? $"settings-{stamp}.db" : $"settings-{label}-{stamp}.db";
        var finalPath = Path.Combine(BackupDirectory, name);
        var temporaryPath = finalPath + ".tmp";
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }

        using (var connection = OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "VACUUM INTO $path;";
            command.Parameters.AddWithValue("$path", temporaryPath);
            command.ExecuteNonQuery();
        }
        File.Move(temporaryPath, finalPath);
        PruneBackups();
        return finalPath;
    }

    /// <summary>Come <see cref="CreateBackup"/>, ma un errore non deve mai fermare chi lo chiama.</summary>
    private string? TryCreateBackup(string label)
    {
        try
        {
            return CreateBackup(label);
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Crea un backup solo se l'ultimo è più vecchio di <paramref name="minimumAge"/>.</summary>
    public string? BackupIfDue(TimeSpan minimumAge)
    {
        var newest = ListBackups().FirstOrDefault();
        if (newest is not null && DateTime.UtcNow - newest.CreatedUtc < minimumAge)
        {
            return null;
        }
        return CreateBackup();
    }

    /// <summary>I backup presenti, dal più recente al più vecchio.</summary>
    public IReadOnlyList<DatabaseBackup> ListBackups()
    {
        if (!Directory.Exists(BackupDirectory))
        {
            return Array.Empty<DatabaseBackup>();
        }
        return new DirectoryInfo(BackupDirectory)
            .GetFiles("*.db")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new DatabaseBackup(file.FullName, file.LastWriteTimeUtc, file.Length))
            .ToArray();
    }

    private void PruneBackups()
    {
        foreach (var old in ListBackups().Skip(MaximumBackups))
        {
            try
            {
                File.Delete(old.Path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Un backup vecchio che non si lascia cancellare non è un problema.
            }
        }
    }

    /// <summary>
    /// Riporta nel file principale tutto ciò che sta nel registro WAL: dopo la
    /// chiamata copiare o spostare settings.db non lascia indietro dati recenti.
    /// </summary>
    public void Checkpoint()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        command.ExecuteNonQuery();
    }

    /// <summary>Manutenzione alla chiusura: backup se serve e checkpoint. Non lancia mai eccezioni.</summary>
    public void MaintainOnExit()
    {
        try
        {
            BackupIfDue(TimeSpan.FromHours(6));
            Checkpoint();
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            // La chiusura del programma non deve dipendere da un backup riuscito.
        }
    }

    /// <summary>Stato attuale del database, per mostrarlo all'utente.</summary>
    public DatabaseHealth GetHealth()
    {
        int schema;
        using (var connection = OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA user_version;";
            schema = Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }
        var backups = ListBackups();
        var size = File.Exists(DatabasePath) ? new FileInfo(DatabasePath).Length : 0;
        var isTemporary = DatabasePath.StartsWith(
            Path.GetFullPath(Path.GetTempPath()),
            StringComparison.OrdinalIgnoreCase);
        return new DatabaseHealth(
            DatabasePath,
            size,
            schema,
            backups.Count,
            backups.Count == 0 ? null : backups[0].CreatedUtc,
            isTemporary);
    }

    /* --------------------- Storico delle esportazioni -------------------- */

    /// <summary>
    /// Registra un'esportazione e fa scorrere le più vecchie fuori dalla
    /// finestra conservata: lo storico serve a ricordare le ultime consegne, non
    /// a crescere per sempre. Con <paramref name="xml"/> conserva anche una copia
    /// compressa del file consegnato. Restituisce l'identificativo della voce.
    /// </summary>
    public long SaveExport(ExportRecord record, byte[]? xml = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        long id;
        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO Exports
                    (ExportedAtUtc, FileName, FilePath, CompanyCode, Employees, Movements, TotalMinutes,
                     Fingerprint, SourceName)
                VALUES ($at, $name, $path, $company, $employees, $movements, $minutes, $fingerprint, $source);
                SELECT last_insert_rowid();
                """;
            insert.Parameters.AddWithValue("$at", record.ExportedAtUtc.ToUniversalTime().ToString("O"));
            insert.Parameters.AddWithValue("$name", record.FileName ?? string.Empty);
            insert.Parameters.AddWithValue("$path", record.FilePath ?? string.Empty);
            insert.Parameters.AddWithValue("$company", record.CompanyCode ?? string.Empty);
            insert.Parameters.AddWithValue("$employees", record.Employees);
            insert.Parameters.AddWithValue("$movements", record.Movements);
            insert.Parameters.AddWithValue("$minutes", record.TotalMinutes);
            insert.Parameters.AddWithValue("$fingerprint", record.Fingerprint ?? string.Empty);
            insert.Parameters.AddWithValue("$source", record.SourceName ?? string.Empty);
            id = Convert.ToInt64(insert.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        if (xml is { Length: > 0 and <= MaximumStoredXmlBytes })
        {
            using var store = connection.CreateCommand();
            store.Transaction = transaction;
            store.CommandText = "INSERT INTO ExportFiles (ExportId, XmlGz, RawBytes) VALUES ($id, $gz, $raw);";
            store.Parameters.AddWithValue("$id", id);
            store.Parameters.AddWithValue("$gz", Compress(xml));
            store.Parameters.AddWithValue("$raw", xml.Length);
            store.ExecuteNonQuery();
        }

        using (var prune = connection.CreateCommand())
        {
            prune.Transaction = transaction;
            // Le copie dell'XML escono insieme alla voce (ON DELETE CASCADE).
            prune.CommandText = """
                DELETE FROM Exports
                WHERE Id NOT IN (
                    SELECT Id FROM Exports ORDER BY ExportedAtUtc DESC, Id DESC LIMIT $keep
                );
                """;
            prune.Parameters.AddWithValue("$keep", ExportHistory.MaximumEntries);
            prune.ExecuteNonQuery();
        }

        transaction.Commit();
        return id;
    }

    /// <summary>Oltre questa dimensione l'XML non viene copiato nel database (resta solo la voce dello storico).</summary>
    public const int MaximumStoredXmlBytes = 20 * 1024 * 1024;

    private const string ExportColumns = """
        e.Id, e.ExportedAtUtc, e.FileName, e.FilePath, e.CompanyCode, e.Employees, e.Movements,
        e.TotalMinutes, e.Fingerprint, e.SourceName, COALESCE(f.RawBytes, 0)
        """;

    private static ExportRecord ReadExport(SqliteDataReader reader) => new(
        ParseTimestamp(reader.GetString(1)),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetInt32(5),
        reader.GetInt32(6),
        reader.GetInt32(7),
        reader.GetInt64(0),
        reader.GetString(8),
        reader.GetString(9),
        reader.GetInt32(10));

    /// <summary>Le esportazioni più recenti, dalla più nuova alla più vecchia.</summary>
    public IReadOnlyList<ExportRecord> GetExports(int limit = ExportHistory.MaximumEntries)
    {
        var take = Math.Clamp(limit, 1, ExportHistory.MaximumEntries);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {ExportColumns}
            FROM Exports e LEFT JOIN ExportFiles f ON f.ExportId = e.Id
            ORDER BY e.ExportedAtUtc DESC, e.Id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", take);
        using var reader = command.ExecuteReader();
        var records = new List<ExportRecord>();
        while (reader.Read())
        {
            records.Add(ReadExport(reader));
        }
        return records;
    }

    /// <summary>
    /// L'ultima consegna con gli stessi movimenti (stessa impronta), se c'è: serve
    /// ad avvisare prima di mandare due volte lo stesso file a PAGHE.
    /// </summary>
    public ExportRecord? FindExportByFingerprint(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return null;
        }
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {ExportColumns}
            FROM Exports e LEFT JOIN ExportFiles f ON f.ExportId = e.Id
            WHERE e.Fingerprint = $fingerprint
            ORDER BY e.ExportedAtUtc DESC, e.Id DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$fingerprint", fingerprint);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadExport(reader) : null;
    }

    /// <summary>Il file XML conservato per una consegna (null se non c'è o è illeggibile).</summary>
    public byte[]? GetExportXml(long exportId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT XmlGz FROM ExportFiles WHERE ExportId = $id;";
        command.Parameters.AddWithValue("$id", exportId);
        if (command.ExecuteScalar() is not byte[] compressed)
        {
            return null;
        }
        try
        {
            return Decompress(compressed);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Optimal))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    public int GetExportCount()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Exports;";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Aggiunge una voce al registro delle attività e tiene solo le ultime <see cref="ActivityLog.MaximumEntries"/>.</summary>
    public void AddActivity(ActivityKind kind, string title, string detail = "", DateTime? atUtc = null)
    {
        var cleanTitle = ActivityLog.CleanTitle(title);
        if (cleanTitle.Length == 0)
        {
            return;
        }
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Activity (AtUtc, Kind, Title, Detail) VALUES ($at, $kind, $title, $detail);";
            insert.Parameters.AddWithValue("$at", (atUtc ?? DateTime.UtcNow).ToUniversalTime().ToString("O"));
            insert.Parameters.AddWithValue("$kind", kind.ToString());
            insert.Parameters.AddWithValue("$title", cleanTitle);
            insert.Parameters.AddWithValue("$detail", ActivityLog.CleanDetail(detail));
            insert.ExecuteNonQuery();
        }
        using (var trim = connection.CreateCommand())
        {
            trim.Transaction = transaction;
            trim.CommandText = """
                DELETE FROM Activity
                WHERE Id <= (SELECT MAX(Id) FROM Activity) - $keep;
                """;
            trim.Parameters.AddWithValue("$keep", ActivityLog.MaximumEntries);
            trim.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    /// <summary>Le attività registrate, dalla più recente alla più vecchia.</summary>
    public IReadOnlyList<ActivityEntry> GetActivities(int limit = ActivityLog.MaximumEntries)
    {
        var take = Math.Clamp(limit, 1, ActivityLog.MaximumEntries);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, AtUtc, Kind, Title, Detail
            FROM Activity
            ORDER BY AtUtc DESC, Id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", take);
        var entries = new List<ActivityEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new ActivityEntry(
                reader.GetInt64(0),
                ParseTimestamp(reader.GetString(1)),
                ActivityLog.ParseKind(reader.GetString(2)),
                reader.GetString(3),
                reader.GetString(4)));
        }
        return entries;
    }

    public void ClearActivities()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Activity;";
        command.ExecuteNonQuery();
    }

    /// <summary>Svuota lo storico (i file XML su disco non vengono toccati).</summary>
    public void ClearExports()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Exports;";
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Un istante illeggibile non deve far saltare l'apertura dello storico: si
    /// ripiega su DateTime.MinValue e la voce resta visibile ma in fondo.
    /// </summary>
    private static DateTime ParseTimestamp(string raw) =>
        DateTime.TryParse(
            raw,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed.ToUniversalTime()
            : DateTime.MinValue;

    /// <summary>Aziende salvate, in ordine di codice.</summary>
    public IReadOnlyList<CompanyRecord> GetCompanies()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Code, Name, Notes, RulesJson FROM Companies ORDER BY Code COLLATE NOCASE;";
        using var reader = command.ExecuteReader();
        var companies = new List<CompanyRecord>();
        while (reader.Read())
        {
            companies.Add(new CompanyRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                CompanyRules.TryParse(reader.GetString(3))));
        }
        return companies;
    }

    /// <summary>
    /// Salva le regole di conversione proprie dell'azienda (null = torna a quelle generali).
    /// L'azienda viene creata se non c'è ancora.
    /// </summary>
    public void SaveCompanyRules(string code, CompanyRules? rules)
    {
        code = (code ?? string.Empty).Trim();
        if (code.Length is 0 or > 6)
        {
            throw new InvalidDataException("Il codice azienda deve contenere da 1 a 6 caratteri.");
        }
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Companies (Code, Name, Notes, UpdatedUtc, RulesJson) VALUES ($code, '', '', $now, $rules)
            ON CONFLICT(Code) DO UPDATE SET RulesJson = excluded.RulesJson, UpdatedUtc = excluded.UpdatedUtc;
            """;
        command.Parameters.AddWithValue("$code", code);
        command.Parameters.AddWithValue("$rules", rules?.ToJson() ?? string.Empty);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Crea o aggiorna un'azienda. Il codice è quello ufficiale PAGHE (1-6 caratteri);
    /// con <paramref name="keepExistingDetails"/> un'azienda già presente non perde
    /// ragione sociale e note (serve quando il codice arriva solo dalla conversione).
    /// </summary>
    public void SaveCompany(string code, string name, string notes = "", bool keepExistingDetails = false)
    {
        code = (code ?? string.Empty).Trim();
        name = (name ?? string.Empty).Trim();
        notes = (notes ?? string.Empty).Trim();
        if (code.Length is 0 or > 6)
        {
            throw new InvalidDataException("Il codice azienda deve contenere da 1 a 6 caratteri.");
        }
        if (name.Length > 120 || notes.Length > 500)
        {
            throw new InvalidDataException("La ragione sociale non può superare 120 caratteri e le note 500.");
        }
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = keepExistingDetails
            ? """
              INSERT INTO Companies (Code, Name, Notes, UpdatedUtc) VALUES ($code, $name, $notes, $now)
              ON CONFLICT(Code) DO NOTHING;
              """
            : """
              INSERT INTO Companies (Code, Name, Notes, UpdatedUtc) VALUES ($code, $name, $notes, $now)
              ON CONFLICT(Code) DO UPDATE SET Name = excluded.Name, Notes = excluded.Notes, UpdatedUtc = excluded.UpdatedUtc;
              """;
        command.Parameters.AddWithValue("$code", code);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$notes", notes);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public bool DeleteCompany(string code)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Companies WHERE Code = $code;";
        command.Parameters.AddWithValue("$code", (code ?? string.Empty).Trim());
        return command.ExecuteNonQuery() > 0;
    }

    private Dictionary<string, string> ReadMappings(string table)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = table switch
        {
            "Employees" => "SELECT Name, Code FROM Employees ORDER BY Name;",
            "Policies" => "SELECT Name, Code FROM Policies ORDER BY Name;",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        using var reader = command.ExecuteReader();
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            mappings.Add(reader.GetString(0), reader.GetString(1));
        }
        return mappings;
    }

    private void SaveMapping(string table, string name, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        name = name.Trim();
        code = code.Trim();
        var maximumCodeLength = table switch
        {
            "Employees" => 7,
            "Policies" => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        if (name.Length > 200 || code.Length > maximumCodeLength)
        {
            throw new InvalidDataException(
                $"Il nome non può superare 200 caratteri e il codice {maximumCodeLength} caratteri.");
        }
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = table switch
        {
            "Employees" => """
                INSERT INTO Employees (Name, Code) VALUES ($name, $code)
                ON CONFLICT(Name) DO UPDATE SET Code = excluded.Code;
                """,
            "Policies" => """
                INSERT INTO Policies (Name, Code) VALUES ($name, $code)
                ON CONFLICT(Name) DO UPDATE SET Code = excluded.Code;
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$code", code);
        command.ExecuteNonQuery();
    }
}
