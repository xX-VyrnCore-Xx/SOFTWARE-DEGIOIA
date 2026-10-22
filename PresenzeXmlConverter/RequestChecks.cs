using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>
/// Controlli sulle richieste lette dal foglio (prima della generazione dei movimenti): richieste che si sovrappongono,
/// malattie molto lunghe e confronto con l'ultima consegna della stessa azienda. Solo dati, senza finestre.
/// </summary>
internal static class RequestChecks
{
    private const int MaximumFindings = 30;

    public static IReadOnlyList<MovementFinding> Review(IReadOnlyList<AttendanceRequest> requests, ValidationRules? rules = null)
    {
        rules ??= ValidationRules.Default;
        var findings = new List<MovementFinding>();
        foreach (var person in requests
                     .Where(request => request.EndDate.Date >= request.StartDate.Date)
                     .GroupBy(request => EmployeeNames.Key(request.EmployeeName))
                     .Where(group => group.Key.Length > 0)
                     .OrderBy(group => group.First().EmployeeName, StringComparer.CurrentCultureIgnoreCase))
        {
            var ordered = person.OrderBy(request => request.StartDate).ThenBy(request => request.SourceRow).ToList();
            if (rules.CheckOverlaps)
            {
                FindOverlaps(ordered, findings);
            }
            FindLongIllnesses(ordered, findings, rules.LongIllnessDays);
            if (rules.LongAbsenceDays > 0)
            {
                FindLongAbsences(ordered, findings, rules.LongAbsenceDays);
            }
            if (findings.Count >= MaximumFindings)
            {
                break;
            }
        }
        return findings.Take(MaximumFindings).ToArray();
    }

    private static void FindOverlaps(List<AttendanceRequest> ordered, List<MovementFinding> findings)
    {
        AttendanceRequest? reaching = null;
        foreach (var request in ordered)
        {
            if (reaching is not null && request.StartDate.Date <= reaching.EndDate.Date)
            {
                var overlapStart = request.StartDate.Date;
                var overlapEnd = request.EndDate.Date < reaching.EndDate.Date ? request.EndDate.Date : reaching.EndDate.Date;
                var same = string.Equals(request.Policy, reaching.Policy, StringComparison.CurrentCultureIgnoreCase) &&
                           request.StartDate.Date == reaching.StartDate.Date &&
                           request.EndDate.Date == reaching.EndDate.Date;
                findings.Add(new MovementFinding(
                    MovementSeverity.Warning,
                    same
                        ? $"“{request.EmployeeName}”: le righe {reaching.SourceRow} e {request.SourceRow} sono la stessa richiesta ({Period(overlapStart, overlapEnd)}): forse è doppia."
                        : $"“{request.EmployeeName}”: le righe {reaching.SourceRow} ({reaching.Policy}) e {request.SourceRow} ({request.Policy}) " +
                          $"si sovrappongono {Period(overlapStart, overlapEnd)}."));
            }
            if (reaching is null || request.EndDate.Date > reaching.EndDate.Date)
            {
                reaching = request;
            }
        }
    }

    private static void FindLongIllnesses(List<AttendanceRequest> ordered, List<MovementFinding> findings, int limit)
    {
        var sick = ordered.Where(request => WorkbookInspection.IsSickPolicy(request.Policy)).ToList();
        if (sick.Count == 0)
        {
            return;
        }
        // Malattie attaccate (una finisce, l'altra comincia il giorno dopo) contano come un'unica malattia.
        var runStart = sick[0].StartDate.Date;
        var runEnd = sick[0].EndDate.Date;
        var rows = new List<int> { sick[0].SourceRow };
        void Close()
        {
            var days = (runEnd - runStart).Days + 1;
            if (days > limit)
            {
                findings.Add(new MovementFinding(
                    MovementSeverity.Warning,
                    $"“{sick[0].EmployeeName}”: malattia di {days} giorni di fila {Period(runStart, runEnd)} " +
                    $"(riga {string.Join(", ", rows)}), oltre {limit}. Verifica che sia giusto."));
            }
        }
        foreach (var request in sick.Skip(1))
        {
            if (request.StartDate.Date <= runEnd.AddDays(1))
            {
                runEnd = request.EndDate.Date > runEnd ? request.EndDate.Date : runEnd;
                rows.Add(request.SourceRow);
                continue;
            }
            Close();
            runStart = request.StartDate.Date;
            runEnd = request.EndDate.Date;
            rows = new List<int> { request.SourceRow };
        }
        Close();
    }

