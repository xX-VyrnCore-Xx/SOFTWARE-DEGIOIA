using System.Drawing;

namespace PresenzeXmlConverter;

/// <summary>Colori delle assenze, gli stessi nel calendario e nella dashboard: si assegnano nell'ordine in cui i giustificativi compaiono.</summary>
internal static class AbsencePalette
{
    public static readonly (Color Fill, Color Ink)[] Colors =
    [
        (Color.FromArgb(207, 228, 255), Color.FromArgb(0, 88, 190)),
        (Color.FromArgb(255, 232, 200), Color.FromArgb(150, 80, 0)),
        (Color.FromArgb(236, 222, 255), Color.FromArgb(100, 50, 160)),
        (Color.FromArgb(255, 218, 216), Color.FromArgb(170, 30, 20)),
        (Color.FromArgb(205, 240, 246), Color.FromArgb(0, 96, 116)),
        (Color.FromArgb(240, 236, 200), Color.FromArgb(110, 100, 20))
    ];

    public static (Color Fill, Color Ink) For(int index) => Colors[Math.Abs(index) % Colors.Length];
}

/// <summary>Ore di un giustificativo di assenza nell'anteprima, con il suo colore.</summary>
internal sealed record AbsenceShare(string Code, string Policy, int Minutes, int ColorIndex);

/// <summary>
/// Numeri dell'analisi mostrata nella dashboard: quanta presenza e quanta assenza ci sono nell'anteprima,
/// quali giustificativi pesano di più e quanti giorni vanno controllati. Come <see cref="DashboardSummary"/>
/// è un calcolo puro, verificabile con un test.
/// </summary>
internal sealed record DashboardInsights(
    int PresenceMinutes,
    int AbsenceMinutes,
    IReadOnlyList<AbsenceShare> Absences,
    int CheckDays,
    int NoticeDays)
{
    public static DashboardInsights Empty { get; } = new(0, 0, [], 0, 0);

    public bool HasData => PresenceMinutes + AbsenceMinutes > 0;

    /// <summary>Quota di assenza sul totale, da 0 a 1.</summary>
    public double AbsenceRatio => HasData ? (double)AbsenceMinutes / (PresenceMinutes + AbsenceMinutes) : 0;

    /// <summary>Stessi numeri e stesse assenze? (Il confronto dei record non basta: le liste si confrontano per riferimento.)</summary>
    public bool SameAs(DashboardInsights other) =>
        PresenceMinutes == other.PresenceMinutes &&
        AbsenceMinutes == other.AbsenceMinutes &&
        CheckDays == other.CheckDays &&
        NoticeDays == other.NoticeDays &&
        Absences.SequenceEqual(other.Absences);

    public static DashboardInsights From(IReadOnlyList<DailyMovement> movements, IReadOnlyList<CalendarAlert> alerts)
    {
        if (movements.Count == 0)
        {
            return Empty;
        }

        var colorIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var minutesByCode = new Dictionary<string, (string Policy, int Minutes)>(StringComparer.OrdinalIgnoreCase);
        var presence = 0;
        foreach (var movement in movements)
        {
            var minutes = (movement.Hours * 60) + movement.Minutes;
            if (AttendanceMovementGenerator.IsPresence(movement))
            {
                presence += minutes;
                continue;
            }
            if (!colorIndex.ContainsKey(movement.ReasonCode))
            {
                colorIndex[movement.ReasonCode] = colorIndex.Count % AbsencePalette.Colors.Length;
            }
            minutesByCode[movement.ReasonCode] = minutesByCode.TryGetValue(movement.ReasonCode, out var known)
                ? (known.Policy, known.Minutes + minutes)
                : (movement.Policy, minutes);
        }

        var absences = minutesByCode
            .Select(pair => new AbsenceShare(pair.Key, pair.Value.Policy, pair.Value.Minutes, colorIndex[pair.Key]))
            .OrderByDescending(share => share.Minutes)
            .ThenBy(share => share.Code, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var days = alerts
            .GroupBy(alert => (alert.EmployeeName, alert.Date))
            .Select(group => group.Max(alert => alert.Severity))
            .ToArray();
        return new DashboardInsights(
            presence,
            absences.Sum(share => share.Minutes),
            absences,
            days.Count(severity => severity == AlertSeverity.Check),
            days.Count(severity => severity == AlertSeverity.Notice));
    }
}
