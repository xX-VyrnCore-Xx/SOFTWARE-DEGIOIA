using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>
/// Quanto spetta in un anno di un giustificativo (ferie, permessi…) a ogni dipendente dell'azienda: una quantità in
/// giorni o in ore. Il giustificativo si indica col nome o col codice, anche in parte («ferie» vale per «Ferie anno
/// corrente»).
/// </summary>
internal sealed record LeaveRule(string Policy, decimal Amount, bool InDays)
{
    public const decimal MaximumAmount = 5000m;

    public static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Spettanza in minuti, con la giornata standard indicata.</summary>
    public int EntitlementMinutes(decimal hoursPerDay) =>
        decimal.ToInt32(decimal.Round(Amount * (InDays ? hoursPerDay : 1m) * 60m, 0, MidpointRounding.AwayFromZero));

    /// <summary>La regola vale per questo giustificativo (nome o codice, senza distinguere maiuscole)?</summary>
    public bool Matches(string policyName, string reasonCode) =>
        Policy.Length > 0 &&
        (policyName.Contains(Policy, StringComparison.CurrentCultureIgnoreCase) ||
         string.Equals(reasonCode, Policy, StringComparison.OrdinalIgnoreCase));

    /// <summary>«Ferie = 26 giorni», «Permessi = 104 ore».</summary>
    public string Describe() =>
        $"{Policy} = {Amount.ToString("0.##", CultureInfo.CurrentCulture)} {(InDays ? "giorni" : "ore")}";

    /// <summary>Le regole scritte una per riga: «nome = quantità [giorni|ore]» (senza unità valgono i giorni).</summary>
    public static bool TryParseLines(string text, out IReadOnlyList<LeaveRule> rules, out string? error)
    {
        var list = new List<LeaveRule>();
        rules = list;
        error = null;
        foreach (var raw in (text ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = raw.IndexOf('=');
            if (separator <= 0 || separator == raw.Length - 1)
            {
                error = $"«{Shorten(raw)}»: scrivi «giustificativo = quantità», ad esempio «Ferie = 26 giorni».";
                return false;
            }
            var policy = raw[..separator].Trim();
            var rest = raw[(separator + 1)..].Trim();
            var inDays = true;
            foreach (var (suffix, days) in Units)
            {
                if (rest.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    inDays = days;
                    rest = rest[..^suffix.Length].Trim();
                    break;
                }
            }
            if (policy.Length == 0 || policy.Length > 60 ||
                !decimal.TryParse(rest.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) ||
                amount <= 0m || amount > MaximumAmount)
            {
                error = $"«{Shorten(raw)}»: la quantità deve essere un numero maggiore di zero (al più {MaximumAmount:0}), con «giorni» oppure «ore».";
                return false;
            }
            if (list.Any(rule => string.Equals(rule.Policy, policy, StringComparison.CurrentCultureIgnoreCase)))
            {
                error = $"«{policy}» è scritto due volte.";
                return false;
            }
            list.Add(new LeaveRule(policy, amount, inDays));
        }
        if (list.Count > 20)
        {
            error = "Al più 20 giustificativi per azienda.";
            return false;
        }
        return true;
    }

    /// <summary>Le regole come testo, una per riga (per la finestra).</summary>
    public static string ToLines(IReadOnlyList<LeaveRule>? rules) =>
        rules is null ? string.Empty : string.Join(Environment.NewLine, rules.Select(rule => rule.Describe()));

    public static string ToJson(IReadOnlyList<LeaveRule> rules) => System.Text.Json.JsonSerializer.Serialize(rules, JsonOptions);

    /// <summary>Legge le regole salvate; testo vuoto o rovinato vale «nessuna».</summary>
    public static IReadOnlyList<LeaveRule> TryParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }
        try
        {
            return (System.Text.Json.JsonSerializer.Deserialize<List<LeaveRule>>(json, JsonOptions) ?? [])
                .Where(rule => !string.IsNullOrWhiteSpace(rule.Policy) && rule.Amount > 0m && rule.Amount <= MaximumAmount)
                .ToArray();
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private static readonly (string Suffix, bool Days)[] Units =
    [
        ("giorni", true), ("giorno", true), ("gg", true), ("g", true),
        ("ore", false), ("ora", false), ("h", false)
    ];

    private static string Shorten(string text) => text.Length <= 40 ? text : text[..40] + "…";
}

/// <summary>Le ore di un giustificativo usate da un dipendente in un mese (si conservano a ogni esportazione).</summary>
internal sealed record AbsenceTotal(
    string CompanyCode,
    DateTime Month,
    string EmployeeCode,
    string EmployeeName,
    string Policy,
    string ReasonCode,
    int Minutes);

/// <summary>Il saldo di un dipendente per un giustificativo in un anno.</summary>
internal sealed record LeaveBalanceRow(
    string EmployeeName,
    string EmployeeCode,
    LeaveRule Rule,
    int EntitlementMinutes,
    int UsedMinutes)
{
    /// <summary>Minuti rimasti (negativi se si è usato più di quanto spetta).</summary>
    public int RemainingMinutes => EntitlementMinutes - UsedMinutes;

    public LeaveStatus Status => RemainingMinutes < 0
        ? LeaveStatus.Over
        : RemainingMinutes * 10 <= EntitlementMinutes ? LeaveStatus.Low : LeaveStatus.Ok;
}

internal enum LeaveStatus
{
    Ok,
    Low,
    Over
}

/// <summary>
/// Saldo di ferie e permessi: quanto spetta, quanto è stato usato nell'anno e quanto resta, per dipendente. Solo calcolo
/// sui totali conservati e sul riepilogo del file in preparazione, senza finestre né database.
/// </summary>
internal static class LeaveBalance
{
    /// <summary>Le ore usate dal riepilogo mensile, una riga per dipendente, mese e giustificativo.</summary>
    public static IReadOnlyList<AbsenceTotal> FromSummary(string companyCode, IReadOnlyList<MonthlySummaryRow> rows) =>
        rows
            .Where(row => row.EmployeeCode.Length > 0)
            .SelectMany(row => row.Absences.Select(absence => new AbsenceTotal(
                companyCode.Trim(),
                new DateTime(row.Month.Year, row.Month.Month, 1),
                row.EmployeeCode,
                row.EmployeeName,
                absence.Policy,
                absence.Code,
                absence.Minutes)))
            .ToArray();

