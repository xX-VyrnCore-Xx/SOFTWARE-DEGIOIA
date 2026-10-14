using System.Globalization;

namespace PresenzeXmlConverter;

internal static class AttendanceMovementGenerator
{
    /// <summary>Capienza di una giornata in minuti: serve solo per avvisare e per le presenze.</summary>
    private static int RuleCapacity(GenerationOptions rules) => rules.HoursPerDay is > 0m and <= 24m
        ? decimal.ToInt32(decimal.Round(rules.HoursPerDay * 60m, 0, MidpointRounding.AwayFromZero))
        : 24 * 60;

    private static bool OverlapsPeriod(AttendanceRequest request, DateTime? periodStart, DateTime? periodEnd) =>
        (!periodEnd.HasValue || request.StartDate.Date <= periodEnd.Value.Date) &&
        (!periodStart.HasValue || request.EndDate.Date >= periodStart.Value.Date);

    public static GenerationResult Generate(
        IReadOnlyList<AttendanceRequest> requests,
        IReadOnlyDictionary<string, string> employeeCodes,
        IReadOnlyDictionary<string, string> policyCodes,
        DateTime? periodStart,
        DateTime? periodEnd,
        GenerationOptions? options = null,
        IReadOnlyDictionary<string, EmployeeProfile>? profiles = null)
    {
        options ??= GenerationOptions.Default;
        var extraHolidays = options.ExtraHolidays ?? [];
        var movements = new List<DailyMovement>();
        var warnings = new List<string>();
        // Il periodo scritto in testa al foglio («From Date / To Date») serve a tagliare le
        // richieste che lo sforano. Ma se NESSUNA richiesta cade nel periodo, il periodo è
        // evidentemente sbagliato o vecchio (un export di agosto con richieste di settembre):
        // applicarlo scarterebbe tutto e l'utente vedrebbe «nessun movimento». In quel caso
        // il periodo si ignora, e lo si dice.
        if ((periodStart.HasValue || periodEnd.HasValue) && requests.Count > 0 &&
            !requests.Any(request => OverlapsPeriod(request, periodStart, periodEnd)))
        {
            var from = periodStart.HasValue ? periodStart.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "…";
            var to = periodEnd.HasValue ? periodEnd.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "…";
            warnings.Add(
                $"Il periodo indicato nel foglio ({from} – {to}) non contiene nessuna richiesta: " +
                "viene ignorato e si usano le date delle richieste. Controlla le date «From Date / To Date» in cima al file.");
            periodStart = null;
            periodEnd = null;
        }
        foreach (var request in requests)
        {
            var start = request.StartDate.Date;
            var end = request.EndDate.Date;
            if (periodStart.HasValue && start < periodStart.Value.Date)
            {
                start = periodStart.Value.Date;
            }
            if (periodEnd.HasValue && end > periodEnd.Value.Date)
            {
                end = periodEnd.Value.Date;
            }

            if (start > end)
            {
                warnings.Add($"Riga {request.SourceRow}: richiesta fuori dal periodo Excel, esclusa.");
                continue;
            }

            // Calendario e ore ricavati dalle date: il sabato entra quando serve a non
            // superare la giornata, una durata espressa in giorni diventa ore.
            // Le ore della giornata di un dipendente part-time sostituiscono quelle standard.
            var profile = profiles is not null && profiles.TryGetValue(request.EmployeeName, out var found) ? found : null;
            var rules = profile?.DailyHours is { } ownHours ? options with { HoursPerDay = ownHours } : options;
            var plan = WorkCalendar.Plan(start, end, request.Hours, rules);
            var workDays = plan.Days.ToArray();
            if (workDays.Length == 0)
            {
                warnings.Add(
                    $"Riga {request.SourceRow}: nessun giorno lavorativo fra " +
                    $"{start:dd/MM/yyyy} e {end:dd/MM/yyyy} (fine settimana, festivi" +
                    (options.IncludeSaturdays ? "" : " e sabati") + "), richiesta esclusa.");
                continue;
            }

            if (plan.Note is not null)
            {
                warnings.Add($"Riga {request.SourceRow}: {plan.Note}");
            }
            var totalMinutes = decimal.ToInt32(decimal.Round(
                plan.Hours * 60m,
                0,
                MidpointRounding.AwayFromZero));
            if (totalMinutes <= 0)
            {
                warnings.Add($"Riga {request.SourceRow}: durata non positiva, richiesta esclusa.");
                continue;
            }

            var baseMinutes = totalMinutes / workDays.Length;
            var extraMinutes = totalMinutes % workDays.Length;
            if (baseMinutes + (extraMinutes > 0 ? 1 : 0) > 99 * 60 + 59)
            {
                throw new InvalidDataException(
                    $"La durata della riga {request.SourceRow} supera la capienza del tracciato TRRIPW.");
            }

            if (!employeeCodes.TryGetValue(request.EmployeeName, out var mappedEmployeeCode) ||
                string.IsNullOrWhiteSpace(mappedEmployeeCode))
            {
                throw new InvalidDataException($"Manca il codice PAGHE per “{request.EmployeeName}”.");
            }
            if (!policyCodes.TryGetValue(request.Policy, out var mappedReasonCode) ||
                string.IsNullOrWhiteSpace(mappedReasonCode))
            {
                throw new InvalidDataException($"Manca il codice giustificativo per “{request.Policy}”.");
            }

            var employeeCode = mappedEmployeeCode.Trim();
            var reasonCode = mappedReasonCode.Trim();
            if (employeeCode.Length > 7 || reasonCode.Length > 3)
            {
                throw new InvalidDataException(
                    $"Il codice PAGHE o il giustificativo supera il limite del tracciato (riga {request.SourceRow}).");
            }
            var overloadedDays = 0;
            for (var index = 0; index < workDays.Length; index++)
            {
                var minutes = baseMinutes + (index < extraMinutes ? 1 : 0);
                if (minutes == 0)
                {
                    continue;
                }
                if (minutes > RuleCapacity(rules))
                {
                    overloadedDays++;
                }
                movements.Add(new DailyMovement(
                    request.EmployeeName,
                    employeeCode,
                    request.Policy,
                    reasonCode,
                    workDays[index],
                    minutes / 60,
                    minutes % 60,
                    request.SourceRow));
            }

            if (overloadedDays > 0)
            {
                var declaredHours = totalMinutes / 60m;
                warnings.Add(
                    $"Riga {request.SourceRow}: {declaredHours:0.##} ore su " +
                    $"{workDays.Length} {(workDays.Length == 1 ? "giorno lavorativo" : "giorni lavorativi")} " +
                    $"superano la giornata di {options.HoursPerDay:0.##}h in {overloadedDays} " +
                    $"{(overloadedDays == 1 ? "giorno" : "giorni")}. Controlla i dati del foglio.");
            }
        }

        foreach (var dateGroup in movements.GroupBy(movement => (movement.EmployeeCode, movement.Date)))
        {
            var sourceRows = dateGroup.Select(movement => movement.SourceRow)
                .Where(row => row > 0)
                .Distinct()
                .Order()
                .ToArray();
            if (sourceRows.Length > 1)
            {
                warnings.Add(
                    $"Dipendente {dateGroup.Key.EmployeeCode}, {dateGroup.Key.Date:dd/MM/yyyy}: più richieste si sovrappongono (righe {string.Join(", ", sourceRows)}). Verifica che le ore non siano duplicate.");
            }
        }

        var merged = MergeSameReasonMovements(movements, warnings);
        if (options.FillPresence)
        {
            AddPresenceDays(merged, periodStart, periodEnd, options, profiles);
        }

        return new GenerationResult(
            merged.OrderBy(movement => movement.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(movement => movement.Date)
                .ThenBy(movement => movement.ReasonCode, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            warnings);
    }

    /// <summary>
    /// Ogni giorno lavorativo (lun-ven, senza festivi) in cui il dipendente non ha una giornata
    /// già coperta da assenze riceve una presenza con il giustificativo «01» (ore ordinarie):
    /// le ore sono quelle standard meno le ore di assenza dello stesso giorno. Il periodo è
    /// quello del foglio oppure i mesi interi toccati dai movimenti.
    /// </summary>
    private static void AddPresenceDays(
        List<DailyMovement> movements,
        DateTime? periodStart,
        DateTime? periodEnd,
        GenerationOptions options,
        IReadOnlyDictionary<string, EmployeeProfile>? profiles)
    {
        if (movements.Count == 0 || string.IsNullOrWhiteSpace(options.PresenceCode))
        {
            return;
        }
        var first = movements.Min(movement => movement.Date).Date;
        var last = movements.Max(movement => movement.Date).Date;
        var from = periodStart?.Date ?? new DateTime(first.Year, first.Month, 1);
        var to = periodEnd?.Date ?? new DateTime(last.Year, last.Month, DateTime.DaysInMonth(last.Year, last.Month));
        if (from > to)
        {
            return;
        }

        var extraHolidays = options.ExtraHolidays ?? [];
        var presence = new List<DailyMovement>();
        foreach (var employee in movements.GroupBy(movement => movement.EmployeeCode, StringComparer.OrdinalIgnoreCase))
        {
            var sample = employee.First();
            profiles ??= new Dictionary<string, EmployeeProfile>();
            var profile = profiles.TryGetValue(sample.EmployeeName, out var own) ? own : null;
            var capacityMinutes = RuleCapacity(profile?.DailyHours is { } hours ? options with { HoursPerDay = hours } : options);
            var absentByDay = employee
                .GroupBy(movement => movement.Date.Date)
                .ToDictionary(group => group.Key, group => group.Sum(movement => movement.Hours * 60 + movement.Minutes));
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                var weekday = day.DayOfWeek;
                if (weekday == DayOfWeek.Sunday ||
                    (weekday == DayOfWeek.Saturday && !options.IncludeSaturdays) ||
                    ItalianHolidays.Contains(day, extraHolidays))
                {
                    continue;
                }
                // Prima dell'assunzione o dopo la cessazione non c'è nessuna presenza da registrare.
                if (profile is not null && !profile.Covers(day))
                {
                    continue;
                }
                var residual = capacityMinutes - (absentByDay.TryGetValue(day, out var absent) ? absent : 0);
                if (residual <= 0)
                {
                    continue;
                }
                presence.Add(new DailyMovement(
                    sample.EmployeeName,
                    sample.EmployeeCode,
                    "Presenza",
                    options.PresenceCode.Trim(),
                    day,
                    residual / 60,
                    residual % 60,
                    0,
                    string.IsNullOrWhiteSpace(options.PresenceOfficialCode) ? null : options.PresenceOfficialCode.Trim()));
            }
        }
        movements.AddRange(presence);
    }

    /// <summary>
    /// Unisce i movimenti duplicati (stesso dipendente, data e giustificativo) sommando le ore:
    /// nel tracciato TRRIPW non devono comparire movimenti identici ripetuti, e sommare
    /// è l'unico modo di non perdere ore quando più righe Excel descrivono lo stesso assenza.
    /// </summary>
    private static List<DailyMovement> MergeSameReasonMovements(
        List<DailyMovement> movements,
        List<string> warnings)
    {
        if (movements.Count == 0)
        {
            return movements;
        }

        var merged = new List<DailyMovement>(movements.Count);
        foreach (var group in movements.GroupBy(movement =>
                     (movement.EmployeeCode, movement.Date, movement.ReasonCode),
                     new MovementKeyComparer()).OrderBy(group => group.Key.Date))
        {
            var groupMovements = group.ToArray();
            if (groupMovements.Length == 1)
            {
                merged.Add(groupMovements[0]);
                continue;
            }

            var totalMinutes = groupMovements.Sum(movement => movement.Hours * 60 + movement.Minutes);
            if (totalMinutes > 24 * 60)
            {
                warnings.Add(
                    $"Dipendente {group.Key.EmployeeCode}, {group.Key.Date:dd/MM/yyyy}: le richieste duplicate sommano a {totalMinutes / 60}h {totalMinutes % 60:D2} min (oltre 24 ore). Verifica i dati sorgente.");
            }
            var representative = groupMovements[0];
            merged.Add(representative with
            {
                Hours = totalMinutes / 60,
                Minutes = totalMinutes % 60,
                SourceRow = groupMovements.Min(movement => movement.SourceRow)
            });
        }
        return merged;
    }

    private sealed class MovementKeyComparer : IEqualityComparer<(string EmployeeCode, DateTime Date, string ReasonCode)>
    {
        public bool Equals(
            (string EmployeeCode, DateTime Date, string ReasonCode) x,
            (string EmployeeCode, DateTime Date, string ReasonCode) y) =>
            x.Date == y.Date &&
            string.Equals(x.EmployeeCode, y.EmployeeCode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ReasonCode, y.ReasonCode, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string EmployeeCode, DateTime Date, string ReasonCode) obj) =>
            HashCode.Combine(
                obj.Date,
                obj.EmployeeCode.ToUpperInvariant(),
                obj.ReasonCode.ToUpperInvariant());
    }

