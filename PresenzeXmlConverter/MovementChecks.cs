namespace PresenzeXmlConverter;

internal enum MovementSeverity
{
    Error,
    Warning,
    Note
}

/// <summary>Una segnalazione sui movimenti generati, con la sua gravità.</summary>
internal sealed record MovementFinding(MovementSeverity Severity, string Message)
{
    public string Label => Severity switch
    {
        MovementSeverity.Error => "ERRORE",
        MovementSeverity.Warning => "ATTENZIONE",
        _ => "NOTA"
    };
}

/// <summary>Riepilogo numerico dei movimenti generati, usato in anteprima e nei controlli.</summary>
internal sealed record MovementTotals(int Movements, int Employees, int Days, int TotalMinutes)
{
    public static MovementTotals Empty { get; } = new(0, 0, 0, 0);

    /// <summary>Ore e minuti totali in forma leggibile ("42 h 30 min").</summary>
    public string TotalText => $"{TotalMinutes / 60:N0} h {TotalMinutes % 60:D2} min";
}

/// <summary>
/// Controlli deterministici sui movimenti generati: girano sempre, in locale e
/// senza rete, e sono la prima cosa che l'utente vede nell'area di revisione.
/// </summary>
internal static class MovementChecks
{
    private const int MaximumFindings = 120;

    /// <summary>
    /// Giustificativo riservato dal tracciato TRRIPW: PAGHE scarta un movimento
    /// comunicato con questo codice. È comodo per neutralizzare una riga senza
    /// toglierla dall'Excel, ma non produce ore: va segnalato.
    /// </summary>
    public const string DiscardReasonCode = "999";

