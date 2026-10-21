namespace PresenzeXmlConverter;

/// <summary>
/// Le poche domande che la conversione può fare all'utente: l'interfaccia (classica o nuova) le mostra a modo suo,
/// il servizio resta uguale. Ogni risposta «no» o nulla interrompe l'operazione senza scrivere nulla.
/// </summary>
internal interface IConversionPrompts
{
    /// <summary>Le colonne del foglio non si riconoscono: quali sono? (null = annulla)</summary>
    ColumnSelection? AskColumns(IReadOnlyList<string> headers);

    /// <summary>Il codice PAGHE nel foglio differisce da quello salvato: aggiornare quello salvato?</summary>
    bool ConfirmCodeReplace(string employee, string fileCode, string savedCode);

    /// <summary>Mancano codici di dipendenti o giustificativi: l'utente li completa. false = annullato.</summary>
    bool CompleteMappings(IReadOnlyList<string> missingEmployees, IReadOnlyList<string> missingPolicies, LocalDatabase database);

    /// <summary>Gli stessi movimenti sono già stati esportati: salvare di nuovo?</summary>
    bool ConfirmAlreadyDelivered(ExportRecord previous);
}

/// <summary>Esito di «Genera anteprima».</summary>
internal enum PreviewOutcome
{
    Ready,
    Cancelled,
    StillMissingCodes
}

/// <summary>
/// Il percorso Excel → movimenti → XML senza alcuna finestra: le stesse chiamate della finestra classica
/// (lettura del foglio, abbinamenti, generazione, controlli, scrittura, verifica del file, storico), così la
/// nuova interfaccia produce lo stesso XML byte per byte. Le decisioni dell'utente passano da <see cref="IConversionPrompts"/>.
/// </summary>
internal sealed class ConversionService
{
    private readonly LocalDatabase _database;
    private GenerationOptions _globalOptions;
    private IReadOnlyList<string> _sourceProblems = Array.Empty<string>();
    private IReadOnlyList<string> _generationWarnings = Array.Empty<string>();

    public ConversionService(LocalDatabase database)
    {
        _database = database;
        _globalOptions = GenerationSettings.Read(database);
        Options = _globalOptions;
        XmlFormat.Active = XmlFormat.Load(database);
        CompanyCode = database.GetSetting("company-code") ?? string.Empty;
    }

    public LocalDatabase Database => _database;

    public GenerationOptions Options { get; private set; }

    public string CompanyCode { get; set; }

    public string? SourcePath { get; private set; }

    public WorkbookInspection? Inspection { get; private set; }

    public ColumnSelection? Columns { get; private set; }

    public IReadOnlyList<AttendanceRequest> Requests { get; private set; } = Array.Empty<AttendanceRequest>();

    public IReadOnlyList<DailyMovement> Movements { get; private set; } = Array.Empty<DailyMovement>();

    public string? Xml { get; private set; }

    public IReadOnlyList<MovementFinding> Findings { get; private set; } = Array.Empty<MovementFinding>();

    public bool HasWorkbook => Inspection is not null && Requests.Count > 0;

    public bool HasPreview => Movements.Count > 0 && !string.IsNullOrWhiteSpace(Xml);

    /// <summary>Errori, attenzioni e note delle segnalazioni correnti.</summary>
    public (int Errors, int Warnings, int Notes) FindingCounts => MovementChecks.CountBySeverity(Findings);

    public static bool IsExpectedInputError(Exception exception) =>
        exception is InvalidDataException or IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or
            System.Xml.XmlException or FormatException or OverflowException or ArgumentException;