    private static IEnumerable<DateTime> EnumerateWorkDays(
        DateTime start,
        DateTime end,
        GenerationOptions options)
    {
        var extraHolidays = options.ExtraHolidays ?? [];
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (day.DayOfWeek == DayOfWeek.Sunday ||
                (day.DayOfWeek == DayOfWeek.Saturday && !options.IncludeSaturdays) ||
                ItalianHolidays.Contains(day) ||
                extraHolidays.Contains(day.Date))
            {
                continue;
            }
            yield return day;
        }
    }
}

/// <summary>Giorni lavorativi e ore effettive di una richiesta, con l'eventuale nota su cosa è stato corretto.</summary>
internal sealed record WorkPlan(IReadOnlyList<DateTime> Days, decimal Hours, string? Note);

/// <summary>
/// Ricava dalle date di una richiesta quali giorni sono lavorativi e quante ore sono
/// davvero. Le regole sono due, entrambe attive con <see cref="GenerationOptions.AutoDetect"/>:
/// <list type="bullet">
/// <item>il sabato conta solo quando serve: se le ore stanno nella giornata sui soli giorni
/// feriali restano quelli (40 h dal lunedì al sabato = 5 giorni da 8 h); se non ci
/// stanno (48 h) il sabato è lavorativo; una richiesta che cade solo di sabato lo usa;</item>
/// <item>una durata uguale al numero di giorni (5 su 5 giorni) è espressa in giorni, non in
/// ore: diventa 5 × la giornata standard.</item>
/// </list>
/// Con «sabato lavorativo» attivo nelle impostazioni il sabato conta sempre.
/// </summary>
internal static class WorkCalendar
{
    public static WorkPlan Plan(DateTime start, DateTime end, decimal hours, GenerationOptions options)
    {
        var dayHours = options.HoursPerDay is > 0m and <= 24m ? options.HoursPerDay : 8m;
        var weekdays = Days(start, end, options, includeSaturdays: false);
        var withSaturdays = Days(start, end, options, includeSaturdays: true);
        string? note = null;

        if (options.AutoDetect && hours > 0m && hours < dayHours && dayHours > 1m)
        {
            // Durata in giorni: coincide con quanti giorni lavorativi ha l'intervallo.
            foreach (var count in new[] { weekdays.Count, withSaturdays.Count }.Distinct())
            {
                if (count >= 2 && hours == count)
                {
                    var converted = count * dayHours;
                    note = $"durata di {hours:0.##} interpretata come {count} giorni = {converted:0.##} ore.";
                    hours = converted;
                    break;
                }
            }
        }

        if (options.IncludeSaturdays)
        {
            return new WorkPlan(withSaturdays, hours, note);
        }
        if (!options.AutoDetect || weekdays.Count == withSaturdays.Count)
        {
            return new WorkPlan(weekdays, hours, note);
        }
        if (weekdays.Count == 0)
        {
            return new WorkPlan(withSaturdays, hours, Join(note, "la richiesta cade di sabato: sabato considerato lavorativo."));
        }
        if (hours <= (weekdays.Count * dayHours) + 0.01m)
        {
            return new WorkPlan(weekdays, hours, note);
        }
        if (hours <= (withSaturdays.Count * dayHours) + 0.01m)
        {
            return new WorkPlan(
                withSaturdays,
                hours,
                Join(note, $"{hours:0.##} ore non stanno nei soli giorni feriali: sabato considerato lavorativo."));
        }
        return new WorkPlan(weekdays, hours, note);
    }

