using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter;

internal sealed class LocalDatabase
{
    /// <summary>
    /// Versione dello schema: 1 = preferenze e abbinamenti, 2 = storico delle
    /// esportazioni. Un database di versione superiore è stato scritto da una
    /// build più recente e non va aperto a metà.
    /// </summary>
    public const int CurrentSchemaVersion = 2;

    private readonly string _connectionString;

    public LocalDatabase()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeGioia",
            "PresenzeXmlConverter",
            "settings.db"))
    {
    }

    internal LocalDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath))
            ?? throw new ArgumentException("Percorso del database non valido.", nameof(databasePath));
        Directory.CreateDirectory(directory);
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

    public void SaveEmployeeMapping(string name, string code) => SaveMapping("Employees", name, code);

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
                $"Il database PresenzeFlow usa una versione non supportata ({schemaVersion}).");
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
            CREATE TABLE IF NOT EXISTS Exports (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ExportedAtUtc TEXT NOT NULL,
                FileName TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                CompanyCode TEXT NOT NULL,
                Employees INTEGER NOT NULL,
                Movements INTEGER NOT NULL,
                TotalMinutes INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS Exports_ExportedAtUtc ON Exports (ExportedAtUtc DESC);
            """;
        command.ExecuteNonQuery();

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
                throw new InvalidDataException(
                    $"Il database PresenzeFlow non ha superato il controllo di integrità: {integrityResult}");
            }
            RecordIntegrityCheck(connection);
        }
    }

    private const string IntegrityCheckSetting = "integrity-check-at";

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

    /* --------------------- Storico delle esportazioni -------------------- */

    /// <summary>
    /// Registra un'esportazione e fa scorrere le più vecchie fuori dalla
    /// finestra conservata: lo storico serve a ricordare le ultime consegne, non
    /// a crescere per sempre.
    /// </summary>
    public void SaveExport(ExportRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO Exports
                    (ExportedAtUtc, FileName, FilePath, CompanyCode, Employees, Movements, TotalMinutes)
                VALUES ($at, $name, $path, $company, $employees, $movements, $minutes);
                """;
            insert.Parameters.AddWithValue("$at", record.ExportedAtUtc.ToUniversalTime().ToString("O"));
            insert.Parameters.AddWithValue("$name", record.FileName ?? string.Empty);
            insert.Parameters.AddWithValue("$path", record.FilePath ?? string.Empty);
            insert.Parameters.AddWithValue("$company", record.CompanyCode ?? string.Empty);
            insert.Parameters.AddWithValue("$employees", record.Employees);
            insert.Parameters.AddWithValue("$movements", record.Movements);
            insert.Parameters.AddWithValue("$minutes", record.TotalMinutes);
            insert.ExecuteNonQuery();
        }

        using (var prune = connection.CreateCommand())
        {
            prune.Transaction = transaction;
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
    }

    /// <summary>Le esportazioni più recenti, dalla più nuova alla più vecchia.</summary>
    public IReadOnlyList<ExportRecord> GetExports(int limit = ExportHistory.MaximumEntries)
    {
        var take = Math.Clamp(limit, 1, ExportHistory.MaximumEntries);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ExportedAtUtc, FileName, FilePath, CompanyCode, Employees, Movements, TotalMinutes
            FROM Exports
            ORDER BY ExportedAtUtc DESC, Id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", take);
        using var reader = command.ExecuteReader();
        var records = new List<ExportRecord>();
        while (reader.Read())
        {
            records.Add(new ExportRecord(
                ParseTimestamp(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6)));
        }
        return records;
    }

    public int GetExportCount()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Exports;";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
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
