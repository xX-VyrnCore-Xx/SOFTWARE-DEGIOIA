using System.Globalization;
using System.Text.Json;

namespace PresenzeXmlConverter;

/// <summary>Tipo di modifica applicabile all'anteprima dei movimenti.</summary>
internal enum DocumentEditKind
{
    SetTime,       // imposta ore e minuti sui movimenti che corrispondono al filtro
    ShiftDays,     // sposta le date di N giorni
    ScaleHours,    // moltiplica la durata per un fattore (es. 0,5 = metà)
    SetReasonCode, // cambia il codice giustificativo dei movimenti selezionati
    Remove         // elimina i movimenti che corrispondono al filtro
}

/// <summary>
/// Filtro con cui selezionare i movimenti di un'anteprima. Un filtro vuoto
/// corrisponde a "tutti i movimenti"; i campi di testo usano un confronto
/// "contiene" per tollerare nomi parziali ("rossi" trova "Mario Rossi").
/// </summary>
internal sealed record MovementFilter(
    string? Employee = null,
    string? Policy = null,
    string? ReasonCode = null,
    DateTime? Date = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null)
{
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Employee) &&
        string.IsNullOrWhiteSpace(Policy) &&
        string.IsNullOrWhiteSpace(ReasonCode) &&
        !Date.HasValue && !DateFrom.HasValue && !DateTo.HasValue;

    public bool Matches(DailyMovement movement)
    {
        if (!string.IsNullOrWhiteSpace(Employee) &&
            !Contains(movement.EmployeeName, Employee!) &&
            !Contains(movement.EmployeeCode, Employee!))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(Policy) && !Contains(movement.Policy, Policy!))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(ReasonCode) && !Contains(movement.ReasonCode, ReasonCode!))
        {
            return false;
        }
        if (Date.HasValue && movement.Date.Date != Date.Value.Date)
        {
            return false;
        }
        if (DateFrom.HasValue && movement.Date.Date < DateFrom.Value.Date)
        {
            return false;
        }
        if (DateTo.HasValue && movement.Date.Date > DateTo.Value.Date)
        {
            return false;
        }
        return true;
    }

    private static bool Contains(string value, string needle) =>
        value.Contains(needle.Trim(), StringComparison.CurrentCultureIgnoreCase);
}

/// <summary>Una singola modifica richiesta dall'utente (o proposta dall'AI).</summary>
internal sealed record DocumentEdit(
    DocumentEditKind Kind,
    MovementFilter Filter,
    string? Description = null,
    DateTime? TargetDate = null,
    int DayDelta = 0,
    int? Hours = null,
    int? Minutes = null,
    decimal? Factor = null,
    string? ReasonCode = null);

/// <summary>Esito dell'applicazione di un gruppo di modifiche.</summary>
internal sealed record DocumentEditResult(
    IReadOnlyList<DailyMovement> Movements,
    IReadOnlyList<string> Applied,
    IReadOnlyList<string> Skipped);

/// <summary>
/// Applica le modifiche in modo deterministico e controllato: l'AI propone,
/// ma è questo codice a decidere cosa cambia e a scartare le richieste non
/// valide, senza mai introdurre codici non configurati.
/// </summary>
internal static class DocumentEditor
{
    private const int MaximumMinutes = 99 * 60 + 59;

    public static DocumentEditResult Apply(
        IReadOnlyList<DailyMovement> movements,
        IReadOnlyList<DocumentEdit> edits)
    {
        var current = movements.ToList();
        var applied = new List<string>();
        var skipped = new List<string>();

        foreach (var edit in edits)
        {
            var label = string.IsNullOrWhiteSpace(edit.Description)
                ? Describe(edit)
                : edit.Description!.Trim();
            switch (edit.Kind)
            {
                case DocumentEditKind.SetTime:
                    ApplySetTime(current, edit, label, applied, skipped);
                    break;
                case DocumentEditKind.ShiftDays:
                    ApplyShiftDays(current, edit, label, applied, skipped);
                    break;
                case DocumentEditKind.ScaleHours:
                    ApplyScaleHours(current, edit, label, applied, skipped);
                    break;
                case DocumentEditKind.SetReasonCode:
                    ApplySetReasonCode(current, edit, label, applied, skipped);
                    break;
                case DocumentEditKind.Remove:
                    ApplyRemove(current, edit, label, applied, skipped);
                    break;
                default:
                    skipped.Add($"{label}: tipo di modifica non supportato.");
                    break;
            }
        }

        return new DocumentEditResult(current, applied, skipped);
    }

