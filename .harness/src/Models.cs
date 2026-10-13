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
internal sealed record CompanyRecord(string Code, string Name, string Notes)
{
    /// <summary>«AZ001 · Rossi Srl», oppure solo il codice se la ragione sociale manca.</summary>
    public string Label => Name.Length == 0 ? Code : $"{Code} · {Name}";
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