    /// <summary>Importa il foglio. false = annullato dall'utente (le colonne non riconosciute e non indicate).</summary>
    public async Task<bool> LoadAsync(string path, IConversionPrompts prompts, string? sheetName = null)
    {
        var inspection = await Task.Run(() => AttendanceWorkbookReader.Inspect(path, sheetName));
        var columns = inspection.SuggestedColumns;
        if (columns is null)
        {
            columns = prompts.AskColumns(inspection.Headers);
            if (columns is null)
            {
                return false;
            }
        }
        var read = inspection.Read(columns, Options.HoursPerDay);
        Requests = read.Requests;
        _sourceProblems = read.Problems;
        _generationWarnings = Array.Empty<string>();
        Inspection = inspection;
        Columns = columns;
        SourcePath = path;
        ClearPreview();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            _database.SaveSetting("last-source-dir", directory);
        }
        Audit(
            ActivityKind.Import,
            $"Importato «{Path.GetFileName(path)}»",
            $"{Requests.Count:N0} richieste · foglio «{inspection.SheetName}»");
        return true;
    }

    /// <summary>Toglie il foglio e tutto ciò che ne deriva.</summary>
    public void Unload()
    {
        Inspection = null;
        Columns = null;
        SourcePath = null;
        Requests = Array.Empty<AttendanceRequest>();
        _sourceProblems = Array.Empty<string>();
        ClearPreview();
    }

    private void ClearPreview()
    {
        Movements = Array.Empty<DailyMovement>();
        Xml = null;
        _generationWarnings = Array.Empty<string>();
        Findings = Array.Empty<MovementFinding>();
    }

    /// <summary>Le opzioni valide per l'azienda: le sue regole, se ne ha, altrimenti quelle generali.</summary>
    public GenerationOptions OptionsFor(string companyCode)
    {
        var company = _database.GetCompanies().FirstOrDefault(item =>
            string.Equals(item.Code, companyCode.Trim(), StringComparison.OrdinalIgnoreCase));
        var referenceYear = Inspection?.PeriodStart?.Year ?? DateTime.Today.Year;
        return company?.Rules is { } rules
            ? rules.ToOptions(referenceYear) with
            {
                FillPresence = _globalOptions.FillPresence,
                PresenceCode = _globalOptions.PresenceCode,
                PresenceOfficialCode = _globalOptions.PresenceOfficialCode
            }
            : _globalOptions;
    }

    /// <summary>Il codice azienda è accettabile (da 1 a 6 caratteri)?</summary>
    public static bool IsValidCompanyCode(string value) => value.Trim().Length is > 0 and <= 6;

    /// <summary>Genera movimenti e XML. Lancia <see cref="InvalidDataException"/> se non ne esce alcun movimento.</summary>
    public async Task<PreviewOutcome> GenerateAsync(IConversionPrompts prompts)
    {
        if (Inspection is null || Requests.Count == 0)
        {
            throw new InvalidOperationException("Importa prima un file Excel.");
        }
        if (!IsValidCompanyCode(CompanyCode))
        {
            throw new InvalidDataException("Inserisci un codice azienda da 1 a 6 caratteri, come previsto dal tracciato.");
        }
        var companyCode = CompanyCode.Trim();
        _database.SaveSetting("company-code", companyCode);
        var options = OptionsFor(companyCode);
        if (options.HoursPerDay != Options.HoursPerDay && Columns is not null)
        {
            Requests = Inspection.Read(Columns, options.HoursPerDay).Requests;
        }
        Options = options;

        var requiredEmployees = Requests.Select(request => request.EmployeeName)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var requiredPolicies = Requests.Select(request => request.Policy)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var employees = _database.GetEmployeeMappings();
        if (!SynchronizeEmployeeCodes(employees, prompts))
        {
            return PreviewOutcome.Cancelled;
        }
        employees = _database.GetEmployeeMappings();
        var policies = _database.GetPolicyMappings();
        var missingEmployees = requiredEmployees.Where(name => !EmployeeNames.TryFind(employees, name, out _)).ToArray();
        var missingPolicies = requiredPolicies.Where(policy => !policies.ContainsKey(policy)).ToArray();
        if (missingEmployees.Length > 0 || missingPolicies.Length > 0)
        {
            if (!prompts.CompleteMappings(missingEmployees, missingPolicies, _database))
            {
                return PreviewOutcome.Cancelled;
            }
            employees = _database.GetEmployeeMappings();
            policies = _database.GetPolicyMappings();
            var stillMissing = requiredEmployees.Count(name => !EmployeeNames.TryFind(employees, name, out _)) +
                               requiredPolicies.Count(policy => !policies.ContainsKey(policy));
            if (stillMissing > 0)
            {
                return PreviewOutcome.StillMissingCodes;
            }
        }

        var profiles = _database.GetEmployeeProfiles();
        var requests = Requests;
        var inspection = Inspection;
        var (result, xml) = await Task.Run(() =>
        {
            var generation = AttendanceMovementGenerator.Generate(
                requests, employees, policies, inspection.PeriodStart, inspection.PeriodEnd, options, profiles);
            var document = generation.Movements.Count == 0
                ? string.Empty
                : TrriPwXmlWriter.Create(companyCode, generation.Movements);
            return (generation, document);
        });
        if (result.Movements.Count == 0)
        {
            throw new InvalidDataException("Non è stato generato alcun movimento. Controlla date, durata e periodo del foglio.");
        }
        Movements = result.Movements.ToList();
        Xml = xml;
        _generationWarnings = result.Warnings;
        Findings = RunChecks();
        Audit(
            ActivityKind.Preview,
            "Anteprima generata",
            $"{Movements.Count:N0} movimenti · {FindingCounts.Errors:N0} errori · {FindingCounts.Warnings:N0} attenzioni");
        return PreviewOutcome.Ready;
    }

    private const int MaximumFindingsShown = 400;

    private IReadOnlyList<MovementFinding> RunChecks()
    {
        var findings = new List<MovementFinding>();
        findings.AddRange(_sourceProblems.Take(40)
            .Select(problem => new MovementFinding(MovementSeverity.Warning, $"Foglio Excel · {problem}")));
        findings.AddRange(_generationWarnings.Take(40)
            .Select(warning => new MovementFinding(MovementSeverity.Warning, $"Conversione · {warning}")));
        var policies = Safe(() => _database.GetPolicyMappings());
        var employees = Safe(() => _database.GetEmployeeMappings());
        findings.AddRange(MovementChecks.Review(
            Movements,
            policies,
            Requests.Select(request => request.EmployeeName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Inspection?.PeriodStart,
            Inspection?.PeriodEnd,
            Options.HoursPerDay,
            Options.IncludeSaturdays || Options.AutoDetect));
        findings.AddRange(MovementChecks.CompareWithSource(Movements, Requests));
        findings.AddRange(MovementChecks.CheckMonthlyHours(
            Movements,
            Options,
            Safe(() => _database.GetEmployeeProfiles()),
            Inspection?.PeriodStart,
            Inspection?.PeriodEnd));
        _ = employees;
        return findings.OrderBy(finding => finding.Severity).Take(MaximumFindingsShown).ToArray();
    }

    private static T Safe<T>(Func<T> read)
        where T : new()
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (IsExpectedInputError(exception))
        {
            return new T();
        }
    }

    private bool SynchronizeEmployeeCodes(Dictionary<string, string> employeeMappings, IConversionPrompts prompts)
    {
        var supplied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var request in Requests.Where(request => !string.IsNullOrWhiteSpace(request.EmployeeCode)))
        {
            var code = request.EmployeeCode!.Trim();
            if (code.Length > 7)
            {
                throw new InvalidDataException(
                    $"Il codice PAGHE nel file Excel per “{request.EmployeeName}” supera il limite di 7 caratteri.");
            }
            if (supplied.TryGetValue(request.EmployeeName, out var previous) &&
                !string.Equals(previous, code, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Nel file Excel sono presenti codici PAGHE diversi per il dipendente “{request.EmployeeName}”.");
            }
            supplied[request.EmployeeName] = code;
        }
        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in supplied)
        {
            if (employeeMappings.TryGetValue(item.Key, out var existing) &&
                !string.Equals(existing, item.Value, StringComparison.OrdinalIgnoreCase) &&
                !prompts.ConfirmCodeReplace(item.Key, item.Value, existing))
            {
                return false;
            }
            updates[item.Key] = item.Value;
        }
        var all = new Dictionary<string, string>(employeeMappings, StringComparer.OrdinalIgnoreCase);
        foreach (var update in updates)
        {
            all[update.Key] = update.Value;
        }
        var duplicate = all
            .Where(mapping => Requests.Any(request => string.Equals(request.EmployeeName, mapping.Key, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(mapping => mapping.Value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"Il codice PAGHE “{duplicate.Key}” risulta assegnato a più dipendenti del file.");
        }
        foreach (var update in updates)
        {
            _database.SaveEmployeeMapping(update.Key, update.Value);
        }
        return true;
    }

    /// <summary>La consegna precedente con gli stessi movimenti, se c'è (null = mai esportati).</summary>
    public ExportRecord? FindPreviousDelivery()
    {
        if (Movements.Count == 0)
        {
            return null;
        }
        try
        {
            return _database.FindExportByFingerprint(ExportHistory.Fingerprint(CompanyCode, Movements));
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            return null;
        }
    }

    /// <summary>Nome suggerito per il file XML.</summary>
    public string SuggestedFileName => ExportHistory.SuggestFileName(CompanyCode, SourcePath, Movements.ToList());

    /// <summary>
    /// Scrive l'XML, lo rilegge e lo verifica contro il tracciato e contro i movimenti; solo allora lo registra nello
    /// storico. Lancia <see cref="InvalidDataException"/> se il file non va consegnato.
    /// </summary>
    public void Export(string path)
    {
        if (string.IsNullOrWhiteSpace(Xml))
        {
            throw new InvalidOperationException("Genera prima l'anteprima.");
        }
        var companyCode = CompanyCode.Trim();
        var fromMovements = Movements.Count > 0 && companyCode.Length is > 0 and <= 6;
        if (fromMovements)
        {
            TrriPwXmlWriter.SaveFile(path, companyCode, Movements.ToList());
        }
        else
        {
            TrriPwXmlWriter.SaveFile(path, Xml!);
        }
        var problems = TrriPwXmlValidator.ValidateFile(path);
        if (problems.Count > 0)
        {
            throw new InvalidDataException(
                "Il file salvato non rispetta il tracciato TRRIPW e non va consegnato:" + Environment.NewLine +
                string.Join(Environment.NewLine, problems.Take(6).Select(problem => "• " + problem)));
        }
        if (fromMovements)
        {
            var differences = TrriPwXmlVerifier.Compare(
                TrriPwXmlWriter.Normalize(companyCode, Movements.ToList()),
                File.ReadAllBytes(path));
            if (differences.Count > 0)
            {
                throw new InvalidDataException(
                    "Il file salvato non contiene le stesse ore dell'anteprima e non va consegnato:" + Environment.NewLine +
                    string.Join(Environment.NewLine, differences.Take(6).Select(difference => "• " + difference)));
            }
        }
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            _database.SaveSetting("last-export-dir", directory);
        }
        RecordExport(path, companyCode);
        Audit(
            ActivityKind.Export,
            $"XML esportato «{Path.GetFileName(path)}»",
            $"azienda {companyCode} · {Movements.Count:N0} movimenti");
    }

    private void RecordExport(string path, string companyCode)
    {
        try
        {
            var totals = MovementChecks.Summarize(Movements.ToList());
            byte[]? delivered = null;
            try
            {
                delivered = File.ReadAllBytes(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Senza la copia lo storico resta valido: manca solo «Salva copia».
            }
            _database.SaveExport(
                new ExportRecord(
                    DateTime.UtcNow,
                    Path.GetFileName(path),
                    Path.GetFullPath(path),
                    companyCode,
                    totals.Employees,
                    totals.Movements,
                    totals.TotalMinutes,
                    0,
                    ExportHistory.Fingerprint(companyCode, Movements.ToList()),
                    Path.GetFileName(SourcePath ?? string.Empty)),
                delivered);
        }
        catch (Exception exception) when (
            exception is Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            // Lo storico è un di più: l'esportazione è già riuscita.
        }
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
