namespace PresenzeXmlConverter;

/// <summary>
/// Testi e disposizione della barra di stato in fondo alla finestra e del blocco «Dati locali» della barra laterale.
/// Solo calcolo: i controlli disegnano ciò che qui si decide.
/// </summary>
internal static class StatusBarModel
{
    public const string ReadyMessage = "Pronto";

    /// <summary>
    /// Toglie il pallino e gli spazi iniziali che i messaggi portavano nel testo («●   Backup creato»): il pallino
    /// ora lo disegna la barra, colorato. Un messaggio vuoto diventa «Pronto».
    /// </summary>
    public static string CleanMessage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ReadyMessage;
        }
        var cleaned = text.Trim().TrimStart('●', '•', '·', ' ', '\t').Trim();
        while (cleaned.Contains("  ", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
        }
        return cleaned.Length == 0 ? ReadyMessage : cleaned;
    }

    /// <summary>«Aggiornamenti · aggiornato alla 1.42.0 · 12:13» → «aggiornato alla 1.42.0 · 12:13» (il segmento ha già l'icona).</summary>
    public static string CleanUpdates(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "verifica in corso";
        }
        var cleaned = text.Trim();
        const string prefix = "Aggiornamenti";
        if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[prefix.Length..];
        }
        cleaned = cleaned.Trim(' ', '·', '-', '–', ':');
        return cleaned.Length == 0 ? "verifica in corso" : cleaned;
    }

    /// <summary>«v1.42.0» dalla versione in uso.</summary>
    public static string VersionText(Version version) => $"v{version.ToString(3)}";

    /// <summary>Ora e minuti, come «12:13».</summary>
    public static string ClockText(DateTime now) => now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Quali segmenti di destra entrano. <paramref name="widths"/> è in ordine di importanza (aggiornamenti, versione, ora):
    /// si tolgono prima gli ultimi. Resta sempre almeno <paramref name="minimumMessage"/> pixel al messaggio di stato;
    /// il primo segmento si tiene sempre (può essere accorciato dal disegno).
    /// </summary>
    public static int SegmentsThatFit(int available, int minimumMessage, IReadOnlyList<int> widths, int separator)
    {
        if (widths.Count == 0)
        {
            return 0;
        }
        var kept = widths.Count;
        while (kept > 1 && Total(widths, kept, separator) > available - minimumMessage)
        {
            kept--;
        }
        return kept;
    }

    /// <summary>Larghezza dei primi <paramref name="count"/> segmenti con i separatori fra uno e l'altro.</summary>
    public static int Total(IReadOnlyList<int> widths, int count, int separator)
    {
        var total = 0;
        for (var index = 0; index < count && index < widths.Count; index++)
        {
            total += widths[index] + (index > 0 ? separator : 0);
        }
        return total;
    }

    /// <summary>Tono di un messaggio: avviso se la barra lo mostra in giallo, altrimenti a posto.</summary>
    public static HealthLevel MessageLevel(bool isWarning) => isWarning ? HealthLevel.Warning : HealthLevel.Good;

    /// <summary>Come «Dati locali» mostra una riga di stato che comincia con il pallino («●  Database collegato»).</summary>
    public static string CleanRailLine(string? text) => CleanMessage(text) is { } line && line != ReadyMessage ? line : string.Empty;
}
