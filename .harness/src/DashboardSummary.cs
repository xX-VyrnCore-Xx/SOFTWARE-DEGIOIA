using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>
/// Numeri della dashboard, calcolati in un unico posto dai dati già in memoria
/// (richieste lette dal foglio, movimenti generati, esportazioni registrate).
/// È un record puro: la finestra lo usa solo per riempire le proprie card, così
/// i conti non si sparpagliano nel codice dell'interfaccia e si possono
/// verificare con un test.
/// </summary>
internal sealed record DashboardSummary(
    int Requests,
    int Employees,
    int Movements,
    int Days,
    int TotalMinutes,
    int Exports)
{
    public static DashboardSummary Empty { get; } = new(0, 0, 0, 0, 0, 0);

    public string RequestsText => Requests.ToString("N0", CultureInfo.InvariantCulture);

    public string EmployeesText => Employees.ToString("N0", CultureInfo.InvariantCulture);

    public string MovementsText => Movements.ToString("N0", CultureInfo.InvariantCulture);

    public string DaysText => Days.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>Ore assegnate nell'anteprima corrente ("42 h 30 min").</summary>
    public string TotalText => ExportHistory.DescribeMinutes(TotalMinutes);

    public string ExportsText => Exports.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>
    /// Ore in forma compatta ("42h 30m"): nella card della dashboard il numero
    /// usa il font grande dei valori e la forma estesa ("42 h 30 min") non ci
    /// starebbe su una riga sola.
    /// </summary>
    public string CompactTotalText => $"{TotalMinutes / 60}h {TotalMinutes % 60:D2}m";

    /// <summary>
    /// C'è qualcosa da mostrare nelle card dei movimenti? Le richieste lette dal
    /// foglio possono esserci anche prima di generare l'anteprima.
    /// </summary>
    public bool HasMovements => Movements > 0;

    public static DashboardSummary From(
        IReadOnlyList<AttendanceRequest> requests,
        IReadOnlyList<DailyMovement> movements,
        int exports)
    {
        var totals = MovementChecks.Summarize(movements);
        return new DashboardSummary(
            requests.Count,
            requests.Select(request => request.EmployeeName)
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Count(),
            totals.Movements,
            totals.Days,
            totals.TotalMinutes,
            exports);
    }

    /// <summary>
    /// Stato del file sorgente in una frase: si capisce da qui se manca il file,
    /// se è stato solo letto o se l'anteprima è pronta per l'esportazione.
    /// </summary>
    public static string DescribeSource(string? fileName, bool hasPreview)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "Nessun foglio importato: apri un file Excel per iniziare.";
        }
        return hasPreview
            ? $"File pronto: {fileName}. L'anteprima è pronta, puoi esportare l'XML."
            : $"File letto: {fileName}. Genera l'anteprima per convertire i movimenti.";
    }
}
