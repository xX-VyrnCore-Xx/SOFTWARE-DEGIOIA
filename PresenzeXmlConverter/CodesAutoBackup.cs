using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter;

/// <summary>
/// Copia automatica di codici e aziende: ogni volta che cambiano, al primo avvio e alla chiusura del programma, ne
/// resta un file nella cartella dei dati (uno al giorno, gli ultimi sette). Così, se si cambia PC o si reinstalla,
/// c'è sempre un file pronto da importare con «Trasferisci». Non lancia mai eccezioni: una copia che non riesce
/// non deve disturbare l'avvio o la chiusura.
/// </summary>
internal static class CodesAutoBackup
{
    public const int Keep = 7;

    private const string Prefix = "Vyrn-Presence-codici-auto-";

    /// <summary>La cartella delle copie automatiche, accanto al database.</summary>
    public static string FolderFor(LocalDatabase database) =>
        Path.Combine(Path.GetDirectoryName(database.DatabasePath)!, "codici-backup");

    /// <summary>Le copie presenti, dalla più recente alla più vecchia (il giorno è nel nome).</summary>
    public static IReadOnlyList<string> List(string folder) =>
        Directory.Exists(folder)
            ? Directory.GetFiles(folder, Prefix + "*.json").OrderByDescending(Path.GetFileName, StringComparer.Ordinal).ToArray()
            : [];

    /// <summary>
    /// Salva la copia se i dati sono cambiati rispetto all'ultima. Restituisce il file scritto, oppure null se non
    /// c'era nulla da salvare, nulla è cambiato o non è stato possibile.
    /// </summary>
    public static string? Run(LocalDatabase database, DateTime nowLocal, int keep = Keep)
    {
        try
        {
            var current = CodesBackup.Read(database, nowLocal.ToUniversalTime());
            if (current.Count == 0)
            {
                return null;
            }
            var folder = FolderFor(database);
            Directory.CreateDirectory(folder);
            var newest = List(folder).FirstOrDefault();
            if (newest is not null &&
                CodesBackup.TryParse(File.ReadAllText(newest), out var previous, out _) &&
                string.Equals(previous.ContentKey(), current.ContentKey(), StringComparison.Ordinal))
            {
                return null;
            }

            var path = Path.Combine(folder, $"{Prefix}{nowLocal:yyyy-MM-dd}.json");
            // Prima in un file d'appoggio e poi al suo posto: una scrittura interrotta non rovina la copia buona.
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, current.ToJson(), new System.Text.UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
            foreach (var old in List(folder).Skip(Math.Max(1, keep)))
            {
                File.Delete(old);
            }
            return path;
        }
        catch (Exception exception) when (
            exception is SqliteException or IOException or UnauthorizedAccessException or InvalidDataException or
            System.Text.Json.JsonException or ArgumentException)
        {
            return null;
        }
    }
}
