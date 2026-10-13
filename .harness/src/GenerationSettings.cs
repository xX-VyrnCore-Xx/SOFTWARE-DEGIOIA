using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>
/// Impostazioni di conversione scelte dall'utente. Tutto quello che si legge da
/// una preferenza salvata o da una casella di testo passa di qui, così un valore
/// scritto male non può mai far fallire la generazione: viene solo corretto.
/// </summary>
internal static class GenerationSettings
{
    public const string HoursPerDaySetting = "generation-hours-per-day";
    public const string AutoDetectSetting = "generation-auto-detect";
    public const string IncludeSaturdaysSetting = "generation-include-saturdays";
    public const string ExtraHolidaysSetting = "generation-extra-holidays";

    public const decimal MinimumHoursPerDay = 1m;
    public const decimal MaximumHoursPerDay = 24m;
    public const decimal DefaultHoursPerDay = 8m;

    /// <summary>
    /// Ore di una giornata standard. Accetta sia "8" sia "7,5"/"7.5"; un valore
    /// fuori intervallo viene sostituito con il default invece di bloccare l'utente.
    /// </summary>
    public static bool TryParseHoursPerDay(string? text, out decimal hours)
    {
        hours = DefaultHoursPerDay;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        var normalized = text.Trim().Replace(',', '.');
        if (!decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return false;
        }
        if (parsed < MinimumHoursPerDay || parsed > MaximumHoursPerDay)
        {
            return false;
        }
        hours = decimal.Round(parsed, 2, MidpointRounding.AwayFromZero);
        return true;
    }

    public static decimal ParseHoursPerDay(string? text) =>
        TryParseHoursPerDay(text, out var hours) ? hours : DefaultHoursPerDay;

    /// <summary>
    /// Festività aggiuntive (chiusure aziendali) scritte una per riga o separate
    /// da virgola/punto e virgola. Sono accettate le forme dd/MM, dd/MM/yyyy,
    /// dd-MM-yyyy, dd.MM.yyyy e yyyy-MM-dd. Le voci non riconoscibili vengono
    /// ignorate: un refuso non impedisce la conversione.
    /// </summary>
    public static IReadOnlyList<DateTime> ParseHolidays(string? text, int referenceYear) =>
        ParseHolidaysDetailed(text, referenceYear, out _);

    /// <summary>
    /// Come <see cref="ParseHolidays"/>, ma restituisce anche le voci non
    /// riconosciute: il dialogo può così dire all'utente quali date sono state
    /// ignorate invece di scartarle in silenzio.
    /// </summary>
    public static IReadOnlyList<DateTime> ParseHolidaysDetailed(
        string? text,
        int referenceYear,
        out IReadOnlyList<string> ignored)
    {
        var days = new SortedSet<DateTime>();
        var notUnderstood = new List<string>();
        if (!string.IsNullOrWhiteSpace(text))
        {
            foreach (var raw in text.Split(
                         new[] { ',', ';', '\n', '\r', '|' },
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var day = ParseSingleHoliday(raw, referenceYear);
                if (day is null)
                {
                    notUnderstood.Add(raw);
                }
                else
                {
                    days.Add(day.Value.Date);
                }
            }
        }
        ignored = notUnderstood;
        return days.ToArray();
    }

    private static DateTime? ParseSingleHoliday(string raw, int referenceYear)
    {
        var value = raw.Trim();
        if (value.Length == 0)
        {
            return null;
        }

        string[] formats =
        [
            "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd",
            "dd/MM/yyyy", "dd/MM/yy", "dd-MM-yyyy", "dd.MM.yyyy",
            "dd/MM", "dd-MM", "dd.MM"
        ];
        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(
                    value,
                    format,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                // "dd/MM" e "dd-MM" non portano l'anno: si usa quello del periodo.
                if (!format.Contains("yy", StringComparison.Ordinal))
                {
                    var year = referenceYear > 0 ? referenceYear : DateTime.Today.Year;
                    if (DateTime.DaysInMonth(year, parsed.Month) < parsed.Day)
                    {
                        return null;
                    }
                    parsed = new DateTime(year, parsed.Month, parsed.Day);
                }
                return parsed;
            }
        }
        return null;
    }

    /// <summary>
    /// Riscrive le festività in modo compatto: senza anno quando l'anno coincide
    /// con quello del periodo, con anno quando è diverso.
    /// </summary>
    public static string FormatHolidays(IEnumerable<DateTime>? days, int referenceYear)
    {
        if (days is null)
        {
            return string.Empty;
        }
        return string.Join(
            ", ",
            days.Select(day => day.Date)
                .Distinct()
                .OrderBy(day => day)
                .Select(day => day.Year == referenceYear
                    ? day.ToString("dd/MM", CultureInfo.InvariantCulture)
                    : day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// Due insiemi di impostazioni sono equivalenti quando ore, sabati e giorni di
    /// chiusura coincidono: i record con una lista dentro non si confrontano da soli.
    /// </summary>
    public static bool AreEquivalent(GenerationOptions first, GenerationOptions second) =>
        first.HoursPerDay == second.HoursPerDay &&
        first.IncludeSaturdays == second.IncludeSaturdays &&
        first.AutoDetect == second.AutoDetect &&
        (first.ExtraHolidays ?? [])
            .Select(day => day.Date)
            .OrderBy(day => day)
            .SequenceEqual((second.ExtraHolidays ?? []).Select(day => day.Date).OrderBy(day => day));

    /// <summary>Impostazioni lette dalle preferenze locali.</summary>
    public static GenerationOptions Read(LocalDatabase database)
    {
        var settings = database.GetSettings(
            HoursPerDaySetting,
            IncludeSaturdaysSetting,
            AutoDetectSetting,
            ExtraHolidaysSetting);
        var hours = ParseHoursPerDay(settings[HoursPerDaySetting]);
        var includeSaturdays = string.Equals(
            settings[IncludeSaturdaysSetting],
            "true",
            StringComparison.OrdinalIgnoreCase);
        var referenceYear = DateTime.Today.Year;
        return new GenerationOptions(
            hours,
            includeSaturdays,
            ParseHolidays(settings[ExtraHolidaysSetting], referenceYear),
            // Il rilevamento è attivo salvo scelta contraria.
            !string.Equals(settings[AutoDetectSetting], "false", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Salva le impostazioni scelte, normalizzando i valori.</summary>
    public static void Save(LocalDatabase database, GenerationOptions options, int referenceYear)
    {
        database.SaveSetting(
            HoursPerDaySetting,
            options.HoursPerDay.ToString(CultureInfo.InvariantCulture));
        database.SaveSetting(
            IncludeSaturdaysSetting,
            options.IncludeSaturdays ? "true" : "false");
        database.SaveSetting(
            AutoDetectSetting,
            options.AutoDetect ? "true" : "false");
        database.SaveSetting(
            ExtraHolidaysSetting,
            FormatHolidays(options.ExtraHolidays, referenceYear));
    }

    /// <summary>Riepilogo in una riga delle regole di conversione in uso.</summary>
    public static string Describe(GenerationOptions options)
    {
        var saturday = options.IncludeSaturdays
            ? "sabato lavorativo"
            : options.AutoDetect ? "sabato automatico" : "sabato escluso";
        return $"{options.HoursPerDay:0.##} h/giorno · {saturday}";
    }
}