    /// <summary>Riepilogo compatto del documento, da inviare all'AI come contesto.</summary>
    public static string Summarize(IReadOnlyList<DailyMovement> movements, int maximumEmployees = 40)
    {
        if (movements.Count == 0)
        {
            return "Anteprima vuota: non c'è ancora alcun movimento.";
        }

        var builder = new System.Text.StringBuilder();
        var totalMinutes = movements.Sum(movement => movement.Hours * 60 + movement.Minutes);
        builder.AppendLine(
            $"Movimenti: {movements.Count}   ·   ore totali: {totalMinutes / 60}h {totalMinutes % 60:D2}min");
        builder.AppendLine(
            $"Periodo: {movements.Min(m => m.Date):yyyy-MM-dd} → {movements.Max(m => m.Date):yyyy-MM-dd}");

        var employees = movements
            .GroupBy(movement => movement.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ToArray();
        var shown = employees.Take(maximumEmployees).ToArray();
        builder.AppendLine($"Dipendenti ({employees.Length}):");
        foreach (var employee in shown)
        {
            var minutes = employee.Sum(movement => movement.Hours * 60 + movement.Minutes);
            var code = employee.First().EmployeeCode;
            builder.AppendLine(
                $"  - {employee.Key} [cod. {code}] · {employee.Count()} mov. · {minutes / 60}h {minutes % 60:D2}min");
        }
        if (employees.Length > shown.Length)
        {
            builder.AppendLine($"  … e altri {employees.Length - shown.Length} dipendenti.");
        }

        builder.AppendLine("Giustificativi presenti:");
        foreach (var policy in movements
                     .GroupBy(movement => (movement.Policy, movement.ReasonCode))
                     .OrderByDescending(group => group.Count())
                     .Take(30))
        {
            builder.AppendLine(
                $"  - “{policy.Key.Policy}” → codice {policy.Key.ReasonCode} ({policy.Count()} mov.)");
        }
        return builder.ToString();
    }

    private static void ApplySetTime(
        List<DailyMovement> current,
        DocumentEdit edit,
        string label,
        List<string> applied,
        List<string> skipped)
    {
        var hours = edit.Hours ?? 0;
        var minutes = edit.Minutes ?? 0;
        if (hours is < 0 or > 99 || minutes is < 0 or > 59)
        {
            skipped.Add($"{label}: ore o minuti fuori dai limiti del tracciato.");
            return;
        }
        var changed = 0;
        for (var index = 0; index < current.Count; index++)
        {
            if (edit.Filter.Matches(current[index]))
            {
                current[index] = current[index] with { Hours = hours, Minutes = minutes };
                changed++;
            }
        }
        if (changed == 0)
        {
            skipped.Add($"{label}: nessun movimento corrisponde al filtro.");
            return;
        }
        applied.Add($"{label}: {changed} movimenti impostati a {hours}h {minutes:D2}min.");
    }

    private static void ApplyShiftDays(
        List<DailyMovement> current,
        DocumentEdit edit,
        string label,
        List<string> applied,
        List<string> skipped)
    {
        if (edit.DayDelta == 0)
        {
            skipped.Add($"{label}: nessuno spostamento indicato.");
            return;
        }
        var changed = 0;
        for (var index = 0; index < current.Count; index++)
        {
            if (edit.Filter.Matches(current[index]))
            {
                current[index] = current[index] with { Date = current[index].Date.AddDays(edit.DayDelta) };
                changed++;
            }
        }
        if (changed == 0)
        {
            skipped.Add($"{label}: nessun movimento corrisponde al filtro.");
            return;
        }
        applied.Add($"{label}: {changed} movimenti spostati di {edit.DayDelta:+#;-#;0} giorni.");
    }

    private static void ApplyScaleHours(
        List<DailyMovement> current,
        DocumentEdit edit,
        string label,
        List<string> applied,
        List<string> skipped)
    {
        var factor = edit.Factor ?? 0m;
        if (factor <= 0m || factor > 10m)
        {
            skipped.Add($"{label}: fattore di scala non valido (usa un valore tra 0 e 10).");
            return;
        }
        var changed = 0;
        for (var index = 0; index < current.Count; index++)
        {
            if (!edit.Filter.Matches(current[index]))
            {
                continue;
            }
            var originalMinutes = current[index].Hours * 60 + current[index].Minutes;
            var scaled = (int)decimal.Round(originalMinutes * factor, 0, MidpointRounding.AwayFromZero);
            if (scaled > MaximumMinutes)
            {
                skipped.Add($"{label}: la durata risultante supera il limite del tracciato.");
                return;
            }
            current[index] = current[index] with { Hours = scaled / 60, Minutes = scaled % 60 };
            changed++;
        }
        if (changed == 0)
        {
            skipped.Add($"{label}: nessun movimento corrisponde al filtro.");
            return;
        }
        applied.Add($"{label}: {changed} movimenti riscalati (×{factor.ToString(CultureInfo.InvariantCulture)}).");
    }

    private static void ApplySetReasonCode(
        List<DailyMovement> current,
        DocumentEdit edit,
        string label,
        List<string> applied,
        List<string> skipped)
    {
        var code = edit.ReasonCode?.Trim() ?? string.Empty;
        if (code.Length is 0 or > 3)
        {
            skipped.Add($"{label}: codice giustificativo mancante o troppo lungo.");
            return;
        }
        var changed = 0;
        for (var index = 0; index < current.Count; index++)
        {
            if (edit.Filter.Matches(current[index]) &&
                !string.Equals(current[index].ReasonCode, code, StringComparison.OrdinalIgnoreCase))
            {
                current[index] = current[index] with { ReasonCode = code };
                changed++;
            }
        }
        if (changed == 0)
        {
            skipped.Add($"{label}: nessun movimento da aggiornare con il codice “{code}”.");
            return;
        }
        applied.Add($"{label}: {changed} movimenti aggiornati al codice “{code}”.");
    }

    private static void ApplyRemove(
        List<DailyMovement> current,
        DocumentEdit edit,
        string label,
        List<string> applied,
        List<string> skipped)
    {
        var removed = current.RemoveAll(movement => edit.Filter.Matches(movement));
        if (removed == 0)
        {
            skipped.Add($"{label}: nessun movimento corrisponde al filtro.");
            return;
        }
        applied.Add($"{label}: {removed} movimenti rimossi.");
    }

    private static string Describe(DocumentEdit edit)
    {
        var subject = edit.Filter.IsEmpty ? "tutti i movimenti" : DescribeFilter(edit.Filter);
        return edit.Kind switch
        {
            DocumentEditKind.SetTime => $"Imposta {subject}",
            DocumentEditKind.ShiftDays => $"Sposta {subject}",
            DocumentEditKind.ScaleHours => $"Ridimensiona {subject}",
            DocumentEditKind.SetReasonCode => $"Cambia codice a {subject}",
            DocumentEditKind.Remove => $"Rimuovi {subject}",
            _ => subject
        };
    }

    private static string DescribeFilter(MovementFilter filter)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.Employee)) parts.Add($"dipendente “{filter.Employee}”");
        if (!string.IsNullOrWhiteSpace(filter.Policy)) parts.Add($"giustificativo “{filter.Policy}”");
        if (!string.IsNullOrWhiteSpace(filter.ReasonCode)) parts.Add($"codice {filter.ReasonCode}");
        if (filter.Date.HasValue) parts.Add($"del {filter.Date.Value:dd/MM/yyyy}");
        if (filter.DateFrom.HasValue || filter.DateTo.HasValue)
        {
            var from = filter.DateFrom?.ToString("dd/MM/yyyy") ?? "inizio";
            var to = filter.DateTo?.ToString("dd/MM/yyyy") ?? "fine";
            parts.Add($"dal {from} al {to}");
        }
        return string.Join(", ", parts);
    }
}

