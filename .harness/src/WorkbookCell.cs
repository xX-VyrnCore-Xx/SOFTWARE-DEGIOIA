using System.Globalization;
using System.Text.RegularExpressions;

namespace PresenzeXmlConverter;

/// <summary>
/// Una cella del foglio Excel, già ridotta ai tre valori che servono
/// (testo, data, numero) e capace di interpretare le forme "sporche" tipiche
/// degli export: "8h30", "8:30", "1.234,5", "2 giorni", "(4)".
/// </summary>
internal sealed record WorkbookCell(
    int RowNumber,
    string Text,
    DateTime? DateValue,
    decimal? NumberValue)
{
    private static readonly Regex HourMinuteText = new(
        @"^(?<hours>\d{1,3})\s*[hH]\s*(?<minutes>\d{1,2})?(?!\d)$",
        RegexOptions.Compiled);
    private static readonly Regex HourMinuteSpelled = new(
        @"^(?<hours>\d{1,3})\s*[hH]\s*(?<minutes>\d{1,3})\s*(?:min|minuti|m)\b$",
        RegexOptions.Compiled);
    private static readonly Regex ClockText = new(
        @"^(?<hours>\d{1,3})\s*:\s*(?<minutes>\d{1,2})\s*(?::\s*\d{1,2})?$",
        RegexOptions.Compiled);
    private static readonly Regex TrailingHoursText = new(
        @"^(?<value>\d{1,3}(?:[.,]\d{1,2})?)\s*[hH]$",
        RegexOptions.Compiled);
    private static readonly Regex MinutesOnlyText = new(
        @"^(?<minutes>\d{1,4})\s*(?:min|minuti|m)\b$",
        RegexOptions.Compiled);
    private static readonly Regex DayText = new(
        @"^(?<value>\d{1,3}(?:[.,]\d{1,2})?)\s*(?:gg|giorn\w*|d|day|days)\b",
        RegexOptions.Compiled);
    private static readonly Regex ParenthesisedText = new(@"^\((?<value>[^()]+)\)$", RegexOptions.Compiled);

    /// <summary>
    /// Testi che negli export significano "nessun valore" e non devono mai
    /// diventare un errore di conversione.
    /// </summary>
    private static readonly string[] EmptyMarkers =
        ["-", "--", "---", "n.d.", "nd", "n/a", "na", "#n/a", "#value!", "#div/0!", "?", "x"];

    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "d.M.yyyy",
        "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yy", "d/M/yy", "ddMMyyyy",
        "dd/MM/yyyy HH:mm", "dd/MM/yyyy H:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss"
    ];

    public static WorkbookCell Empty(int rowNumber) => new(rowNumber, string.Empty, null, null);

    private static readonly string[] EmptyTexts = [string.Empty, .. EmptyMarkers];

    public bool HasValue =>
        DateValue.HasValue ||
        NumberValue.HasValue ||
        IsMeaningfulText(Text);

    /// <summary>
    /// Il testo è un vero contenuto, non un segnaposto ("-", "n.d.", "#N/A").
    /// </summary>
    public static bool IsMeaningfulText(string? text)
    {
        var value = (text ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return false;
        }
        if (EmptyMarkers.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }
        // Uno zero isolato non identifica una riga di dati (righe di totale vuote).
        return value.Replace(",", ".") is not ("0" or "0.0" or "0.00");
    }

    /// <summary>
    /// Interpreta la cella come data. Prima i tipi nativi di Excel, poi i
    /// formati espliciti (ISO, italiano, con punto o barra) e infine le
    /// culture, così "17/08/2026", "2026-08-17" e "17 ago 2026" funzionano tutti.
    /// </summary>
    public bool TryGetDate(out DateTime date)
    {
        if (DateValue.HasValue)
        {
            date = DateValue.Value;
            return true;
        }

        var value = Text.Trim();
        if (value.Length == 0)
        {
            date = default;
            return false;
        }

        if (DateTime.TryParseExact(
                value,
                DateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out date))
        {
            return true;
        }
        if (DateTime.TryParseExact(
                value,
                ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "yyyy-MM-dd"],
                CultureInfo.GetCultureInfo("it-IT"),
                DateTimeStyles.AllowWhiteSpaces,
                out date))
        {
            return true;
        }
        foreach (var culture in new[] { "it-IT", "en-US", "en-GB" })
        {
            if (DateTime.TryParse(value, CultureInfo.GetCultureInfo(culture), DateTimeStyles.AllowWhiteSpaces, out date))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Interpreta la cella come numero/durata in ore. Riconosce anche le forme
    /// testuali usate nei fogli: "8h30", "8h 30min", "8:30", "30min", "8,5h",
    /// i numeri negativi tra parentesi e i separatori delle migliaia.
    /// </summary>
    public bool TryGetNumber(out decimal number)
    {
        if (NumberValue.HasValue)
        {
            number = NumberValue.Value;
            return true;
        }
        return TryParseNumberText(Text, out number);
    }

    private static bool TryParseNumberText(string text, out decimal number)
    {
        var value = text.Trim();
        if (value.Length == 0 || EmptyTexts.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            number = 0m;
            return false;
        }
        value = value.Replace(" ", string.Empty).Replace("\u00a0", string.Empty);

        var parenthesised = ParenthesisedText.Match(value);
        if (parenthesised.Success)
        {
            // "(4)" negli estratti contabili significa −4.
            if (TryParseNumberText(parenthesised.Groups["value"].Value, out var negative))
            {
                number = -Math.Abs(negative);
                return true;
            }
        }

        var hourMinuteSpelled = HourMinuteSpelled.Match(value);
        if (hourMinuteSpelled.Success)
        {
            number = decimal.Parse(hourMinuteSpelled.Groups["hours"].Value, CultureInfo.InvariantCulture) +
                     decimal.Parse(hourMinuteSpelled.Groups["minutes"].Value, CultureInfo.InvariantCulture) / 60m;
            return true;
        }
        var hourMinute = HourMinuteText.Match(value);
        if (hourMinute.Success)
        {
            var hoursPart = decimal.Parse(hourMinute.Groups["hours"].Value, CultureInfo.InvariantCulture);
            var minutesPart = hourMinute.Groups["minutes"].Success
                ? decimal.Parse(hourMinute.Groups["minutes"].Value, CultureInfo.InvariantCulture)
                : 0m;
            number = hoursPart + minutesPart / 60m;
            return true;
        }
        var clock = ClockText.Match(value);
        if (clock.Success)
        {
            number = decimal.Parse(clock.Groups["hours"].Value, CultureInfo.InvariantCulture) +
                     decimal.Parse(clock.Groups["minutes"].Value, CultureInfo.InvariantCulture) / 60m;
            return true;
        }
        var trailingHours = TrailingHoursText.Match(value);
        if (trailingHours.Success)
        {
            return TryParseDecimal(trailingHours.Groups["value"].Value, out number);
        }
        var minutesOnly = MinutesOnlyText.Match(value);
        if (minutesOnly.Success)
        {
            number = decimal.Parse(minutesOnly.Groups["minutes"].Value, CultureInfo.InvariantCulture) / 60m;
            return true;
        }

        // "2 giorni" dentro la colonna delle ore non deve diventare "2 ore":
        // viene segnalato al chiamante tramite TryGetDuration.
        if (DayText.IsMatch(value))
        {
            number = 0m;
            return false;
        }

        return TryParseDecimal(value, out number);
    }

    /// <summary>
    /// Interpreta la colonna della durata distinguendo l'unità dichiarata nella
    /// cella: "2 giorni" / "2gg" sono giorni (vanno moltiplicati per la giornata
    /// lavorativa), "16" e "16h" sono ore.
    /// </summary>
    public bool TryGetDuration(out decimal value, out bool expressedInDays)
    {
        expressedInDays = false;
        var text = Text.Trim();
        var normalized = text.Replace(" ", string.Empty).Replace("\u00a0", string.Empty);
        var dayMatch = DayText.Match(normalized);
        if (dayMatch.Success && TryParseDecimal(dayMatch.Groups["value"].Value, out var days))
        {
            value = days;
            expressedInDays = true;
            return true;
        }

        if (text.Length > 0 && !EmptyTexts.Contains(text, StringComparer.OrdinalIgnoreCase))
        {
            var written = Regex.Match(text, @"(?:giorn\w*|\bgg\b|\bday\w*\b)", RegexOptions.IgnoreCase);
            if (written.Success)
            {
                var digits = Regex.Match(text, @"\d+(?:[.,]\d+)?");
                if (digits.Success && TryParseDecimal(digits.Value, out var writtenDays))
                {
                    value = writtenDays;
                    expressedInDays = true;
                    return true;
                }
            }
        }

        return TryGetNumber(out value);
    }

    private static bool TryParseDecimal(string value, out decimal number)
    {
        var comma = value.LastIndexOf(',');
        var dot = value.LastIndexOf('.');
        if (comma >= 0 && dot >= 0)
        {
            var decimalSeparator = comma > dot ? ',' : '.';
            var groupSeparator = decimalSeparator == ',' ? '.' : ',';
            value = value.Replace(groupSeparator.ToString(), string.Empty)
                .Replace(decimalSeparator, '.');
        }
        else
        {
            value = value.Replace(',', '.');
        }

        return decimal.TryParse(
            value,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out number);
    }
}