    public static IReadOnlyList<MovementFinding> Review(
        IReadOnlyList<DailyMovement> movements,
        IReadOnlyDictionary<string, string> policyCodes,
        IReadOnlyCollection<string> sourceEmployees,
        DateTime? periodStart,
        DateTime? periodEnd,
        decimal dailyHours = 8m,
        bool includeSaturdays = false)
    {
        var findings = new List<MovementFinding>();
        if (movements.Count == 0)
        {
            findings.Add(new MovementFinding(MovementSeverity.Error, "Non è stato generato alcun movimento."));
            return findings;
        }

        if (dailyHours <= 0m || dailyHours > 24m)
        {
            dailyHours = 8m;
        }
        var dailyCapacityMinutes = decimal.ToInt32(decimal.Round(dailyHours * 60m, 0, MidpointRounding.AwayFromZero));
        var allowedCodes = policyCodes.Values
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var group in movements.GroupBy(movement => (movement.EmployeeCode, movement.Date)))
        {
            var minutes = group.Sum(movement => movement.Hours * 60 + movement.Minutes);
            // Le presenze aggiunte dall'app non sono richieste del foglio: non contano come «più giustificativi».
            var codes = group.Where(movement => !AttendanceMovementGenerator.IsPresence(movement))
                .Select(movement => movement.ReasonCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (minutes > 24 * 60)
            {
                findings.Add(new MovementFinding(
                    MovementSeverity.Error,
                    $"{Label(group.First())}: {minutes / 60}h {minutes % 60:D2}m in un solo giorno, oltre il limite di 24 ore."));
            }
            else if (minutes > dailyCapacityMinutes)
            {
                findings.Add(new MovementFinding(
                    MovementSeverity.Warning,
                    $"{Label(group.First())}: {minutes / 60}h {minutes % 60:D2}m in un solo giorno, " +
                    $"oltre la giornata standard di {dailyHours:0.##}h. Verifica che sia corretto."));
            }
            if (codes.Length > 1)
            {
                findings.Add(new MovementFinding(
                    MovementSeverity.Note,
                    $"{Label(group.First())}: più giustificativi nello stesso giorno ({string.Join(", ", codes)})."));
            }
        }

        foreach (var movement in movements)
        {
            if (movement.Date.DayOfWeek == DayOfWeek.Sunday ||
                (movement.Date.DayOfWeek == DayOfWeek.Saturday && !includeSaturdays))
            {
                findings.Add(new MovementFinding(
                    MovementSeverity.Warning,
                    $"{Label(movement)}: il {movement.Date:dd/MM/yyyy} è {(movement.Date.DayOfWeek == DayOfWeek.Sunday ? "domenica" : "sabato")}."));
            }
            else if (ItalianHolidays.Contains(movement.Date))
            {
                findings.Add(new MovementFinding(
                    MovementSeverity.Warning,
                    $"{Label(movement)}: il {movement.Date:dd/MM/yyyy} è un giorno festivo."));
            }

            if (periodStart.HasValue && movement.Date < periodStart.Value.Date)
            {
                findings.Add(new MovementFinding(
                    MovementSeverity.Warning,
                    $"{Label(movement)}: il {movement.Date:dd/MM/yyyy} è prima dell’inizio del periodo dichiarato."));
            }
            if (periodEnd.HasValue && movement.Date > periodEnd.Value.Date)
            {
                findings.Add(new MovementFinding(
                    MovementSeverity.Warning,
                    $"{Label(movement)}: il {movement.Date:dd/MM/yyyy} è dopo la fine del periodo dichiarato."));
            }

            if (allowedCodes.Count > 0 && !AttendanceMovementGenerator.IsPresence(movement) &&
                !allowedCodes.Contains(movement.ReasonCode))
            {
                findings.Add(new MovementFinding(
                    MovementSeverity.Error,
                    $"{Label(movement)}: il codice “{movement.ReasonCode}” non è fra i giustificativi configurati."));
            }
            if (string.Equals(movement.ReasonCode, DiscardReasonCode, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new MovementFinding(
                    MovementSeverity.Note,
                    $"{Label(movement)}: il {movement.Date:dd/MM/yyyy} usa il giustificativo " +
                    $"{DiscardReasonCode}, che PAGHE scarta: quel giorno non riceverà ore."));
            }
            if (string.IsNullOrWhiteSpace(movement.EmployeeCode))
            {
                findings.Add(new MovementFinding(
                    MovementSeverity.Error,
                    $"{Label(movement)}: manca il codice PAGHE del dipendente."));
            }
        }

        // Lo stesso dipendente con due codici diversi è quasi sempre un errore di anagrafica.
        foreach (var duplicate in movements
                     .GroupBy(movement => movement.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
                     .Where(group => group.Select(movement => movement.EmployeeCode)
                         .Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1))
        {
            findings.Add(new MovementFinding(
                MovementSeverity.Error,
                $"“{duplicate.Key}” compare con più codici PAGHE: {string.Join(", ", duplicate.Select(movement => movement.EmployeeCode).Distinct())}."));
        }

        // Insieme dei nomi presenti nei movimenti, costruito una volta: confrontare ogni nome
        // del foglio con ogni movimento era un ciclo dentro un ciclo (centinaia di
        // dipendenti per migliaia di movimenti, con confronti sensibili alla cultura).
        var namesWithMovements = new HashSet<string>(
            movements.Select(movement => movement.EmployeeName),
            StringComparer.CurrentCultureIgnoreCase);
        foreach (var missing in sourceEmployees
                     .Where(name => !namesWithMovements.Contains(name))
                     .Take(20))
        {
            findings.Add(new MovementFinding(
                MovementSeverity.Warning,
                $"“{missing}” è presente nel foglio ma non ha prodotto alcun movimento."));
        }

        // I controlli più gravi per primi, senza superare il limite mostrabile.
        return findings
            .OrderBy(finding => finding.Severity)
            .Take(MaximumFindings)
            .ToArray();
    }

    /// <summary>
    /// Confronta le ore generate con quelle dichiarate nel foglio: una differenza
    /// non è per forza un errore (il periodo può tagliare una richiesta), ma
    /// quando c'è va mostrata.
    /// </summary>
    public static IReadOnlyList<MovementFinding> CompareWithSource(
        IReadOnlyList<DailyMovement> movements,
        IReadOnlyList<AttendanceRequest> requests)
    {
        if (movements.Count == 0 || requests.Count == 0)
        {
            return [];
        }

        var findings = new List<MovementFinding>();
        // Si confrontano solo le ore delle richieste: le presenze aggiunte dall'app non stanno nel foglio.
        var generated = movements
            .Where(movement => !AttendanceMovementGenerator.IsPresence(movement))
            .GroupBy(movement => movement.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(movement => movement.Hours * 60 + movement.Minutes),
                StringComparer.CurrentCultureIgnoreCase);
        // Una grafia diversa dello stesso dipendente si confronta con il nome usato nei movimenti.
        var generatedNames = movements.Select(movement => movement.EmployeeName).Distinct().ToArray();
        string NameInMovements(string name) =>
            generatedNames.FirstOrDefault(candidate => string.Equals(candidate, name, StringComparison.CurrentCultureIgnoreCase)) ??
            generatedNames.FirstOrDefault(candidate => EmployeeNames.SamePerson(candidate, name)) ??
            name;
        var declared = requests
            .GroupBy(request => NameInMovements(request.EmployeeName), StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => decimal.ToInt32(decimal.Round(
                    group.Sum(request => request.Hours * 60m),
                    0,
                    MidpointRounding.AwayFromZero)),
                StringComparer.CurrentCultureIgnoreCase);

        foreach (var employee in declared.Keys.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase))
        {
            var expected = declared[employee];
            var actual = generated.TryGetValue(employee, out var value) ? value : 0;
            if (expected == actual)
            {
                continue;
            }
            findings.Add(new MovementFinding(
                MovementSeverity.Note,
                $"“{employee}”: nel foglio {expected / 60}h {expected % 60:D2}m, generati {actual / 60}h {actual % 60:D2}m " +
                "(differenza dovuta a periodo del foglio o festivi)."));
            if (findings.Count == 20)
            {
                break;
            }
        }
        return findings;
    }

