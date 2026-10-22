using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter;

/// <summary>
/// Trasferimento di codici e aziende fra PC (esporta in un file, importa da un file), senza finestre: la nuova interfaccia
/// ci mette i selettori di file e i messaggi, i test la provano direttamente.
/// </summary>
internal static class CodesTransfer
{
    /// <summary>Scrive codici, ore dei dipendenti e aziende in un file. Restituisce la descrizione di ciò che contiene (null se non c'è nulla da esportare).</summary>
    public static string? Export(LocalDatabase database, string path, DateTime nowUtc)
    {
        var backup = CodesBackup.Read(database, nowUtc);
        if (backup.Count == 0)
        {
            return null;
        }
        File.WriteAllText(path, backup.ToJson(), new System.Text.UTF8Encoding(false));
        Audit(database, "Codici e aziende esportati", $"{backup.Describe()} · {Path.GetFileName(path)}");
        return backup.Describe();
    }

    /// <summary>Legge un file di codici e aziende senza toccare nulla: serve per mostrare che cosa contiene prima di importarlo.</summary>
    public static bool TryRead(string path, out CodesBackup backup, out string problem)
    {
        backup = null!;
        if (new FileInfo(path).Length > CodesBackup.MaximumFileBytes)
        {
            problem = "Il file è troppo grande per essere un elenco di codici.";
            return false;
        }
        if (!CodesBackup.TryParse(File.ReadAllText(path), out backup, out problem))
        {
            return false;
        }
        if (backup.Count == 0)
        {
            problem = "Il file non contiene nessun codice né azienda.";
            return false;
        }
        return true;
    }

    /// <summary>Unisce il file ai dati (aggiunge i nuovi, aggiorna quelli con un codice diverso, lascia il resto) dopo una copia di sicurezza.</summary>
    public static CodesImportReport Import(LocalDatabase database, CodesBackup backup)
    {
        database.CreateBackup("prima-di-importare");
        var report = backup.ApplyTo(database);
        Audit(database, "Codici e aziende importati", report.Describe());
        return report;
    }

    private static void Audit(LocalDatabase database, string title, string detail)
    {
        try
        {
            database.AddActivity(ActivityKind.Backup, title, detail);
        }
        catch (Exception exception) when (
            exception is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Il registro non è l'operazione.
        }
    }
}
