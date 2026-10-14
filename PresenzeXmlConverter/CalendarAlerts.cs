namespace PresenzeXmlConverter;

internal enum AlertSeverity
{
    /// <summary>Insolito ma spesso legittimo (sabato lavorato, ferie e permesso nello stesso giorno).</summary>
    Notice = 1,

    /// <summary>Da controllare: le ore del giorno superano la giornata.</summary>
    Check = 2
}

/// <summary>Un giorno di un dipendente che il calendario segnala all'attenzione.</summary>
internal sealed record CalendarAlert(string EmployeeName, DateTime Date, AlertSeverity Severity, string Text);

/// <summary>
/// Anomalie dei movimenti generati, giorno per giorno, da mostrare nel calendario: restano visibili
/// finché l'utente guarda l'anteprima, così non si dimentica di controllarle. Sono controlli sui
/// movimenti e non sul foglio (le richieste escluse e le conversioni ore → giorni stanno nei Controlli).
/// </summary>
internal static class CalendarAlerts
{
    public static IReadOnlyList<CalendarAlert> Find(
        IReadOnlyList<DailyMovement> movements,
        GenerationOptions options,
        IReadOnlyDictionary<string, EmployeeProfile>? profiles)
    {
        var alerts = new List<CalendarAlert>();
        var extraHolidays = options.ExtraHolidays ?? [];
        foreach (var employee in movements.GroupBy(movement => movement.EmployeeName, StringComparer.CurrentCultureIgnoreCase))
        {
            var profile = profiles is not null && profiles.TryGetValue(employee.Key, out var found) ? found : null;
            var hours = profile?.DailyHours ?? options.HoursPerDay;
            if (hours <= 0m || hours > 24m)
            {
                hours = 8m;
            }
            var capacity = decimal.ToInt32(decimal.Round(hours * 60m, 0, MidpointRounding.AwayFromZero));

            foreach (var day in employee.GroupBy(movement => movement.Date.Date).OrderBy(group => group.Key))
            {
                var total = day.Sum(movement => (movement.Hours * 60) + movement.Minutes);
                if (total > capacity)
                {
                    alerts.Add(new CalendarAlert(
                        employee.Key,
                        day.Key,
                        AlertSeverity.Check,
                        $"{Format(total)} in un giorno: oltre la giornata di {Format(capacity)}."));
                }

                var codes = day
                    .Where(movement => !AttendanceMovementGenerator.IsPresence(movement))
                    .Select(movement => movement.ReasonCode)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (codes.Length > 1)
                {
                    alerts.Add(new CalendarAlert(
                        employee.Key,
                        day.Key,
                        AlertSeverity.Notice,
                        $"più giustificativi nello stesso giorno ({string.Join(", ", codes)})."));
                }

                var reason = NonWorkingReason(day.Key, profile, options, extraHolidays);
                if (reason is not null)
                {
                    alerts.Add(new CalendarAlert(
                        employee.Key,
                        day.Key,
                        AlertSeverity.Notice,
                        $"movimento in un giorno non lavorativo ({reason})."));
                }
            }
        }
        return alerts;
    }

    private static string? NonWorkingReason(
        DateTime day,
        EmployeeProfile? profile,
        GenerationOptions options,
        IReadOnlyCollection<DateTime> extraHolidays)
    {
        if (day.DayOfWeek == DayOfWeek.Sunday)
        {
            return "domenica";
        }
        if (ItalianHolidays.Contains(day, extraHolidays))
        {
            return "festivo o chiusura";
        }
        if (day.DayOfWeek == DayOfWeek.Saturday && !options.IncludeSaturdays)
        {
            return "sabato";
        }
        return profile is not null && !profile.Covers(day) ? "fuori dal rapporto di lavoro" : null;
    }

    private static string Format(int minutes) => minutes % 60 == 0
        ? $"{minutes / 60}h"
        : $"{minutes / 60}h {minutes % 60:00}m";
}
