using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>Come leggere una variazione: più di prima, meno di prima, uguale.</summary>
internal enum TrendTone
{
    Same,
    Up,
    Down
}

/// <summary>Il testo e il tono della pillola di confronto sotto un indicatore della dashboard.</summary>
internal sealed record Trend(string Text, TrendTone Tone);

/// <summary>Confronto fra due totali di ore, solo calcolo (verificabile con un test).</summary>
internal static class TrendChip
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    /// <summary>
    /// Variazione percentuale dei minuti attuali sull'ultima esportazione. Null se manca un confronto sensato
    /// (nessuna esportazione precedente, totali vuoti).
    /// </summary>
    public static Trend? Compare(int currentMinutes, int? previousMinutes)
    {
        if (previousMinutes is not { } previous || previous <= 0 || currentMinutes <= 0)
        {
            return null;
        }
        var percent = Math.Round((currentMinutes - previous) * 100d / previous, 1, MidpointRounding.AwayFromZero);
        if (percent == 0)
        {
            return new Trend("come l'ultima esportazione", TrendTone.Same);
        }
        var sign = percent > 0 ? "+" : "−";
        var text = $"{sign}{Math.Abs(percent).ToString("0.0", Italian)}% sull'ultima esportazione";
        return new Trend(text, percent > 0 ? TrendTone.Up : TrendTone.Down);
    }
}
