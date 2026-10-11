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

internal sealed record GenerationResult(
    IReadOnlyList<DailyMovement> Movements,
    IReadOnlyList<string> Warnings);

/// <summary>Messaggio della conversazione con l'assistente AI locale.</summary>
internal sealed record AiChatMessage(bool IsUser, string Text);

/// <summary>Risposta dell'assistente: testo in linguaggio naturale e modifiche proposte.</summary>
internal sealed record AiChatResponse(string Reply, IReadOnlyList<DocumentEdit> Edits);
