using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>Le ore di un dipendente in un mese, come consegnate per un'azienda (si conservano a ogni esportazione).</summary>
internal sealed record MonthlyTotal(
    string CompanyCode,
    DateTime Month,
    string EmployeeCode,
    string EmployeeName,
    int PresenceMinutes,
    int AbsenceMinutes)
{
    public int TotalMinutes => PresenceMinutes + AbsenceMinutes;
}

/// <summary>Il totale di un mese per tutta l'azienda (una colonna del grafico).</summary>
internal sealed record MonthPoint(DateTime Month, int TotalMinutes, int AbsenceMinutes, int Employees);

/// <summary>Il confronto di un dipendente fra l'ultimo mese consegnato e quello prima.</summary>
internal sealed record EmployeeChange(
    string EmployeeName,
    string EmployeeCode,
    DateTime Month,
    int TotalMinutes,
    int PreviousMinutes,
    int AbsenceMinutes,
    int PreviousAbsenceMinutes)
{
    public int Delta => TotalMinutes - PreviousMinutes;

    public int AbsenceDelta => AbsenceMinutes - PreviousAbsenceMinutes;

    /// <summary>Variazione in percentuale sul mese prima; nulla se il mese prima non c'era (o era a zero).</summary>
    public int? Percent => PreviousMinutes <= 0
        ? null
        : (int)Math.Round(Delta * 100.0 / PreviousMinutes, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Andamento mese su mese delle ore consegnate: serie per l'azienda e confronto per dipendente fra l'ultimo mese e
/// quello prima. Solo calcolo sui totali conservati, senza finestre né database, così si verifica con un test.
/// </summary>
internal static class MonthlyTrend
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    public const int MaximumMonthsShown = 12;

    /// <summary>I totali del riepilogo mensile pronti da conservare per l'azienda.</summary>
    public static IReadOnlyList<MonthlyTotal> FromSummary(string companyCode, IReadOnlyList<MonthlySummaryRow> rows) =>
        rows
            .Where(row => row.EmployeeCode.Length > 0)
            .Select(row => new MonthlyTotal(
                companyCode.Trim(),
                new DateTime(row.Month.Year, row.Month.Month, 1),
                row.EmployeeCode,
                row.EmployeeName,
                row.PresenceMinutes,
                row.AbsenceMinutes))
            .ToArray();

    /// <summary>I totali dell'azienda, mese per mese in ordine di tempo (al più gli ultimi <paramref name="maxMonths"/>).</summary>
    public static IReadOnlyList<MonthPoint> Series(IReadOnlyList<MonthlyTotal> totals, int maxMonths = MaximumMonthsShown) =>
        totals
            .GroupBy(item => (item.Month.Year, item.Month.Month))
            .OrderBy(group => group.Key)
            .TakeLast(Math.Max(1, maxMonths))
            .Select(group => new MonthPoint(
                new DateTime(group.Key.Year, group.Key.Month, 1),
                group.Sum(item => item.TotalMinutes),
                group.Sum(item => item.AbsenceMinutes),
                group.Select(item => item.EmployeeCode).Distinct(StringComparer.OrdinalIgnoreCase).Count()))
            .ToArray();

    /// <summary>L'ultimo mese conservato e quello prima (nullo se ce n'è uno solo).</summary>
    public static (DateTime Latest, DateTime? Previous)? LatestMonths(IReadOnlyList<MonthlyTotal> totals)
    {
        var months = totals
            .Select(item => new DateTime(item.Month.Year, item.Month.Month, 1))
            .Distinct()
            .OrderBy(month => month)
            .ToArray();
        return months.Length == 0 ? null : (months[^1], months.Length > 1 ? months[^2] : null);
    }

    /// <summary>Chi ha cambiato di più fra l'ultimo mese e quello prima: prima le differenze più grandi.</summary>
    public static IReadOnlyList<EmployeeChange> Changes(IReadOnlyList<MonthlyTotal> totals)
    {
        if (LatestMonths(totals) is not { } months)
        {
            return [];
        }
        var latest = totals.Where(item => SameMonth(item.Month, months.Latest)).ToList();
        var previous = months.Previous is { } before
            ? totals.Where(item => SameMonth(item.Month, before)).ToDictionary(item => item.EmployeeCode, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, MonthlyTotal>(StringComparer.OrdinalIgnoreCase);
        return latest
            .Select(item =>
            {
                previous.TryGetValue(item.EmployeeCode, out var before);
                return new EmployeeChange(
                    item.EmployeeName,
                    item.EmployeeCode,
                    months.Latest,
                    item.TotalMinutes,
                    before?.TotalMinutes ?? 0,
                    item.AbsenceMinutes,
                    before?.AbsenceMinutes ?? 0);
            })
            .OrderByDescending(change => Math.Abs(change.Delta))
            .ThenBy(change => change.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static bool SameMonth(DateTime first, DateTime second) => first.Year == second.Year && first.Month == second.Month;

    public static string MonthName(DateTime month)
    {
        var text = month.ToString("MMMM yyyy", Italian);
        return char.ToUpper(text[0], Italian) + text[1..];
    }

    public static string ShortMonth(DateTime month) => month.ToString("MMM yy", Italian);

    public static string Hours(int minutes) => (minutes / 60m).ToString("0.##", Italian) + " h";

    public static string SignedHours(int minutes) =>
        minutes == 0 ? "—" : (minutes > 0 ? "+" : "−") + Hours(Math.Abs(minutes));

    public static string PercentText(int? percent) => percent switch
    {
        null => "—",
        0 => "0%",
        > 0 => $"+{percent}%",
        _ => "−" + Math.Abs(percent.Value).ToString(CultureInfo.InvariantCulture) + "%"
    };
}
