using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>Una riga degli abbinamenti: un dipendente (con ore e rapporto) o un giustificativo, col suo codice PAGHE.</summary>
internal sealed class CodeRow
{
    public CodeRow(string name, bool required)
    {
        Name = name;
        Required = required;
    }

    /// <summary>Il nome com'è nel foglio: è la chiave dell'abbinamento.</summary>
    public string Name { get; }

    /// <summary>Compare nel foglio importato (serve il codice per creare l'anteprima).</summary>
    public bool Required { get; }

    public string DisplayName => Required ? Name + " *" : Name;

    public string Code { get; set; } = string.Empty;

    public string Hours { get; set; } = string.Empty;

    public string Start { get; set; } = string.Empty;

    public string End { get; set; } = string.Empty;
}

/// <summary>Esito del salvataggio degli abbinamenti.</summary>
internal sealed record CodesSaveResult(string? Note, string? Warnings, int Missing);

/// <summary>
/// Gli abbinamenti nome → codice PAGHE e giustificativo → codice, senza finestre: carica le righe, le convalida con le
/// stesse regole della pagina classica (lunghezze del tracciato, ore e date del rapporto, codici ripetuti) e salva
/// anche a metà, perché ciò che è valido non si rifaccia da capo.
/// </summary>
internal sealed class CodesService
{
    public const int MaximumEmployeeCodeLength = 7;
    public const int MaximumPolicyCodeLength = 3;

