using System.Globalization;
using System.Text.Json;

namespace PresenzeXmlConverter;

/// <summary>Un abbinamento nome → codice (dipendente o giustificativo).</summary>
internal sealed record CodePair(string Name, string Code);

/// <summary>Ore giornaliere e periodo del rapporto di un dipendente, come stanno nel file.</summary>
internal sealed record ProfileEntry(string Name, decimal? DailyHours, string? StartDate, string? EndDate);

/// <summary>Un'azienda con la sua ragione sociale, le note e le regole proprie (se ci sono).</summary>
internal sealed record CompanyEntry(string Code, string Name, string Notes, CompanyRules? Rules);

/// <summary>Esito di un'importazione: cosa è stato aggiunto, aggiornato, lasciato com'era o scartato.</summary>
internal sealed record CodesImportReport(
    int Added,
    int Updated,
    int Unchanged,
    IReadOnlyList<string> Skipped)
{
    public int Changed => Added + Updated;

    public string Describe() =>
        Changed == 0 && Skipped.Count == 0
            ? $"Nessuna novità: i {Unchanged:N0} elementi del file erano già presenti."
            : $"{Added:N0} aggiunti · {Updated:N0} aggiornati · {Unchanged:N0} già uguali" +
              (Skipped.Count > 0 ? $" · {Skipped.Count:N0} scartati" : string.Empty);
}

