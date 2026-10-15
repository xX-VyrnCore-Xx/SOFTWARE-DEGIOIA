using System.Globalization;
using System.Text.RegularExpressions;

namespace PresenzeXmlConverter;

/// <summary>
/// Conteggio animato dei numeri nelle schede della dashboard: dato il testo di partenza e quello di arrivo
/// («1,234», «176h 30m») calcola il testo da mostrare a un certo punto della corsa. Ogni numero scorre per conto
/// suo e mantiene il formato del traguardo (separatore delle migliaia, zeri iniziali), quindi «176h 30m» passa per
/// «88h 15m» e non si vede mai un formato diverso da quello finale. Calcolo puro, verificabile con un test.
/// </summary>
internal static partial class CountUpText
{
    [GeneratedRegex(@"\d{1,3}(?:,\d{3})+|\d+")]
    private static partial Regex Numbers();

    /// <summary>Il testo a <paramref name="progress"/> (0 = partenza, 1 = traguardo esatto).</summary>
    public static string Frame(string from, string to, float progress)
    {
        if (progress >= 1f || string.IsNullOrEmpty(to) || string.Equals(from, to, StringComparison.Ordinal))
        {
            return to ?? string.Empty;
        }
        var source = Numbers().Matches(from ?? string.Empty).Select(match => Parse(match.Value)).ToArray();
        var index = 0;
        return Numbers().Replace(to, match =>
        {
            var target = Parse(match.Value);
            var start = index < source.Length ? source[index] : 0L;
            index++;
            var value = (long)Math.Round(start + ((target - start) * (double)Math.Clamp(progress, 0f, 1f)));
            var text = Format(value, match.Value);
            // I minuti dopo le ore («176h 05m») hanno sempre due cifre, anche a metà corsa.
            var isMinutes = match.Index >= 2 &&
                            to[match.Index - 1] == ' ' &&
                            to[match.Index - 2] == 'h' &&
                            match.Index + match.Length < to.Length &&
                            to[match.Index + match.Length] == 'm';
            return isMinutes ? text.PadLeft(2, '0') : text;
        });
    }

    private static long Parse(string token) =>
        long.TryParse(token.Replace(",", string.Empty, StringComparison.Ordinal), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0L;

    /// <summary>Scrive <paramref name="value"/> come il numero del traguardo: con le migliaia o con gli zeri iniziali.</summary>
    private static string Format(long value, string template)
    {
        if (template.Contains(',', StringComparison.Ordinal))
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }
        return template.Length > 1 && template[0] == '0'
            ? value.ToString(CultureInfo.InvariantCulture).PadLeft(template.Length, '0')
            : value.ToString(CultureInfo.InvariantCulture);
    }
}
