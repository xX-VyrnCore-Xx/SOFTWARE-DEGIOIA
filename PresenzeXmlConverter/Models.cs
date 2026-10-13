namespace PresenzeXmlConverter;

internal sealed record AttendanceRequest(
    string EmployeeName,
    string Policy,
    DateTime StartDate,
    DateTime EndDate,
    decimal Hours,
    int SourceRow,
    string? EmployeeCode = null);

internal sealed record WorkbookData(
    string SheetName,
    IReadOnlyList<string> Headers,
    IReadOnlyList<AttendanceRequest> Requests,
    DateTime? PeriodStart,
    DateTime? PeriodEnd);

internal sealed record ColumnSelection(
    int Employee,
    int Policy,
    int StartDate,
    int EndDate,
    int Duration);

internal sealed record DailyMovement(
    string EmployeeName,
    string EmployeeCode,
    string Policy,
    string ReasonCode,
    DateTime Date,
    int Hours,
    int Minutes,
    int SourceRow = 0);

/// <summary>Un'azienda dell'anagrafica: il codice è quello ufficiale PAGHE (max 6 caratteri).</summary>
internal sealed record CompanyRecord(string Code, string Name, string Notes, CompanyRules? Rules = null)
{
    /// <summary>«AZ001 · Rossi Srl», oppure solo il codice se la ragione sociale manca.</summary>
    public string Label => Name.Length == 0 ? Code : $"{Code} · {Name}";
}

/// <summary>
/// Regole di conversione proprie di un'azienda: giornata standard, sabato, rilevamento
/// automatico e chiusure aziendali. Un'azienda senza regole proprie usa quelle generali delle
/// Impostazioni; con le proprie, scegliere l'azienda cambia da solo il modo di calcolare le ore.
/// </summary>
internal sealed record CompanyRules(decimal HoursPerDay, bool IncludeSaturdays, bool AutoDetect, string Holidays)
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Opzioni di generazione; le chiusure senza anno valgono per l'anno indicato.</summary>
    public GenerationOptions ToOptions(int referenceYear) =>
        new(HoursPerDay, IncludeSaturdays, GenerationSettings.ParseHolidays(Holidays, referenceYear), AutoDetect);

    /// <summary>Regole che riproducono le opzioni date (punto di partenza di quelle proprie).</summary>
    public static CompanyRules From(GenerationOptions options, int referenceYear) => new(
        options.HoursPerDay,
        options.IncludeSaturdays,
        options.AutoDetect,
        GenerationSettings.FormatHolidays(options.ExtraHolidays, referenceYear));

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Legge le regole salvate; un testo vuoto, rovinato o con valori fuori misura vale «nessuna regola».</summary>
    public static CompanyRules? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            var rules = System.Text.Json.JsonSerializer.Deserialize<CompanyRules>(json, JsonOptions);
            if (rules is null ||
                rules.HoursPerDay < GenerationSettings.MinimumHoursPerDay ||
                rules.HoursPerDay > GenerationSettings.MaximumHoursPerDay)
            {
                return null;
            }
            return rules with { Holidays = rules.Holidays ?? string.Empty };
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}

internal sealed record GenerationResult(
    IReadOnlyList<DailyMovement> Movements,
    IReadOnlyList<string> Warnings);

/// <summary>Impostazioni di generazione scelte dall'utente.</summary>
internal sealed record GenerationOptions(
    decimal HoursPerDay = 8m,
    bool IncludeSaturdays = false,
    IReadOnlyList<DateTime>? ExtraHolidays = null,
    bool AutoDetect = true)
{
    public static GenerationOptions Default { get; } = new();
}
