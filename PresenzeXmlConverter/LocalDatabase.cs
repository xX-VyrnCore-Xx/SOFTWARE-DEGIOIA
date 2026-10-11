using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter;

internal sealed class LocalDatabase
{
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
        if (schemaVersion > 1)
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
            """;
        command.ExecuteNonQuery();

        using var updateVersion = connection.CreateCommand();
        updateVersion.CommandText = "PRAGMA user_version = 1;";
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
