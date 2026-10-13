using System.Globalization;
using System.Security.Cryptography;
using System.Text;

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
    int TotalMinutes,
    long Id = 0,
    string Fingerprint = "",
    string SourceName = "",
    int StoredBytes = 0)
{
    /// <summary>
    /// Il file XML consegnato è conservato nel database: si può risalvare una copia
    /// anche se l'originale è stato spostato, rinominato o cancellato.
    /// </summary>
    public bool HasStoredXml => StoredBytes > 0;

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

    /// <summary>
    /// Impronta del CONTENUTO di una consegna: azienda e movimenti (dipendente,
    /// giorno, giustificativo, ore), in ordine canonico. Non dipende dal formato del
    /// file né dall'ordine delle righe, quindi due consegne con gli stessi movimenti
    /// hanno la stessa impronta anche se l'XML è stato scritto in modo diverso.
    /// Serve ad accorgersi di un secondo invio identico a PAGHE.
    /// </summary>
    public static string Fingerprint(string companyCode, IEnumerable<DailyMovement> movements)
    {
        var lines = movements
            .Select(movement => string.Join(
                '|',
                movement.EmployeeCode.Trim().ToUpperInvariant(),
                movement.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                movement.ReasonCode.Trim().ToUpperInvariant(),
                movement.Hours.ToString(CultureInfo.InvariantCulture),
                movement.Minutes.ToString(CultureInfo.InvariantCulture)))
            .Order(StringComparer.Ordinal);
        var text = (companyCode ?? string.Empty).Trim().ToUpperInvariant() + "\n" + string.Join("\n", lines);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    /// <summary>
    /// Nome proposto per il file XML: TRRIPW, azienda e periodo dei movimenti
    /// ("TRRIPW_AZ001_2026-08.xml"). Senza movimenti si ripiega sul nome del file
    /// sorgente. I caratteri non ammessi nei nomi di file vengono tolti.
    /// </summary>
    public static string SuggestFileName(
        string companyCode,
        string? sourceName,
        IReadOnlyCollection<DailyMovement> movements,
        bool trripa = false)
    {
        var prefix = trripa ? "TRRIPA" : "TRRIPW";
        var parts = new List<string>();
        var company = (companyCode ?? string.Empty).Trim().ToUpperInvariant();
        if (movements.Count > 0)
        {
            parts.Add(prefix);
            if (company.Length > 0)
            {
                parts.Add(company);
            }
            var first = movements.Min(movement => movement.Date).Date;
            var last = movements.Max(movement => movement.Date).Date;
            parts.Add(first.Year == last.Year && first.Month == last.Month
                ? first.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                : $"{first:yyyyMMdd}-{last:yyyyMMdd}");
        }
        else
        {
            var source = Path.GetFileNameWithoutExtension(sourceName ?? string.Empty);
            parts.Add(string.IsNullOrWhiteSpace(source) ? "presenze" : source);
            parts.Add(prefix);
        }

        var invalid = Path.GetInvalidFileNameChars();
        var name = string.Concat(string.Join('_', parts).Where(character => !invalid.Contains(character)));
        return (name.Length == 0 ? prefix : name) + (trripa ? ".txt" : ".xml");
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