/// <summary>
/// Converte la risposta JSON dell'AI in modifiche tipizzate, scartando in modo
/// silenzioso tutto ciò che non è riconosciuto. I codici giustificativo sono
/// accettati solo se già configurati dall'utente.
/// </summary>
internal static class DocumentEditParser
{
    public static string ParseReply(string json)
    {
        var document = TryParse(json);
        if (document is null)
        {
            return string.Empty;
        }
        return ReadString(document.RootElement, "reply")?.Trim() ?? string.Empty;
    }

    public static IReadOnlyList<DocumentEdit> Parse(
        string json,
        IReadOnlySet<string>? allowedReasonCodes = null)
    {
        var edits = new List<DocumentEdit>();
        var document = TryParse(json);
        if (document is null)
        {
            return edits;
        }
        if (!document.RootElement.TryGetProperty("edits", out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return edits;
        }

        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !TryParseKind(ReadString(element, "kind"), out var kind))
            {
                continue;
            }

            var reasonCode = ReadString(element, "reasonCode")?.Trim();
            if (kind == DocumentEditKind.SetReasonCode &&
                (reasonCode is null ||
                 (allowedReasonCodes is not null && !allowedReasonCodes.Contains(reasonCode))))
            {
                // L'AI non può introdurre codici ufficiali nuovi.
                continue;
            }

            var filter = new MovementFilter(
                Employee: ReadString(element, "employee")?.Trim(),
                Policy: ReadString(element, "policy")?.Trim(),
                ReasonCode: kind == DocumentEditKind.SetReasonCode ? null : ReadString(element, "filterReasonCode")?.Trim(),
                Date: ReadDate(element, "date"),
                DateFrom: ReadDate(element, "dateFrom"),
                DateTo: ReadDate(element, "dateTo"));

            edits.Add(new DocumentEdit(
                Kind: kind,
                Filter: filter,
                Description: ReadString(element, "summary")?.Trim(),
                TargetDate: ReadDate(element, "targetDate"),
                DayDelta: ReadInt(element, "days") ?? 0,
                Hours: ReadInt(element, "hours"),
                Minutes: ReadInt(element, "minutes"),
                Factor: ReadDecimal(element, "factor"),
                ReasonCode: reasonCode));
        }
        return edits;
    }

    private static JsonDocument? TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryParseKind(string? value, out DocumentEditKind kind)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "settime":
            case "sethours":
            case "setore":
                kind = DocumentEditKind.SetTime;
                return true;
            case "shiftdays":
            case "shift":
            case "sposta":
                kind = DocumentEditKind.ShiftDays;
                return true;
            case "scalehours":
            case "scale":
            case "ridimensiona":
                kind = DocumentEditKind.ScaleHours;
                return true;
            case "setreasoncode":
            case "setcode":
            case "codice":
                kind = DocumentEditKind.SetReasonCode;
                return true;
            case "remove":
            case "delete":
            case "rimuovi":
                kind = DocumentEditKind.Remove;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static DateTime? ReadDate(JsonElement element, string name)
    {
        var raw = ReadString(element, name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        return DateTime.TryParse(
            raw.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var date)
            ? date.Date
            : null;
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }
        var raw = ReadString(element, name);
        return raw is not null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static decimal? ReadDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }
        var raw = ReadString(element, name)?.Replace(',', '.');
        return raw is not null &&
               decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
