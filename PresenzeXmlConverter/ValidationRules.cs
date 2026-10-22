using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>
/// Soglie dei controlli di un'azienda: oltre quanti giorni una malattia o un'assenza va controllata, quanto può cambiare
/// il mese rispetto all'ultima consegna, il tetto di ore al mese per dipendente e chi deve per forza comparire nel
/// foglio. Valgono solo per quell'azienda; senza regole proprie restano quelle di serie (le stesse di prima).
/// </summary>
internal sealed record ValidationRules(
    int LongIllnessDays = ValidationRules.DefaultLongIllnessDays,
    int LongAbsenceDays = 0,
    int ChangePercent = ValidationRules.DefaultChangePercent,
    bool CheckOverlaps = true,
    decimal MaxMonthHours = 0m,
    string ExpectedEmployees = "")
{
    public const int DefaultLongIllnessDays = 30;
    public const int DefaultChangePercent = 30;
    public const int MaximumDays = 366;
    public const int MaximumPercent = 500;
    public const decimal MaximumMonthHoursLimit = 744m;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Le soglie di serie, usate dalle aziende che non ne hanno di proprie.</summary>
    public static ValidationRules Default { get; } = new();

    public bool IsDefault => this == Default;

    /// <summary>Nomi attesi nel foglio, uno per riga (o separati da «;»), senza righe vuote né doppioni.</summary>
    public IReadOnlyList<string> ExpectedList => (ExpectedEmployees ?? string.Empty)
        .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    /// <summary>Le soglie sono dentro i limiti ragionevoli?</summary>
    public bool IsValid =>
        LongIllnessDays is >= 1 and <= MaximumDays &&
        LongAbsenceDays is >= 0 and <= MaximumDays &&
        ChangePercent is >= 5 and <= MaximumPercent &&
        MaxMonthHours >= 0m && MaxMonthHours <= MaximumMonthHoursLimit;

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Legge le soglie salvate; testo vuoto, rovinato o fuori misura vale «di serie».</summary>
    public static ValidationRules? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            var rules = System.Text.Json.JsonSerializer.Deserialize<ValidationRules>(json, JsonOptions);
            return rules is not null && rules.IsValid ? rules with { ExpectedEmployees = rules.ExpectedEmployees ?? string.Empty } : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Le soglie dell'azienda (quelle di serie se non ne ha o se il codice non c'è).</summary>
    public static ValidationRules For(LocalDatabase database, string companyCode)
    {
        var code = (companyCode ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return Default;
        }
        try
        {
            return database.GetCompanies()
                .FirstOrDefault(company => string.Equals(company.Code, code, StringComparison.OrdinalIgnoreCase))
                ?.Checks ?? Default;
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            return Default;
        }
    }

    /// <summary>Riepilogo a parole delle soglie diverse da quelle di serie (per l'elenco delle aziende).</summary>
    public string Describe()
    {
        if (IsDefault)
        {
            return "di serie";
        }
        var parts = new List<string> { $"malattia oltre {LongIllnessDays} g" };
        if (LongAbsenceDays > 0)
        {
            parts.Add($"assenza oltre {LongAbsenceDays} g");
        }
        parts.Add($"variazione {ChangePercent}%");
        if (!CheckOverlaps)
        {
            parts.Add("senza sovrapposizioni");
        }
        if (MaxMonthHours > 0m)
        {
            parts.Add($"tetto {MaxMonthHours.ToString("0.##", CultureInfo.CurrentCulture)} h/mese");
        }
        if (ExpectedList.Count > 0)
        {
            parts.Add($"{ExpectedList.Count} attesi");
        }
        return string.Join(" · ", parts);
    }
}