    /// <summary>
    /// Ore del mese di ogni dipendente contro quelle teoriche (giorni lavorativi del mese × ore della sua
    /// giornata, nel suo rapporto di lavoro). Ha senso solo con le presenze automatiche accese: senza, nel
    /// file ci sono le sole assenze e il confronto non vuol dire nulla.
    /// </summary>
    public static IReadOnlyList<MovementFinding> CheckMonthlyHours(
        IReadOnlyList<DailyMovement> movements,
        GenerationOptions options,
        IReadOnlyDictionary<string, EmployeeProfile>? profiles,
        DateTime? periodStart,
        DateTime? periodEnd)
    {
        var findings = new List<MovementFinding>();
        if (!options.FillPresence || movements.Count == 0)
        {
            return findings;
        }

        // Il periodo del foglio vale solo se tocca davvero i movimenti (come fa la generazione).
        var first = movements.Min(movement => movement.Date).Date;
        var last = movements.Max(movement => movement.Date).Date;
        var usePeriod = (periodStart.HasValue || periodEnd.HasValue) &&
                        (!periodEnd.HasValue || first <= periodEnd.Value.Date) &&
                        (!periodStart.HasValue || last >= periodStart.Value.Date);
        var from = usePeriod && periodStart.HasValue ? periodStart.Value.Date : new DateTime(first.Year, first.Month, 1);
        var to = usePeriod && periodEnd.HasValue
            ? periodEnd.Value.Date
            : new DateTime(last.Year, last.Month, DateTime.DaysInMonth(last.Year, last.Month));
        var extraHolidays = options.ExtraHolidays ?? [];

        foreach (var employee in movements
                     .GroupBy(movement => movement.EmployeeCode, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.First().EmployeeName, StringComparer.CurrentCultureIgnoreCase))
        {
            var name = employee.First().EmployeeName;
            var profile = profiles is not null && profiles.TryGetValue(name, out var found) ? found : null;
            var hours = profile?.DailyHours ?? options.HoursPerDay;
            if (hours <= 0m || hours > 24m)
            {
                hours = 8m;
            }
            var dayMinutes = decimal.ToInt32(decimal.Round(hours * 60m, 0, MidpointRounding.AwayFromZero));

            foreach (var month in employee
                         .GroupBy(movement => (movement.Date.Year, movement.Date.Month))
                         .OrderBy(group => group.Key))
            {
                var monthStart = new DateTime(month.Key.Year, month.Key.Month, 1);
                var monthEnd = new DateTime(month.Key.Year, month.Key.Month, DateTime.DaysInMonth(month.Key.Year, month.Key.Month));
                var expected = 0;
                for (var day = monthStart < from ? from : monthStart; day <= (monthEnd > to ? to : monthEnd); day = day.AddDays(1))
                {
                    if (day.DayOfWeek == DayOfWeek.Sunday ||
                        (day.DayOfWeek == DayOfWeek.Saturday && !options.IncludeSaturdays) ||
                        ItalianHolidays.Contains(day, extraHolidays) ||
                        (profile is not null && !profile.Covers(day)))
                    {
                        continue;
                    }
                    expected += dayMinutes;
                }
                var actual = month.Sum(movement => movement.Hours * 60 + movement.Minutes);
                if (actual == expected)
                {
                    continue;
                }
                var difference = actual - expected;
                findings.Add(new MovementFinding(
                    MovementSeverity.Warning,
                    $"“{name}” · {monthStart:MMMM yyyy}: {Format(actual)} registrate su {Format(expected)} teoriche " +
                    $"({(difference > 0 ? "+" : "−")}{Format(Math.Abs(difference))})."));
                if (findings.Count == 30)
                {
                    return findings;
                }
            }
        }
        return findings;
    }

