namespace PresenzeXmlConverter;

/// <summary>Esito del controllo di un file consegnato: confronto con la copia conservata e con il tracciato.</summary>
internal sealed record ExportVerification(FileIntegrity Result, string Message, IReadOnlyList<string> Problems);

/// <summary>
/// Lo storico delle esportazioni senza finestre: elenco, controllo di un file consegnato (identico alla copia conservata e
/// valido contro il tracciato), copia dell'XML conservato e svuotamento. Stesse operazioni della sezione classica.
/// </summary>
internal sealed class HistoryService
{
    private readonly LocalDatabase _database;

    public HistoryService(LocalDatabase database) => _database = database;

    public IReadOnlyList<ExportRecord> Load() => _database.GetExports();

    /// <summary>Confronta il file sul disco con la copia consegnata e lo ricontrolla contro il tracciato.</summary>
    public ExportVerification Verify(ExportRecord record)
    {
        var stored = record.HasStoredXml ? _database.GetExportXml(record.Id) : null;
        var current = File.Exists(record.FilePath) ? File.ReadAllBytes(record.FilePath) : null;
        var result = ExportIntegrity.Check(stored, current);
        var message = ExportIntegrity.Describe(result, record.FileName, current);
        IReadOnlyList<string> problems = Array.Empty<string>();
        if (current is not null)
        {
            problems = TrriPwXmlValidator.Validate(current);
            message += problems.Count == 0
                ? Environment.NewLine + "Il file rispetta il tracciato TRRIPW."
                : Environment.NewLine + "Il file NON rispetta il tracciato:" + Environment.NewLine +
                  string.Join(Environment.NewLine, problems.Take(4).Select(problem => "• " + problem));
        }
        Audit(
            ActivityKind.Check,
            $"Verifica del file «{record.FileName}»",
            result switch
            {
                FileIntegrity.Identical => "identico a quello consegnato",
                FileIntegrity.Modified => "modificato dopo l'esportazione",
                FileIntegrity.Missing => "file non trovato",
                _ => "copia non conservata"
            });
        return new ExportVerification(result, message, problems);
    }

    /// <summary>Il file XML conservato nel database per quella consegna (null se non c'è).</summary>
    public byte[]? StoredXml(ExportRecord record) => record.HasStoredXml ? _database.GetExportXml(record.Id) : null;

    public void Clear()
    {
        _database.ClearExports();
        Audit(ActivityKind.Export, "Storico delle esportazioni svuotato", "I file XML su disco non sono stati toccati");
    }

    private void Audit(ActivityKind kind, string title, string detail)
    {
        try
        {
            _database.AddActivity(kind, title, detail);
        }
        catch (Exception exception) when (
            exception is Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Il registro non è l'operazione.
        }
    }
}

/// <summary>Il registro delle attività senza finestre: elenco, ricerca per testo e tipo, CSV e svuotamento.</summary>
internal sealed class ActivityService
{
    private readonly LocalDatabase _database;

    public ActivityService(LocalDatabase database) => _database = database;

    public IReadOnlyList<ActivityEntry> Load() => _database.GetActivities();

    public static IReadOnlyList<ActivityEntry> Filter(IEnumerable<ActivityEntry> entries, string? query, ActivityKind? kind) =>
        ActivityLog.Filter(entries, query, kind);

    public static string ToCsv(IEnumerable<ActivityEntry> entries) => ActivityLog.ToCsv(entries);

    public void Clear() => _database.ClearActivities();
}