/// <summary>
/// File di trasferimento di codici e aziende: serve a portare il lavoro già fatto su un altro PC (o a tenerne
/// una copia) invece di rifare tutto a mano. Contiene abbinamenti dipendenti e giustificativi, ore e periodo
/// dei dipendenti, aziende con regole proprie. Non contiene lo storico delle esportazioni né le preferenze.
/// Calcolo puro sul database: nessuna finestra, verificabile con un test.
/// </summary>
internal sealed record CodesBackup(
    int Version,
    DateTime ExportedUtc,
    IReadOnlyList<CodePair> Employees,
    IReadOnlyList<CodePair> Policies,
    IReadOnlyList<ProfileEntry> Profiles,
    IReadOnlyList<CompanyEntry> Companies)
{
    public const int CurrentVersion = 1;

    /// <summary>Oltre questa dimensione il file non è un elenco di codici: non si legge nemmeno.</summary>
    public const long MaximumFileBytes = 20L * 1024 * 1024;

    private const string Format = "vyrn-presence-codici";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public int Count => Employees.Count + Policies.Count + Profiles.Count + Companies.Count;

    public string Describe() =>
        $"{Employees.Count:N0} dipendenti · {Policies.Count:N0} giustificativi · {Companies.Count:N0} aziende" +
        (Profiles.Count > 0 ? $" · {Profiles.Count:N0} profili orari" : string.Empty);

    /// <summary>Nome del file proposto: «Vyrn-Presence-codici-2026-10-15.json».</summary>
    public static string SuggestFileName(DateTime now) =>
        $"Vyrn-Presence-codici-{now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.json";

    /// <summary>Raccoglie dal database tutto ciò che si può portare su un altro PC.</summary>
    public static CodesBackup Read(LocalDatabase database, DateTime nowUtc)
    {
        var culture = CultureInfo.InvariantCulture;
        return new CodesBackup(
            CurrentVersion,
            nowUtc,
            database.GetEmployeeMappings().Select(pair => new CodePair(pair.Key, pair.Value)).OrderBy(pair => pair.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            database.GetPolicyMappings().Select(pair => new CodePair(pair.Key, pair.Value)).OrderBy(pair => pair.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            database.GetEmployeeProfiles()
                .Select(pair => new ProfileEntry(
                    pair.Key,
                    pair.Value.DailyHours,
                    pair.Value.StartDate?.ToString("yyyy-MM-dd", culture),
                    pair.Value.EndDate?.ToString("yyyy-MM-dd", culture)))
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            database.GetCompanies().Select(company => new CompanyEntry(company.Code, company.Name, company.Notes, company.Rules)).ToArray());
    }

    public string ToJson()
    {
        var document = new Document
        {
            Format = Format,
            Version = Version,
            ExportedUtc = ExportedUtc.ToString("O", CultureInfo.InvariantCulture),
            Employees = Employees.ToList(),
            Policies = Policies.ToList(),
            Profiles = Profiles.ToList(),
            Companies = Companies.ToList()
        };
        return JsonSerializer.Serialize(document, Options);
    }

    /// <summary>
    /// Legge un file di codici. Restituisce false (con il motivo in <paramref name="error"/>) se non lo è, se è
    /// di una versione più nuova o se è rovinato: l'importazione non cambia nulla.
    /// </summary>
    public static bool TryParse(string? json, out CodesBackup backup, out string error)
    {
        backup = new CodesBackup(CurrentVersion, DateTime.MinValue, [], [], [], []);
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Il file è vuoto.";
            return false;
        }
        Document? document;
        try
        {
            document = JsonSerializer.Deserialize<Document>(json, Options);
        }
        catch (JsonException)
        {
            error = "Il file non è leggibile: non sembra un file di codici di Vyrn Presence.";
            return false;
        }
        if (document is null || !string.Equals(document.Format, Format, StringComparison.Ordinal))
        {
            error = "Il file non è un'esportazione di codici e aziende di Vyrn Presence.";
            return false;
        }
        if (document.Version is < 1 or > CurrentVersion)
        {
            error = $"Il file è di una versione più recente ({document.Version}): aggiorna il programma prima di importarlo.";
            return false;
        }
        var exported = DateTime.TryParse(
            document.ExportedUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed.ToUniversalTime()
            : DateTime.MinValue;
        backup = new CodesBackup(
            document.Version,
            exported,
            (document.Employees ?? []).Where(pair => pair is not null).ToArray(),
            (document.Policies ?? []).Where(pair => pair is not null).ToArray(),
            (document.Profiles ?? []).Where(entry => entry is not null).ToArray(),
            (document.Companies ?? []).Where(entry => entry is not null).ToArray());
        return true;
    }

    /// <summary>
    /// Unisce il file al database: ciò che c'è nel file vale (aggiunge i nuovi, aggiorna quelli con un codice diverso),
    /// ciò che nel file non c'è resta com'è. Un dipendente scritto in un altro ordine («Rossi Mario» / «Mario Rossi»)
    /// è la stessa persona e si aggiorna, non si duplica. Le voci non valide si scartano una per una.
    /// </summary>
    public CodesImportReport ApplyTo(LocalDatabase database)
    {
        var added = 0;
        var updated = 0;
        var unchanged = 0;
        var skipped = new List<string>();

        void Track(Func<bool?> apply, string label)
        {
            try
            {
                switch (apply())
                {
                    case true:
                        added++;
                        break;
                    case false:
                        updated++;
                        break;
                    default:
                        unchanged++;
                        break;
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
            {
                skipped.Add($"{label}: {exception.Message}");
            }
        }

        var employees = database.GetEmployeeMappings();
        foreach (var pair in Employees)
        {
            Track(
                () => ApplyMapping(employees, pair, database.SaveEmployeeMapping),
                $"Dipendente «{pair.Name}»");
        }

        var policies = database.GetPolicyMappings();
        foreach (var pair in Policies)
        {
            Track(
                () => ApplyMapping(policies, pair, database.SavePolicyMapping, matchVariants: false),
                $"Giustificativo «{pair.Name}»");
        }

        var profiles = database.GetEmployeeProfiles();
        foreach (var entry in Profiles)
        {
            Track(() => ApplyProfile(database, profiles, entry), $"Profilo «{entry.Name}»");
        }

        var companies = database.GetCompanies().ToDictionary(company => company.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Companies)
        {
            Track(() => ApplyCompany(database, companies, entry), $"Azienda «{entry.Code}»");
        }
        return new CodesImportReport(added, updated, unchanged, skipped);
    }

    /// <summary>true = nuovo, false = aggiornato, null = già uguale.</summary>
    private static bool? ApplyMapping(
        Dictionary<string, string> existing,
        CodePair pair,
        Action<string, string> save,
        bool matchVariants = true)
    {
        var name = (pair.Name ?? string.Empty).Trim();
        var code = (pair.Code ?? string.Empty).Trim();
        if (name.Length == 0 || code.Length == 0)
        {
            throw new InvalidDataException("nome o codice mancante");
        }
        var target = name;
        if (!existing.ContainsKey(name) && matchVariants)
        {
            var key = EmployeeNames.Key(name);
            var variant = key.Length == 0
                ? null
                : existing.Keys.FirstOrDefault(known => EmployeeNames.Key(known) == key);
            if (variant is not null)
            {
                target = variant;
            }
        }
        if (existing.TryGetValue(target, out var current))
        {
            if (string.Equals(current, code, StringComparison.Ordinal))
            {
                return null;
            }
            save(target, code);
            existing[target] = code;
            return false;
        }
        save(target, code);
        existing[target] = code;
        return true;
    }

    private static bool? ApplyProfile(LocalDatabase database, Dictionary<string, EmployeeProfile> existing, ProfileEntry entry)
    {
        var name = (entry.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new InvalidDataException("nome mancante");
        }
        var culture = CultureInfo.InvariantCulture;
        DateTime? Date(string? text) => DateTime.TryParseExact(text, "yyyy-MM-dd", culture, DateTimeStyles.None, out var day) ? day : null;
        var profile = new EmployeeProfile(entry.DailyHours, Date(entry.StartDate), Date(entry.EndDate));
        if (profile.IsEmpty)
        {
            return null;
        }
        var target = existing.Keys.FirstOrDefault(known => EmployeeNames.SamePerson(known, name)) ?? name;
        if (existing.TryGetValue(target, out var current))
        {
            if (current == profile)
            {
                return null;
            }
            database.SaveEmployeeProfile(target, profile);
            existing[target] = profile;
            return false;
        }
        database.SaveEmployeeProfile(target, profile);
        existing[target] = profile;
        return true;
    }

    private static bool? ApplyCompany(LocalDatabase database, Dictionary<string, CompanyRecord> existing, CompanyEntry entry)
    {
        var code = (entry.Code ?? string.Empty).Trim();
        var name = (entry.Name ?? string.Empty).Trim();
        var notes = (entry.Notes ?? string.Empty).Trim();
        if (entry.Rules is { } rules &&
            (rules.HoursPerDay < GenerationSettings.MinimumHoursPerDay || rules.HoursPerDay > GenerationSettings.MaximumHoursPerDay))
        {
            throw new InvalidDataException("regole con ore giornaliere fuori misura");
        }
        if (existing.TryGetValue(code, out var current) &&
            current.Name == name && current.Notes == notes && current.Rules == entry.Rules)
        {
            return null;
        }
        database.SaveCompany(code, name, notes);
        if (entry.Rules is not null || current?.Rules is not null)
        {
            database.SaveCompanyRules(code, entry.Rules);
        }
        var isNew = current is null;
        existing[code] = new CompanyRecord(code, name, notes, entry.Rules);
        return isNew;
    }

    private sealed class Document
    {
        public string? Format { get; set; }

        public int Version { get; set; }

        public string? ExportedUtc { get; set; }

        public List<CodePair>? Employees { get; set; }

        public List<CodePair>? Policies { get; set; }

        public List<ProfileEntry>? Profiles { get; set; }

        public List<CompanyEntry>? Companies { get; set; }
    }
}