    private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "dd-MM-yyyy", "dd.MM.yyyy", "yyyy-MM-dd"];

    private readonly LocalDatabase _database;

    public CodesService(LocalDatabase database) => _database = database;

    public List<CodeRow> Employees { get; private set; } = [];

    public List<CodeRow> Policies { get; private set; } = [];

    /// <summary>Legge dal database gli abbinamenti salvati e aggiunge i nomi del foglio importato che ne sono privi.</summary>
    public void Load(IEnumerable<string> requiredEmployees, IEnumerable<string> requiredPolicies)
    {
        var employees = _database.GetEmployeeMappings();
        var policies = _database.GetPolicyMappings();
        var profiles = _database.GetEmployeeProfiles();
        Employees = Build(employees, requiredEmployees.ToHashSet(StringComparer.OrdinalIgnoreCase));
        Policies = Build(policies, requiredPolicies.ToHashSet(StringComparer.OrdinalIgnoreCase));
        foreach (var row in Employees)
        {
            if (profiles.TryGetValue(row.Name, out var profile))
            {
                row.Hours = profile.DailyHours?.ToString("0.##", CultureInfo.CurrentCulture) ?? string.Empty;
                row.Start = profile.StartDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
                row.End = profile.EndDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
    }

    private static List<CodeRow> Build(IReadOnlyDictionary<string, string> mappings, HashSet<string> required) =>
        mappings.Keys.Concat(required)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .Select(name => new CodeRow(name, required.Contains(name))
            {
                Code = mappings.TryGetValue(name, out var code) ? code : string.Empty
            })
            .ToList();

    /// <summary>Quanti codici richiesti dal foglio mancano ancora (un dipendente scritto in due modi conta una volta).</summary>
    public int CountMissing()
    {
        var coded = Employees
            .Where(row => !string.IsNullOrWhiteSpace(row.Code))
            .Select(row => EmployeeNames.Key(row.Name))
            .ToHashSet(StringComparer.Ordinal);
        var employees = Employees.Count(row =>
            row.Required && string.IsNullOrWhiteSpace(row.Code) && !coded.Contains(EmployeeNames.Key(row.Name)));
        return employees + Policies.Count(row => row.Required && string.IsNullOrWhiteSpace(row.Code));
    }

    /// <summary>
    /// Convalida e salva. Con <paramref name="allowIncomplete"/> i codici mancanti non bloccano e le righe scritte male
    /// restano fuori con una nota; senza, il primo problema lancia <see cref="InvalidDataException"/> e non si salva nulla.
    /// </summary>
    public CodesSaveResult Save(bool allowIncomplete)
    {
        var skipped = allowIncomplete ? new List<string>() : null;
        var notes = new List<string>();
        var employeeMappings = ReadMappings(Employees, MaximumEmployeeCodeLength, "dipendente", skipped);
        var policyMappings = ReadMappings(Policies, MaximumPolicyCodeLength, "giustificativo", skipped);

        var conflicts = employeeMappings
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.Value))
            .GroupBy(mapping => mapping.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(mapping => EmployeeNames.Key(mapping.Key)).Distinct(StringComparer.Ordinal).Count() > 1)
            .ToArray();
        if (conflicts.Length > 0)
        {
            if (skipped is null)
            {
                throw new InvalidDataException(
                    $"Il codice dipendente “{conflicts[0].Key}” è assegnato a più persone diverse. Verifica gli abbinamenti.");
            }
            foreach (var group in conflicts)
            {
                notes.Add(
                    $"il codice “{group.Key}” è usato da {string.Join(" e ", group.Select(mapping => $"“{mapping.Key}”").Take(3))}: " +
                    "se sono persone diverse correggilo prima di esportare");
            }
        }
        var profiles = ReadProfiles(skipped);

        foreach (var mapping in employeeMappings)
        {
            _database.SaveEmployeeMapping(mapping.Key, mapping.Value);
        }
        foreach (var mapping in policyMappings)
        {
            _database.SavePolicyMapping(mapping.Key, mapping.Value);
        }
        foreach (var profile in profiles)
        {
            _database.SaveEmployeeProfile(profile.Key, profile.Value);
        }

        var note = skipped is { Count: > 0 }
            ? (skipped.Count == 1 ? "Una riga non è stata salvata: " : $"{skipped.Count} righe non sono state salvate: ") +
              string.Join("; ", skipped.Take(3)) + (skipped.Count > 3 ? "…" : ".")
            : null;
        var warnings = notes.Count > 0
            ? "Salvato tutto. Attenzione: " + string.Join("; ", notes.Take(2)) + (notes.Count > 2 ? "…" : ".")
            : null;
        Audit(ActivityKind.Codes, "Abbinamenti salvati", $"{employeeMappings.Count:N0} dipendenti · {policyMappings.Count:N0} giustificativi");
        return new CodesSaveResult(note, warnings, CountMissing());
    }

    /// <summary>Toglie la riga dal database (e dall'elenco), con ore e rapporto se è un dipendente.</summary>
    public void Delete(CodeRow row, bool isPolicy)
    {
        if (isPolicy)
        {
            _database.DeletePolicyMapping(row.Name);
            Policies.Remove(row);
        }
        else
        {
            _database.DeleteEmployeeMapping(row.Name);
            Employees.Remove(row);
        }
        Audit(
            ActivityKind.Codes,
            $"{(isPolicy ? "Giustificativo" : "Dipendente")} «{row.Name}» eliminato dagli abbinamenti",
            string.Empty);
    }

    private static Dictionary<string, string> ReadMappings(
        IEnumerable<CodeRow> rows,
        int maximumLength,
        string label,
        List<string>? skipped)
    {
        var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = row.Code?.Trim() ?? string.Empty;
            if (code.Length == 0)
            {
                if (row.Required && skipped is null)
                {
                    throw new InvalidDataException($"Inserisci il codice {label} richiesto per “{row.Name}”.");
                }
                continue;
            }
            if (code.Length > maximumLength)
            {
                if (skipped is not null)
                {
                    skipped.Add($"codice {label} “{code}” troppo lungo (max {maximumLength})");
                    continue;
                }
                throw new InvalidDataException(
                    $"Il codice {label} “{code}” supera il limite di {maximumLength} caratteri del tracciato.");
            }
            output[row.Name] = code;
        }
        return output;
    }

    private Dictionary<string, EmployeeProfile> ReadProfiles(List<string>? skipped)
    {
        var output = new Dictionary<string, EmployeeProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Employees)
        {
            decimal? hours = null;
            var hoursText = row.Hours?.Trim() ?? string.Empty;
            if (hoursText.Length > 0)
            {
                if (!GenerationSettings.TryParseHoursPerDay(hoursText, out var parsed))
                {
                    var message = $"Le ore al giorno di “{row.Name}” (“{hoursText}”) devono essere un numero fra " +
                                  $"{GenerationSettings.MinimumHoursPerDay:0} e {GenerationSettings.MaximumHoursPerDay:0}, per esempio 4 oppure 6,5.";
                    if (skipped is null)
                    {
                        throw new InvalidDataException(message);
                    }
                    skipped.Add($"ore di “{row.Name}” (“{hoursText}”) non valide");
                    continue;
                }
                hours = parsed;
            }
            if (!TryReadDate(row.Start, out var start, out var startText) || !TryReadDate(row.End, out var end, out var endText))
            {
                var bad = startText ?? string.Empty;
                if (startText is null)
                {
                    TryReadDate(row.End, out _, out var endBad);
                    bad = endBad ?? string.Empty;
                }
                if (skipped is null)
                {
                    throw new InvalidDataException(
                        $"La data “{bad}” di “{row.Name}” non è valida: scrivi giorno/mese/anno, per esempio 01/03/2026.");
                }
                skipped.Add($"data “{bad}” di “{row.Name}” non valida");
                continue;
            }
            if (start is not null && end is not null && end < start)
            {
                if (skipped is null)
                {
                    throw new InvalidDataException($"Per “{row.Name}” la data di fine rapporto precede quella di inizio.");
                }
                skipped.Add($"rapporto di “{row.Name}”: la fine precede l'inizio");
                continue;
            }
            output[row.Name] = new EmployeeProfile(hours, start, end);
        }
        return output;
    }

    private static bool TryReadDate(string? text, out DateTime? date, out string? invalidText)
    {
        date = null;
        invalidText = null;
        text = text?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return true;
        }
        if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed.Date;
            return true;
        }
        invalidText = text;
        return false;
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