    /// <summary>Dipendenti che in un mese superano il tetto di ore fissato dall'azienda (0 = nessun tetto).</summary>
    public static IReadOnlyList<MovementFinding> CheckMonthLimit(IReadOnlyList<DailyMovement> movements, decimal maxMonthHours)
    {
        if (maxMonthHours <= 0m || movements.Count == 0)
        {
            return [];
        }
        var limit = decimal.ToInt32(decimal.Round(maxMonthHours * 60m, 0, MidpointRounding.AwayFromZero));
        var findings = new List<MovementFinding>();
        foreach (var employee in movements
                     .GroupBy(movement => movement.EmployeeCode, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.First().EmployeeName, StringComparer.CurrentCultureIgnoreCase))
        {
            foreach (var month in employee
                         .GroupBy(movement => (movement.Date.Year, movement.Date.Month))
                         .OrderBy(group => group.Key))
            {
                var total = month.Sum(movement => movement.Hours * 60 + movement.Minutes);
                if (total <= limit)
                {
                    continue;
                }
                var monthName = new DateTime(month.Key.Year, month.Key.Month, 1).ToString("MMMM yyyy");
                findings.Add(new MovementFinding(
                    MovementSeverity.Warning,
                    $"{Label(employee.First())} · {monthName}: {Format(total)} registrate, oltre il tetto di {Format(limit)} al mese."));
                if (findings.Count == 30)
                {
                    return findings;
                }
            }
        }
        return findings;
    }

    private static string Format(int minutes) => minutes % 60 == 0
        ? $"{minutes / 60}h"
        : $"{minutes / 60}h {minutes % 60:D2}m";

    /// <summary>Totali dei movimenti: righe, dipendenti distinti, giorni distinti e minuti.</summary>
    public static MovementTotals Summarize(IReadOnlyList<DailyMovement> movements)
    {
        if (movements.Count == 0)
        {
            return MovementTotals.Empty;
        }
        return new MovementTotals(
            movements.Count,
            movements.Select(movement => movement.EmployeeCode)
                .Where(code => code.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            movements.Select(movement => movement.Date.Date).Distinct().Count(),
            movements.Sum(movement => movement.Hours * 60 + movement.Minutes));
    }

    /// <summary>Conta le segnalazioni per gravità, per il riepilogo della scheda Controlli.</summary>
    public static (int Errors, int Warnings, int Notes) CountBySeverity(IReadOnlyList<MovementFinding> findings)
    {
        var errors = 0;
        var warnings = 0;
        var notes = 0;
        foreach (var finding in findings)
        {
            switch (finding.Severity)
            {
                case MovementSeverity.Error:
                    errors++;
                    break;
                case MovementSeverity.Warning:
                    warnings++;
                    break;
                default:
                    notes++;
                    break;
            }
        }
        return (errors, warnings, notes);
    }

    private static string Label(DailyMovement movement) =>
        movement.EmployeeCode.Length > 0
            ? $"{movement.EmployeeName} ({movement.EmployeeCode})"
            : movement.EmployeeName;
}
