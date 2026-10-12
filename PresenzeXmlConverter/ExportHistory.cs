using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>
/// Una conversione salvata su disco, come viene conservata nello storico locale.
/// Serve a rispondere alla domanda "quale XML ho consegnato, e quando?": il file
/// può essere stato spostato o sovrascritto, l'esportazione resta registrata.
/// </summary>
internal sealed record ExportRecord(
    DateTime ExportedAtUtc,
    string FileName,
    string FilePath,
    string CompanyCode,
    int Employees,
    int Movements,
    int TotalMinutes)
{
    /// <summary>Momento dell'esportazione nel fuso orario del PC.</summary>
    public DateTime LocalTime => ExportedAtUtc.ToLocalTime();

    /// <summary>Ore e minuti in forma leggibile ("42 h 30 min").</summary>
    public string TotalText => ExportHistory.DescribeMinutes(TotalMinutes);
}

/// <summary>
/// Regole dello storico delle esportazioni e formattazione per la griglia. È
/// logica pura: si può verificare senza aprire la finestra, e le voci di una
/// giornata si leggono come le leggerebbe una persona ("oggi 14:32").
/// </summary>
internal static class ExportHistory
{
    /// <summary>Numero massimo di esportazioni conservate (le più vecchie escono).</summary>
    public const int MaximumEntries = 200;

    /// <summary>Ore e minuti in forma leggibile.</summary>
    public static string DescribeMinutes(int totalMinutes)
    {
        var safe = Math.Max(0, totalMinutes);
        return $"{safe / 60:N0} h {safe % 60:D2} min";
    }

    /// <summary>
    /// Data di un'esportazione come la si legge in un elenco: oggi e ieri hanno
    /// un nome, il resto diventa una data. La differenza si calcola sui giorni
    /// di calendario, non sulle ore: alle 07:00 di ieri "ieri" è ancora giusto
    /// anche se sono passate meno di 24 ore.
    /// </summary>
    public static string DescribeDay(DateTime localTime, DateTime now)
    {
        var days = (now.Date - localTime.Date).Days;
        var time = localTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        return days switch
        {
            0 => $"oggi {time}",
            1 => $"ieri {time}",
            _ => localTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
        };
    }

    /// <summary>Descrive il contenuto di un'esportazione in una riga.</summary>
    public static string DescribeContents(ExportRecord record) =>
        $"{record.Movements:N0} movimenti · {record.Employees:N0} dipendenti · {record.TotalText}";

    /// <summary>
    /// Riepilogo dello storico per il piè di pagina: quante esportazioni e
    /// quando è stata l'ultima.
    /// </summary>
    public static string DescribeSummary(IReadOnlyList<ExportRecord> exports, DateTime now)
    {
        if (exports.Count == 0)
        {
            return "Nessuna esportazione registrata.";
        }
        var newest = exports.Max(record => record.ExportedAtUtc);
        var last = DescribeDay(newest.ToLocalTime(), now);
        return exports.Count == 1
            ? $"1 esportazione registrata · ultima {last}."
            : $"{exports.Count:N0} esportazioni registrate · ultima {last}.";
    }
}
