using System.Globalization;
using System.Text;

namespace PresenzeXmlConverter;

/// <summary>Riepilogo di un mese per un dipendente: ore registrate, ore teoriche e giorni scoperti.</summary>
internal sealed record MonthlySummaryRow(
    string EmployeeName,
    string EmployeeCode,
    DateTime Month,
    int PresenceMinutes,
    int AbsenceMinutes,
    int? ExpectedMinutes,
    int? UncoveredDays,
    IReadOnlyList<(string Code, string Policy, int Minutes)> Absences)
{
    public int TotalMinutes => PresenceMinutes + AbsenceMinutes;
}

/// <summary>
/// Totali mese per mese di ogni dipendente, pronti da aprire in Excel. Le ore teoriche e i giorni
/// scoperti hanno senso solo con le presenze automatiche accese (altrimenti nel file ci sono le sole
/// assenze) e si calcolano come nel controllo delle ore del mese: giorni lavorativi senza festivi,
/// dentro il periodo del foglio e il rapporto di lavoro del dipendente.
/// </summary>
internal static class MonthlySummary
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    public static IReadOnlyList<MonthlySummaryRow> Build(
        IReadOnlyList<DailyMovement> movements,
        GenerationOptions options,
        IReadOnlyDictionary<string, EmployeeProfile>? profiles,
        DateTime? periodStart,
        DateTime? periodEnd)
    {
        var rows = new List<MonthlySummaryRow>();
        if (movements.Count == 0)
        {
            return rows;
        }

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
                int? expected = null;
                int? uncovered = null;
                if (options.FillPresence)
                {
                    var covered = month.Select(movement => movement.Date.Date).ToHashSet();
                    var expectedMinutes = 0;
                    var gaps = 0;
                    for (var day = monthStart < from ? from : monthStart; day <= (monthEnd > to ? to : monthEnd); day = day.AddDays(1))
                    {
                        if (day.DayOfWeek == DayOfWeek.Sunday ||
                            (day.DayOfWeek == DayOfWeek.Saturday && !options.IncludeSaturdays) ||
                            ItalianHolidays.Contains(day, extraHolidays) ||
                            (profile is not null && !profile.Covers(day)))
                        {
                            continue;
                        }
                        expectedMinutes += dayMinutes;
                        if (!covered.Contains(day))
                        {
                            gaps++;
                        }
                    }
                    expected = expectedMinutes;
                    uncovered = gaps;
                }

                var absences = month
                    .Where(movement => !AttendanceMovementGenerator.IsPresence(movement))
                    .GroupBy(movement => movement.ReasonCode, StringComparer.OrdinalIgnoreCase)
                    .Select(group => (
                        Code: group.Key,
                        Policy: group.First().Policy,
                        Minutes: group.Sum(movement => (movement.Hours * 60) + movement.Minutes)))
                    .OrderBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                rows.Add(new MonthlySummaryRow(
                    name,
                    employee.Key,
                    monthStart,
                    month.Where(AttendanceMovementGenerator.IsPresence).Sum(movement => (movement.Hours * 60) + movement.Minutes),
                    absences.Sum(item => item.Minutes),
                    expected,
                    uncovered,
                    absences));
            }
        }
        return rows;
    }

    /// <summary>CSV per Excel italiano: separatore «;», virgola decimale, UTF-8 con BOM dal chiamante.</summary>
    public static string ToCsv(IReadOnlyList<MonthlySummaryRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(
            ';',
            "Dipendente", "Codice", "Mese", "Ore presenza", "Ore assenza", "Ore totali",
            "Ore teoriche", "Differenza", "Giorni senza movimenti", "Dettaglio assenze"));
        foreach (var row in rows)
        {
            var month = row.Month.ToString("MMMM yyyy", Italian);
            var detail = string.Join(
                " · ",
                row.Absences.Select(item =>
                    $"{item.Code}{(item.Policy.Length > 0 ? $" ({item.Policy})" : string.Empty)}: {Hours(item.Minutes)}"));
            builder.AppendLine(string.Join(
                ';',
                Field(row.EmployeeName),
                Field(row.EmployeeCode),
                Field(char.ToUpper(month[0], Italian) + month[1..]),
                Hours(row.PresenceMinutes),
                Hours(row.AbsenceMinutes),
                Hours(row.TotalMinutes),
                row.ExpectedMinutes is { } expected ? Hours(expected) : string.Empty,
                row.ExpectedMinutes is { } theoretical ? Signed(row.TotalMinutes - theoretical) : string.Empty,
                row.UncoveredDays?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                Field(detail)));
        }
        return builder.ToString();
    }

    private static string Hours(int minutes) => (minutes / 60m).ToString("0.##", Italian);

    private static string Signed(int minutes) =>
        minutes > 0 ? "+" + Hours(minutes) : Hours(minutes);

    private static string Field(string value) =>
        value.IndexOfAny([';', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