    /// <summary>Il saldo dell'anno per ogni dipendente che ha usato qualcosa e per ogni regola, in ordine di nome.</summary>
    public static IReadOnlyList<LeaveBalanceRow> Build(
        IReadOnlyList<LeaveRule> rules,
        decimal hoursPerDay,
        IReadOnlyList<AbsenceTotal> usage,
        int year)
    {
        if (rules.Count == 0)
        {
            return [];
        }
        if (hoursPerDay <= 0m || hoursPerDay > 24m)
        {
            hoursPerDay = 8m;
        }
        var inYear = usage.Where(item => item.Month.Year == year).ToList();
        var rows = new List<LeaveBalanceRow>();
        foreach (var employee in inYear
                     .GroupBy(item => item.EmployeeCode, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.First().EmployeeName, StringComparer.CurrentCultureIgnoreCase))
        {
            foreach (var rule in rules)
            {
                var used = employee.Where(item => rule.Matches(item.Policy, item.ReasonCode)).Sum(item => item.Minutes);
                if (used == 0)
                {
                    continue;
                }
                rows.Add(new LeaveBalanceRow(employee.First().EmployeeName, employee.Key, rule, rule.EntitlementMinutes(hoursPerDay), used));
            }
        }
        return rows;
    }

    /// <summary>Gli anni per cui ci sono ore usate, dal più recente.</summary>
    public static IReadOnlyList<int> Years(IReadOnlyList<AbsenceTotal> usage) =>
        usage.Select(item => item.Month.Year).Distinct().OrderByDescending(year => year).ToArray();

    /// <summary>
    /// Controllo sul file in preparazione: al saldo già consegnato (esclusi i mesi che il file rifà) si aggiungono le ore
    /// del file; chi supera la spettanza, o ci arriva a un decimo, viene segnalato.
    /// </summary>
    public static IReadOnlyList<MovementFinding> CheckPreview(
        IReadOnlyList<LeaveRule> rules,
        decimal hoursPerDay,
        IReadOnlyList<AbsenceTotal> saved,
        IReadOnlyList<AbsenceTotal> preview)
    {
        if (rules.Count == 0 || preview.Count == 0)
        {
            return [];
        }
        var previewMonths = preview.Select(item => (item.Month.Year, item.Month.Month)).ToHashSet();
        var combined = saved
            .Where(item => !previewMonths.Contains((item.Month.Year, item.Month.Month)))
            .Concat(preview)
            .ToList();
        var findings = new List<MovementFinding>();
        foreach (var year in preview.Select(item => item.Month.Year).Distinct().OrderBy(year => year))
        {
            foreach (var row in Build(rules, hoursPerDay, combined, year))
            {
                if (row.Status == LeaveStatus.Ok)
                {
                    continue;
                }
                var name = $"{row.EmployeeName} ({row.EmployeeCode})";
                findings.Add(row.Status == LeaveStatus.Over
                    ? new MovementFinding(
                        MovementSeverity.Warning,
                        $"{name}: {row.Rule.Policy} {year}: usate {Hours(row.UsedMinutes)} su {Hours(row.EntitlementMinutes)} spettanti ({Signed(row.RemainingMinutes)}).")
                    : new MovementFinding(
                        MovementSeverity.Note,
                        $"{name}: {row.Rule.Policy} {year}: restano {Hours(row.RemainingMinutes)} su {Hours(row.EntitlementMinutes)} spettanti."));
                if (findings.Count >= 40)
                {
                    return findings;
                }
            }
        }
        return findings;
    }

    public static string Hours(int minutes) => (minutes / 60m).ToString("0.##", CultureInfo.GetCultureInfo("it-IT")) + " h";

    public static string Signed(int minutes) => minutes == 0 ? "0 h" : (minutes > 0 ? "+" : "−") + Hours(Math.Abs(minutes));
}