    private static string Join(string? first, string second) => first is null ? second : $"{first} {second}";

    private static List<DateTime> Days(DateTime start, DateTime end, GenerationOptions options, bool includeSaturdays)
    {
        var extraHolidays = options.ExtraHolidays ?? [];
        var days = new List<DateTime>();
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
        {
            if (day.DayOfWeek == DayOfWeek.Sunday ||
                (day.DayOfWeek == DayOfWeek.Saturday && !includeSaturdays) ||
                ItalianHolidays.Contains(day) ||
                extraHolidays.Contains(day))
            {
                continue;
            }
            days.Add(day);
        }
        return days;
    }
}

internal static class ItalianHolidays
{
    /// <summary>Festività nazionali più le chiusure aziendali aggiunte dall'utente.</summary>
    public static bool Contains(DateTime date, IReadOnlyCollection<DateTime>? extraHolidays = null) =>
        IsNationalHoliday(date) ||
        (extraHolidays is not null && extraHolidays.Contains(date.Date));

    public static bool IsNationalHoliday(DateTime date)
    {
        var day = date.Date;
        return (day.Month, day.Day) is
            (1, 1) or (1, 6) or (4, 25) or (5, 1) or (6, 2) or
            (8, 15) or (11, 1) or (12, 8) or (12, 25) or (12, 26) ||
            day == EasterMonday(day.Year);
    }

    private static DateTime EasterMonday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateTime(year, month, day).AddDays(1);
    }
}
