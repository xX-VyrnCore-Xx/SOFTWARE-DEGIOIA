using System.Text.Json;

namespace PresenzeXmlConverter;

/// <summary>Un movimento della sessione salvata (stesso contenuto di <see cref="DailyMovement"/>).</summary>
internal sealed record SessionMovement(
    string Name,
    string Code,
    string Policy,
    string Reason,
    DateTime Date,
    int Hours,
    int Minutes,
    int Row,
    string? Official);

/// <summary>
/// Il lavoro in corso: file sorgente, anteprima generata (comprese le modifiche fatte a mano) e se è già
/// stata esportata. Si salva da solo nel database locale e si ripristina all'avvio, così chiudere il
/// programma per sbaglio non costringe a rifare importazione, conversione e correzioni.
/// </summary>
internal sealed record SessionSnapshot(
    int Version,
    string? SourcePath,
    long SourceLength,
    long SourceWriteTicks,
    string? SheetName,
    string CompanyCode,
    bool Exported,
    DateTime SavedAt,
    List<SessionMovement> Movements,
    List<string> Warnings);

internal static class SessionState
{
    public const string SettingKey = "session-state";
    public const int CurrentVersion = 1;

    public static string Serialize(SessionSnapshot snapshot) => JsonSerializer.Serialize(snapshot);

    /// <summary>Legge una sessione salvata: testo vuoto, rovinato o di una versione diversa → falso, mai un'eccezione.</summary>
    public static bool TryParse(string? json, out SessionSnapshot snapshot)
    {
        snapshot = null!;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }
        try
        {
            var parsed = JsonSerializer.Deserialize<SessionSnapshot>(json);
            if (parsed is null || parsed.Version != CurrentVersion || parsed.Movements is null || parsed.Warnings is null)
            {
                return false;
            }
            snapshot = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static SessionSnapshot Create(
        string? sourcePath,
        string? sheetName,
        string companyCode,
        bool exported,
        IReadOnlyList<DailyMovement> movements,
        IReadOnlyList<string> warnings)
    {
        var (length, ticks) = sourcePath is not null ? Fingerprint(sourcePath) : (0L, 0L);
        return new SessionSnapshot(
            CurrentVersion,
            sourcePath,
            length,
            ticks,
            sheetName,
            companyCode,
            exported,
            DateTime.Now,
            movements.Select(movement => new SessionMovement(
                movement.EmployeeName,
                movement.EmployeeCode,
                movement.Policy,
                movement.ReasonCode,
                movement.Date,
                movement.Hours,
                movement.Minutes,
                movement.SourceRow,
                movement.OfficialCode)).ToList(),
            warnings.ToList());
    }

    public static List<DailyMovement> ToMovements(SessionSnapshot snapshot) =>
        snapshot.Movements
            .Select(movement => new DailyMovement(
                movement.Name,
                movement.Code,
                movement.Policy,
                movement.Reason,
                movement.Date,
                movement.Hours,
                movement.Minutes,
                movement.Row,
                movement.Official))
            .ToList();

    /// <summary>Dimensione e data di modifica del file: dicono se è lo stesso di quando si è salvato.</summary>
    public static (long Length, long Ticks) Fingerprint(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? (info.Length, info.LastWriteTimeUtc.Ticks) : (0L, 0L);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (0L, 0L);
        }
    }

    public enum SourceStatus
    {
        /// <summary>Non c'era un file sorgente.</summary>
        None,

        /// <summary>Il file è lo stesso.</summary>
        Same,

        /// <summary>Il file è stato modificato dopo il salvataggio.</summary>
        Changed,

        /// <summary>Il file non si trova più (spostato, disco scollegato).</summary>
        Missing
    }

    public static SourceStatus CheckSource(SessionSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.SourcePath))
        {
            return SourceStatus.None;
        }
        if (!File.Exists(snapshot.SourcePath))
        {
            return SourceStatus.Missing;
        }
        var (length, ticks) = Fingerprint(snapshot.SourcePath);
        return length == snapshot.SourceLength && ticks == snapshot.SourceWriteTicks
            ? SourceStatus.Same
            : SourceStatus.Changed;
    }
}
