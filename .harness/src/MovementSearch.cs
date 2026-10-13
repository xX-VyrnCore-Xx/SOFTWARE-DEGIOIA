using System.Globalization;
using System.Text;

namespace PresenzeXmlConverter;

/// <summary>
/// Ricerca nei movimenti generati. Più parole in qualsiasi ordine, senza
/// distinzione di maiuscole e senza accenti: "MARIO ferie" trova le stesse
/// righe di "ferie mario", e "giustificat" trova "giustificativo".
/// </summary>
internal static class MovementSearch
{
    /// <summary>Nessun risultato utile oltre questo numero di termini: evita di scorrere query assurde.</summary>
    private const int MaximumTerms = 8;

    /// <summary>
    /// Divide la query in termini normalizzati. Le parole vuote vengono scartate,
    /// così una query con solo spazi equivale a "nessun filtro".
    /// </summary>
    public static IReadOnlyList<string> SplitTerms(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }
        var terms = new List<string>();
        foreach (var raw in query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var term = Normalize(raw);
            if (term.Length == 0 || terms.Contains(term, StringComparer.Ordinal))
            {
                continue;
            }
            terms.Add(term);
            if (terms.Count == MaximumTerms)
            {
                break;
            }
        }
        return terms;
    }

    /// <summary>
    /// Testo confrontabile: minuscolo, senza accenti, con la punteggiatura ridotta
    /// a spazi (così "Mario-Rossi" e "Mario Rossi" coincidono).
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            var folded = char.IsLetterOrDigit(character) ? Fold(character) : ' ';
            if (folded == ' ')
            {
                if (!previousWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
                continue;
            }
            builder.Append(folded);
            previousWasSpace = false;
        }
        return builder.ToString().TrimEnd();
    }

    /// <summary>Ogni termine deve comparire da qualche parte nel testo (AND, non OR).</summary>
    public static bool Matches(string? haystack, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            return true;
        }
        var normalized = Normalize(haystack);
        foreach (var term in terms)
        {
            if (!normalized.Contains(term, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Cerca un movimento su tutti i campi visibili in griglia, comprese le date
    /// in entrambi i formati e le ore scritte come "8h 30m", "8:30" o "8.30".
    /// </summary>
    public static bool MatchesMovement(DailyMovement movement, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            return true;
        }
        return Matches(Describe(movement), terms);
    }

    /// <summary>Testo su cui viene cercato il contenuto di una riga della griglia.</summary>
    public static string Describe(DailyMovement movement) => Describe(
        movement.EmployeeName,
        movement.EmployeeCode,
        movement.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        movement.Policy,
        movement.ReasonCode,
        movement.Hours.ToString(CultureInfo.InvariantCulture),
        movement.Minutes.ToString(CultureInfo.InvariantCulture),
        movement.SourceRow > 0 ? movement.SourceRow.ToString(CultureInfo.InvariantCulture) : string.Empty);

    /// <summary>
    /// Costruisce il testo indicizzato di una riga della griglia: i valori come
    /// sono scritti più le forme derivate che l'utente può digitare (data ISO,
    /// "8:30", "8.30", "8h30", totale in minuti).
    /// </summary>
    public static string Describe(
        string employee,
        string code,
        string dateText,
        string policy,
        string reason,
        string hoursText,
        string minutesText,
        string sourceText)
    {
        var parts = new List<string>(16)
        {
            employee, code, dateText, policy, reason, hoursText, minutesText, sourceText
        };

        if (DateTime.TryParseExact(
                dateText.Trim(),
                ["dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "dd.MM.yyyy"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            // Data in entrambi i formati: la griglia mostra gg/mm/aaaa ma
            // l'utente può digitare (o incollare) anche la forma ISO.
            parts.Add(date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            parts.Add(date.ToString("dd/MM", CultureInfo.InvariantCulture));
            parts.Add(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            parts.Add(date.ToString("MMMM", CultureInfo.InvariantCulture));
            parts.Add(date.ToString("MMMM", CultureInfo.GetCultureInfo("it-IT")));
            parts.Add(date.ToString("yyyyMM", CultureInfo.InvariantCulture));
        }

        if (int.TryParse(hoursText.Trim(), out var hours) &&
            int.TryParse(minutesText.Trim(), out var minutes) &&
            hours is >= 0 and <= 99 &&
            minutes is >= 0 and <= 59)
        {
            parts.Add($"{hours}:{minutes:00}");
            parts.Add($"{hours:00}:{minutes:00}");
            parts.Add($"{hours}.{minutes:00}");
            parts.Add($"{hours}h{minutes:00}");
            parts.Add($"{hours}h {minutes}m");
            parts.Add((hours * 60 + minutes).ToString(CultureInfo.InvariantCulture));
        }

        return string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    /// <summary>Minuscolo senza accenti, mantenendo lettere e cifre.</summary>
    private static char Fold(char character) => character switch
    {
        'à' or 'á' or 'â' or 'ã' or 'ä' or 'å' => 'a',
        'è' or 'é' or 'ê' or 'ë' => 'e',
        'ì' or 'í' or 'î' or 'ï' => 'i',
        'ò' or 'ó' or 'ô' or 'õ' or 'ö' => 'o',
        'ù' or 'ú' or 'û' or 'ü' => 'u',
        'ç' => 'c',
        'ñ' => 'n',
        'ý' or 'ÿ' => 'y',
        'ß' => 's',
        _ => character
    };
}
