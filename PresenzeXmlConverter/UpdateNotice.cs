using System.Globalization;
using System.Text.RegularExpressions;

namespace PresenzeXmlConverter;

/// <summary>
/// Testi e regole delle notifiche di aggiornamento, come calcolo puro (verificabile con un test): quando avvisare
/// di nuovo, cosa scrivere nel fumetto e nel suggerimento dell'icona nel tray.
/// </summary>
internal static partial class UpdateNotice
{
    /// <summary>Limite di Windows per il testo dell'icona nel tray.</summary>
    public const int TrayTextLimit = 127;

    [GeneratedRegex(@"^[\s#>*\-••\p{So}\p{Cs}]+")]
    private static partial Regex LeadingMarks();

    /// <summary>Si avvisa una volta al giorno per la stessa versione (e subito per una versione nuova), non a ogni controllo orario.</summary>
    public static bool ShouldNotify(string? lastNotice, Version version, DateTime today) =>
        !string.Equals(lastNotice, Record(version, today), StringComparison.Ordinal);

    /// <summary>Il valore da salvare dopo l'avviso: versione e giorno.</summary>
    public static string Record(Version version, DateTime today) =>
        string.Create(CultureInfo.InvariantCulture, $"{version.ToString(3)}|{today:yyyy-MM-dd}");

    /// <summary>Il suggerimento dell'icona nel tray: il nome della finestra e, se c'è, l'aggiornamento pronto.</summary>
    public static string TrayTooltip(string caption, Version? pending)
    {
        var text = pending is null ? caption : $"{caption} · aggiornamento {pending.ToString(3)} disponibile";
        return text.Length <= TrayTextLimit ? text : text[..(TrayTextLimit - 1)] + "…";
    }

    /// <summary>Testo del fumetto: da quale versione a quale, la prima novità e l'invito a cliccare.</summary>
    public static string BalloonText(Version current, Version update, string? notes)
    {
        var headline = FirstNoteLine(notes);
        var text = $"Dalla {current.ToString(3)} alla {update.ToString(3)}." +
                   (headline.Length > 0 ? $" Novità: {headline}." : string.Empty) +
                   " Clic per vedere le novità e aggiornare.";
        // I fumetti di Windows mostrano al massimo 255 caratteri: meglio una frase intera e più corta.
        return text.Length <= 250 ? text : $"Dalla {current.ToString(3)} alla {update.ToString(3)}. Clic per vedere le novità e aggiornare.";
    }

    /// <summary>
    /// La prima novità vera delle note: il primo punto elenco (senza simboli né grassetti), altrimenti il primo
    /// titolo; accorciata. Le righe d'intestazione delle note («Vyrn Presence 1.2.3 — v1.2.3») non contano.
    /// </summary>
    public static string FirstNoteLine(string? notes)
    {
        string? heading = null;
        foreach (var raw in (notes ?? string.Empty).Split('\n'))
        {
            var trimmed = raw.Trim();
            var line = LeadingMarks().Replace(trimmed, string.Empty).Replace("**", string.Empty, StringComparison.Ordinal).Trim().TrimEnd('.', ':');
            if (line.Length < 8 || line.StartsWith("Vyrn Presence", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var shortened = line.Length <= 90 ? line : line[..89].TrimEnd() + "…";
            if (trimmed.StartsWith('-') || trimmed.StartsWith('*') || trimmed.StartsWith('•'))
            {
                return shortened;
            }
            heading ??= shortened;
        }
        return heading ?? string.Empty;
    }
}
