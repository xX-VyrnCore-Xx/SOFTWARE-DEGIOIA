using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>Controlli delle finestre di dialogo senza finestre: la finestra classica e quella nuova usano le stesse regole.</summary>
internal static class DialogLogic
{
    /// <summary>
    /// Le cinque colonne scelte (nome, giustificativo, data iniziale, data finale, durata): tutte indicate e tutte diverse.
    /// Un indice negativo vuol dire «nessuna colonna scelta».
    /// </summary>
    public static bool TryBuildColumns(IReadOnlyList<int> indexes, out ColumnSelection? selection, out string? error)
    {
        selection = null;
        if (indexes.Count != 5 || indexes.Any(index => index < 0) || indexes.Distinct().Count() != indexes.Count)
        {
            error = "Seleziona una colonna diversa per ogni campo richiesto.";
            return false;
        }
        selection = new ColumnSelection(indexes[0], indexes[1], indexes[2], indexes[3], indexes[4]);
        error = null;
        return true;
    }

    /// <summary>L'intestazione di una colonna come si legge negli elenchi: «Colonna 3: Data inizio».</summary>
    public static string DescribeColumn(int index, string header) =>
        index < 0 ? header : $"Colonna {index + 1}: {(string.IsNullOrWhiteSpace(header) ? "(senza intestazione)" : header)}";

    /// <summary>
    /// Le regole di un'azienda: senza «regole proprie» valgono quelle generali (regole null); altrimenti ore, sabato,
    /// rilevamento automatico e chiusure vengono controllati come nelle Impostazioni.
    /// </summary>
    public static bool TryBuildCompanyRules(
        bool ownRules,
        string hoursText,
        bool saturdays,
        bool autoDetect,
        string holidaysText,
        int referenceYear,
        out CompanyRules? rules,
        out string? error)
    {
        rules = null;
        error = null;
        if (!ownRules)
        {
            return true;
        }
        if (!GenerationSettings.TryParseHoursPerDay(hoursText, out var hours))
        {
            error = $"Le ore di una giornata devono essere un numero fra {GenerationSettings.MinimumHoursPerDay:0.##} e " +
                    $"{GenerationSettings.MaximumHoursPerDay:0.##} (es. 8 oppure 7,5).";
            return false;
        }
        var holidays = GenerationSettings.ParseHolidaysDetailed(holidaysText, referenceYear, out var ignored);
        if (ignored.Count > 0)
        {
            error = $"Date non riconosciute: {string.Join(", ", ignored.Take(4))}. Usa gg/mm o gg/mm/aaaa.";
            return false;
        }
        rules = new CompanyRules(hours, saturdays, autoDetect, GenerationSettings.FormatHolidays(holidays, referenceYear));
        return true;
    }

    /// <summary>Il testo delle regole nei campi: ore con la cultura corrente, chiusure una per riga.</summary>
    public static (string Hours, string Holidays) FormatRules(CompanyRules rules) =>
        (rules.HoursPerDay.ToString("0.##", CultureInfo.CurrentCulture),
            rules.Holidays.Replace(", ", Environment.NewLine, StringComparison.Ordinal));
}
