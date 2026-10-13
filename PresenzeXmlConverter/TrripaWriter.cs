using System.Globalization;
using System.Text;

namespace PresenzeXmlConverter;

/// <summary>
/// Tracciato TRRIPA di Paghe Infinity: testo a larghezza fissa, 100 caratteri per riga.
/// A differenza dell'XML, il PDF «Interfaccia rilevazione presenze – tracciati record»
/// lo descrive campo per campo (posizioni comprese) e ne garantisce l'importazione:
/// non dipende da uno schema XSD sconosciuto.
/// Ogni dipendente ha una riga di testata (00) seguita dai suoi movimenti giornalieri (01).
/// </summary>
internal static class TrripaWriter
{
    public const int RecordLength = 100;

    /// <summary>CodGiusvPA: il codice giustificativo Paghe è di 2 caratteri.</summary>
    public const int MaxReasonCodeLength = 2;

    public static string Create(string companyCode, IReadOnlyList<DailyMovement> movements)
    {
        var (_, normalized) = TrriPwXmlWriter.Validate(companyCode, movements);
        var tooLong = normalized.FirstOrDefault(movement => movement.ReasonCode.Length > MaxReasonCodeLength);
        if (tooLong is not null)
        {
            throw new InvalidDataException(
                $"Il giustificativo “{tooLong.ReasonCode}” ha più di {MaxReasonCodeLength} caratteri: " +
                "il tracciato TRRIPA ammette solo il codice Paghe a 2 caratteri (es. 01). Correggilo nella Gestione codici.");
        }
        var builder = new StringBuilder();
        foreach (var employee in normalized
                     .GroupBy(movement => movement.EmployeeCode, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            AppendRecord(builder, Header(employee.Key));
            foreach (var movement in employee
                         .OrderBy(item => item.Date)
                         .ThenBy(item => item.ReasonCode, StringComparer.OrdinalIgnoreCase))
            {
                AppendRecord(builder, Movement(movement));
            }
        }
        return builder.ToString();
    }

    public static void SaveFile(string path, string companyCode, IReadOnlyList<DailyMovement> movements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var text = Create(companyCode, movements);
        File.WriteAllText(path, text, Encoding.Latin1);
    }

    /// <summary>Controlla un file TRRIPA già scritto: lunghezza dei record, testata e posizioni.</summary>
    public static IReadOnlyList<string> ValidateFile(string path)
    {
        var problems = new List<string>();
        var lines = File.ReadAllLines(path, Encoding.Latin1);
        if (lines.Length == 0)
        {
            problems.Add("Il file è vuoto.");
            return problems;
        }
        var headerSeen = false;
        for (var index = 0; index < lines.Length && problems.Count < 20; index++)
        {
            var line = lines[index];
            var number = index + 1;
            if (line.Length != RecordLength)
            {
                problems.Add($"Riga {number}: lunga {line.Length} caratteri invece di {RecordLength}.");
                continue;
            }
            if (line.StartsWith("00", StringComparison.Ordinal))
            {
                headerSeen = true;
                if (line.Substring(2, 21).Trim().Length == 0)
                {
                    problems.Add($"Riga {number}: testata senza codice dipendente.");
                }
                if (line[34] != 'N' && line[34] != 'S')
                {
                    problems.Add($"Riga {number}: «generazione automatica del teorico» deve essere N o S.");
                }
            }
            else if (line.StartsWith("01", StringComparison.Ordinal))
            {
                if (!headerSeen)
                {
                    problems.Add($"Riga {number}: movimento senza la testata del dipendente.");
                }
                if (!DateTime.TryParseExact(line.Substring(9, 8), "yyyyMMdd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _))
                {
                    problems.Add($"Riga {number}: data non valida «{line.Substring(9, 8)}».");
                }
                if (line.Substring(7, 2).Trim().Length == 0)
                {
                    problems.Add($"Riga {number}: codice giustificativo Paghe mancante.");
                }
                if (!int.TryParse(line.AsSpan(17, 4), NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    problems.Add($"Riga {number}: ore o minuti non numerici.");
                }
            }
            else
            {
                problems.Add($"Riga {number}: tipo record «{line[..2]}» non previsto.");
            }
        }
        return problems;
    }

    /// <summary>
    /// RIPA00: il codice dipendente va nel campo «rilevazione presenze» (21 caratteri, a sinistra);
    /// codice soggetto e numero rapporto restano vuoti/zero e Paghe collega il dipendente con la
    /// «Tabella di abbinamento dipendenti». Teorico automatico «N»: si leggono i movimenti.
    /// </summary>
    private static string Header(string employeeCode)
    {
        var record = new char[RecordLength];
        Array.Fill(record, ' ');
        Put(record, 1, "00");
        Put(record, 3, employeeCode.PadRight(21)[..21]);
        Put(record, 32, "000");
        Put(record, 35, "N");
        return new string(record);
    }

    /// <summary>RIPA01: giustificativo, data aaaammgg, ore e minuti.</summary>
    private static string Movement(DailyMovement movement)
    {
        var record = new char[RecordLength];
        Array.Fill(record, ' ');
        Put(record, 1, "01");
        // Codice giustificativo Paghe (2 caratteri): "1" diventa "01", le ore ordinarie sono "01".
        Put(record, 8, movement.ReasonCode.All(char.IsDigit)
            ? movement.ReasonCode.PadLeft(2, '0')
            : movement.ReasonCode.PadRight(2));
        Put(record, 10, movement.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        Put(record, 18, movement.Hours.ToString("00", CultureInfo.InvariantCulture));
        Put(record, 20, movement.Minutes.ToString("00", CultureInfo.InvariantCulture));
        Put(record, 26, "0");
        return new string(record);
    }

    private static void Put(char[] record, int position, string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            record[position - 1 + index] = value[index];
        }
    }

    private static void AppendRecord(StringBuilder builder, string record) =>
        builder.Append(record).Append("\r\n");
}