    /// <summary>
    /// Assenze (non malattia) lunghe: richieste dello stesso giustificativo attaccate o sovrapposte che superano
    /// <paramref name="limit"/> giorni di calendario di fila.
    /// </summary>
    private static void FindLongAbsences(List<AttendanceRequest> ordered, List<MovementFinding> findings, int limit)
    {
        foreach (var policy in ordered
                     .Where(request => !WorkbookInspection.IsSickPolicy(request.Policy))
                     .GroupBy(request => request.Policy.Trim(), StringComparer.CurrentCultureIgnoreCase))
        {
            var items = policy.OrderBy(request => request.StartDate).ToList();
            var runStart = items[0].StartDate.Date;
            var runEnd = items[0].EndDate.Date;
            var rows = new List<int> { items[0].SourceRow };
            void Close()
            {
                var days = (runEnd - runStart).Days + 1;
                if (days > limit)
                {
                    findings.Add(new MovementFinding(
                        MovementSeverity.Warning,
                        $"“{items[0].EmployeeName}”: {policy.Key} per {days} giorni di fila {Period(runStart, runEnd)} " +
                        $"(riga {string.Join(", ", rows)}), oltre {limit}. Verifica che sia giusto."));
                }
            }
            foreach (var request in items.Skip(1))
            {
                if (request.StartDate.Date <= runEnd.AddDays(1))
                {
                    runEnd = request.EndDate.Date > runEnd ? request.EndDate.Date : runEnd;
                    rows.Add(request.SourceRow);
                    continue;
                }
                Close();
                runStart = request.StartDate.Date;
                runEnd = request.EndDate.Date;
                rows = new List<int> { request.SourceRow };
            }
            Close();
        }
    }

    /// <summary>I dipendenti che l'azienda si aspetta nel foglio ma che non ci sono (nome scritto in qualunque ordine).</summary>
    public static IReadOnlyList<MovementFinding> CheckExpectedEmployees(IReadOnlyList<AttendanceRequest> requests, ValidationRules rules)
    {
        var expected = rules.ExpectedList;
        if (expected.Count == 0 || requests.Count == 0)
        {
            return [];
        }
        var present = requests.Select(request => EmployeeNames.Key(request.EmployeeName)).ToHashSet(StringComparer.Ordinal);
        return expected
            .Where(name => !present.Contains(EmployeeNames.Key(name)))
            .Take(MaximumFindings)
            .Select(name => new MovementFinding(
                MovementSeverity.Warning,
                $"“{name}” è tra i dipendenti attesi di questa azienda ma non compare nel foglio."))
            .ToArray();
    }

    /// <summary>
    /// Confronta la consegna in preparazione con l'ultima della stessa azienda: un calo o un aumento forte di
    /// dipendenti o ore è quasi sempre un foglio incompleto o un mese sbagliato, ed è una nota, non un errore.
    /// </summary>
    public static IReadOnlyList<MovementFinding> CompareWithPreviousDelivery(MovementTotals current, ExportRecord? previous, int changePercent = ValidationRules.DefaultChangePercent)
    {
        if (previous is null || previous.TotalMinutes <= 0 || current.Movements == 0)
        {
            return [];
        }
        var findings = new List<MovementFinding>();
        var when = previous.ExportedAtUtc.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);
        var minutesChange = Percent(current.TotalMinutes, previous.TotalMinutes);
        var employeesChange = previous.Employees > 0 ? Percent(current.Employees, previous.Employees) : 0;
        if (Math.Abs(minutesChange) >= changePercent || Math.Abs(employeesChange) >= changePercent)
        {
            findings.Add(new MovementFinding(
                MovementSeverity.Note,
                $"Rispetto all'ultima consegna ({when}): dipendenti {previous.Employees:N0} → {current.Employees:N0}, " +
                $"ore {previous.TotalMinutes / 60:N0} → {current.TotalMinutes / 60:N0} ({Signed(minutesChange)}%)."));
        }
        return findings;
    }

    private static int Percent(int now, int before) =>
        before == 0 ? 0 : (int)Math.Round((now - before) * 100.0 / before, MidpointRounding.AwayFromZero);

    private static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture).Replace("-", "−", StringComparison.Ordinal);

    private static string Period(DateTime from, DateTime to) => from == to
        ? $"il {from:dd/MM/yyyy}"
        : $"dal {from:dd/MM/yyyy} al {to:dd/MM/yyyy}";
}
