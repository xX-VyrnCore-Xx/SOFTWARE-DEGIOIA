using System.Globalization;

namespace PresenzeXmlConverter;

internal enum CalendarCellKind
{
    /// <summary>Giorno lavorativo senza nulla da mostrare (le presenze automatiche sono spente).</summary>
    Empty,
    /// <summary>Domenica, festivo, chiusura, sabato non lavorativo o fuori dal rapporto di lavoro.</summary>
    NonWorking,
    /// <summary>Giorno lavorativo senza movimenti, con le presenze automatiche accese: un buco.</summary>
    Gap,
    Presence,
    Absence,
    /// <summary>Assenza e presenza nello stesso giorno (mezza giornata).</summary>
    Mixed
}

/// <summary>Un giorno di un dipendente nel calendario.</summary>
internal sealed record CalendarDay(
    DateTime Date,
    CalendarCellKind Kind,
    int PresenceMinutes,
    int AbsenceMinutes,
    string? AbsenceCode,
    int AbsenceColor,
    AlertSeverity? Alert)
{
    public int Minutes => PresenceMinutes + AbsenceMinutes;
}

/// <summary>Una riga del calendario: un dipendente, i suoi giorni del mese e i totali.</summary>
internal sealed record CalendarRow(
    string EmployeeName,
    string EmployeeCode,
    IReadOnlyList<CalendarDay> Days,
    int TotalMinutes,
    int Gaps,
    int DifferenceMinutes)
{
    /// <summary>Giorni scoperti, anomalie o totale del mese che non torna: il dipendente va guardato.</summary>
    public bool NeedsChecking => Gaps > 0 || DifferenceMinutes != 0 || Days.Any(day => day.Alert is not null);
}

/// <summary>Una voce della legenda dei colori.</summary>
internal sealed record CalendarLegendEntry(string Text, int ColorIndex, bool IsPresence);

/// <summary>Un giorno segnalato di un dipendente, per scorrere le anomalie una a una.</summary>
internal sealed record CalendarAlertDay(string EmployeeName, DateTime Date, AlertSeverity Severity);

/// <summary>
/// Il calendario del mese come dati: una riga per dipendente e un giorno per colonna, con il tipo di ogni giorno, i totali,
/// le anomalie e la legenda. Senza finestre né disegno, così si verifica con un test; la nuova interfaccia lo disegna e ne
/// mostra i dettagli. Le regole (festivi, sabato, rapporto di lavoro, giorni scoperti) sono quelle del riepilogo mensile.
/// </summary>
internal sealed class CalendarModel
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    private readonly GenerationOptions _options;
    private readonly IReadOnlyDictionary<string, EmployeeProfile> _profiles;
    private readonly Dictionary<string, Dictionary<DateTime, List<DailyMovement>>> _byEmployee = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly Dictionary<string, string> _codes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _colors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<DateTime, List<CalendarAlert>>> _alerts = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly Dictionary<(string Name, int Year, int Month), int> _differences = new();
    private readonly Dictionary<string, string> _codeOf = new(StringComparer.CurrentCultureIgnoreCase);

    private CalendarModel(
        IReadOnlyList<DailyMovement> movements,
        GenerationOptions options,
        IReadOnlyDictionary<string, EmployeeProfile>? profiles,
        DateTime? periodStart,
        DateTime? periodEnd)
    {
        _options = options;
        _profiles = profiles ?? new Dictionary<string, EmployeeProfile>();
        foreach (var movement in movements)
        {
            if (!_byEmployee.TryGetValue(movement.EmployeeName, out var days))
            {
                days = new Dictionary<DateTime, List<DailyMovement>>();
                _byEmployee[movement.EmployeeName] = days;
                _codeOf[movement.EmployeeName] = movement.EmployeeCode;
            }
            if (!days.TryGetValue(movement.Date.Date, out var list))
            {
                list = new List<DailyMovement>();
                days[movement.Date.Date] = list;
            }
            list.Add(movement);
            if (!AttendanceMovementGenerator.IsPresence(movement) && !_colors.ContainsKey(movement.ReasonCode))
            {
                _colors[movement.ReasonCode] = _colors.Count % AbsencePalette.Colors.Length;
                _codes[movement.ReasonCode] = movement.Policy;
            }
        }
        Employees = _byEmployee.Keys.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        Months = movements
            .Select(movement => (movement.Date.Year, movement.Date.Month))
            .Distinct()
            .OrderBy(month => month)
            .ToArray();
        foreach (var alert in CalendarAlerts.Find(movements, options, _profiles))
        {
            if (!_alerts.TryGetValue(alert.EmployeeName, out var days))
            {
                days = new Dictionary<DateTime, List<CalendarAlert>>();
                _alerts[alert.EmployeeName] = days;
            }
            if (!days.TryGetValue(alert.Date, out var list))
            {
                list = new List<CalendarAlert>();
                days[alert.Date] = list;
            }
            list.Add(alert);
        }
        AlertDays = _alerts
            .SelectMany(employee => employee.Value.Select(day => new CalendarAlertDay(
                employee.Key,
                day.Key,
                day.Value.Max(alert => alert.Severity))))
            .OrderByDescending(day => day.Severity)
            .ThenBy(day => day.Date)
            .ThenBy(day => day.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        foreach (var row in MonthlySummary.Build(movements, options, _profiles, periodStart, periodEnd))
        {
            if (row.ExpectedMinutes is { } expected && row.TotalMinutes != expected)
            {
                _differences[(row.EmployeeName, row.Month.Year, row.Month.Month)] = row.TotalMinutes - expected;
            }
        }
    }

    public static CalendarModel Create(
        IReadOnlyList<DailyMovement> movements,
        GenerationOptions options,
        IReadOnlyDictionary<string, EmployeeProfile>? profiles,
        DateTime? periodStart = null,
        DateTime? periodEnd = null) => new(movements, options, profiles, periodStart, periodEnd);

    /// <summary>I dipendenti in ordine di nome.</summary>
    public IReadOnlyList<string> Employees { get; }

    /// <summary>I mesi toccati dai movimenti, in ordine di tempo.</summary>
    public IReadOnlyList<(int Year, int Month)> Months { get; }

    /// <summary>Tutti i giorni segnalati, prima quelli da controllare.</summary>
    public IReadOnlyList<CalendarAlertDay> AlertDays { get; }

    public bool IsEmpty => Employees.Count == 0;

    public bool FillsPresence => _options.FillPresence;

    /// <summary>Giorni con ore oltre la giornata: sono quelli da controllare davvero.</summary>
    public int CheckDayCount => AlertDays.Count(day => day.Severity == AlertSeverity.Check);

    /// <summary>Giorni insoliti ma spesso legittimi (sabato lavorato, più giustificativi).</summary>
    public int NoticeDayCount => AlertDays.Count(day => day.Severity == AlertSeverity.Notice);

    /// <summary>Quanti totali del mese sono diversi dalle ore teoriche.</summary>
    public int DifferenceCount => _differences.Count;

    /// <summary>La legenda dei colori: presenza, ogni giustificativo comparso e, se serve, i giorni scoperti.</summary>
    public IReadOnlyList<CalendarLegendEntry> Legend
    {
        get
        {
            var entries = new List<CalendarLegendEntry> { new("Presenza", -1, true) };
            foreach (var code in _colors)
            {
                entries.Add(new CalendarLegendEntry(
                    _codes.TryGetValue(code.Key, out var policy) && policy.Length > 0 ? $"{code.Key} · {policy}" : code.Key,
                    code.Value,
                    false));
            }
            return entries;
        }
    }

    /// <summary>Le righe di un mese, filtrate per nome e, volendo, solo i dipendenti da guardare.</summary>
    public IReadOnlyList<CalendarRow> RowsFor(int year, int month, string? search = null, bool needsCheckingOnly = false)
    {
        var rows = new List<CalendarRow>();
        var text = (search ?? string.Empty).Trim();
        foreach (var name in Employees)
        {
            if (text.Length > 0 && name.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) < 0)
            {
                continue;
            }
            var row = BuildRow(name, year, month);
            if (needsCheckingOnly && !row.NeedsChecking)
            {
                continue;
            }
            rows.Add(row);
        }
        return rows;
    }

    private CalendarRow BuildRow(string name, int year, int month)
    {
        var profile = _profiles.TryGetValue(name, out var found) ? found : null;
        var days = _byEmployee[name];
        _alerts.TryGetValue(name, out var alerts);
        var result = new List<CalendarDay>(DateTime.DaysInMonth(year, month));
        var minutes = 0;
        var gaps = 0;
        for (var number = 1; number <= DateTime.DaysInMonth(year, month); number++)
        {
            var date = new DateTime(year, month, number);
            AlertSeverity? severity = alerts is not null && alerts.TryGetValue(date, out var list) ? list.Max(alert => alert.Severity) : null;
            if (days.TryGetValue(date, out var items))
            {
                var absences = items.Where(movement => !AttendanceMovementGenerator.IsPresence(movement)).ToArray();
                var presence = items.Where(AttendanceMovementGenerator.IsPresence).ToArray();
                var absenceMinutes = absences.Sum(movement => (movement.Hours * 60) + movement.Minutes);
                var presenceMinutes = presence.Sum(movement => (movement.Hours * 60) + movement.Minutes);
                minutes += absenceMinutes + presenceMinutes;
                var kind = absences.Length > 0 && presence.Length > 0
                    ? CalendarCellKind.Mixed
                    : absences.Length > 0 ? CalendarCellKind.Absence : CalendarCellKind.Presence;
                var code = absences.Length > 0 ? absences[0].ReasonCode : null;
                result.Add(new CalendarDay(date, kind, presenceMinutes, absenceMinutes, code, code is not null && _colors.TryGetValue(code, out var color) ? color : 0, severity));
            }
            else if (IsWorkingDay(date, profile))
            {
                if (_options.FillPresence)
                {
                    gaps++;
                    result.Add(new CalendarDay(date, CalendarCellKind.Gap, 0, 0, null, 0, severity));
                }
                else
                {
                    result.Add(new CalendarDay(date, CalendarCellKind.Empty, 0, 0, null, 0, severity));
                }
            }
            else
            {
                result.Add(new CalendarDay(date, CalendarCellKind.NonWorking, 0, 0, null, 0, severity));
            }
        }
        _differences.TryGetValue((name, year, month), out var difference);
        return new CalendarRow(name, _codeOf.TryGetValue(name, out var employeeCode) ? employeeCode : string.Empty, result, minutes, gaps, difference);
    }

    private bool IsWorkingDay(DateTime day, EmployeeProfile? profile) =>
        day.DayOfWeek != DayOfWeek.Sunday &&
        (day.DayOfWeek != DayOfWeek.Saturday || _options.IncludeSaturdays) &&
        !ItalianHolidays.Contains(day, _options.ExtraHolidays ?? []) &&
        (profile is null || profile.Covers(day));

    /// <summary>Quanti dipendenti hanno la presenza e quanti l'assenza in un giorno (il piè del calendario).</summary>
    public static (int Present, int Absent) CountsOn(IReadOnlyList<CalendarRow> rows, int dayIndex)
    {
        var present = 0;
        var absent = 0;
        foreach (var row in rows)
        {
            if (dayIndex < 0 || dayIndex >= row.Days.Count)
            {
                continue;
            }
            switch (row.Days[dayIndex].Kind)
            {
                case CalendarCellKind.Presence:
                    present++;
                    break;
                case CalendarCellKind.Absence:
                    absent++;
                    break;
                case CalendarCellKind.Mixed:
                    present++;
                    absent++;
                    break;
            }
        }
        return (present, absent);
    }

    /// <summary>Il testo mostrato passando sul giorno: movimenti, anomalie e, se non c'è nulla, perché.</summary>
    public string Describe(string name, DateTime date)
    {
        var lines = new List<string> { $"{name} · {date.ToString("dddd d MMMM yyyy", Italian)}" };
        if (_byEmployee.TryGetValue(name, out var days) && days.TryGetValue(date.Date, out var items))
        {
            foreach (var movement in items)
            {
                var minutes = (movement.Hours * 60) + movement.Minutes;
                lines.Add($"{movement.ReasonCode} · {movement.Policy}: {HoursText(minutes)}");
            }
        }
        else
        {
            var profile = _profiles.TryGetValue(name, out var found) ? found : null;
            lines.Add(!IsWorkingDay(date, profile)
                ? "Giorno non lavorativo."
                : _options.FillPresence ? "Giorno lavorativo senza movimenti." : "Nessun movimento.");
        }
        if (_alerts.TryGetValue(name, out var alerts) && alerts.TryGetValue(date.Date, out var list))
        {
            lines.AddRange(list.Select(alert => (alert.Severity == AlertSeverity.Check ? "⚠ " : "• ") + alert.Text));
        }
        return string.Join(Environment.NewLine, lines);
    }

    public static string HoursText(int minutes) => minutes % 60 == 0
        ? $"{minutes / 60}h"
        : $"{minutes / 60}h {minutes % 60:D2}m";

    /// <summary>Ore per la cella: «8», «7,5».</summary>
    public static string CellHours(int minutes) => minutes % 60 == 0
        ? (minutes / 60).ToString(CultureInfo.InvariantCulture)
        : (minutes / 60m).ToString("0.#", Italian);

    public static string MonthName(int year, int month)
    {
        var text = new DateTime(year, month, 1).ToString("MMMM yyyy", Italian);
        return char.ToUpper(text[0], Italian) + text[1..];
    }
}
