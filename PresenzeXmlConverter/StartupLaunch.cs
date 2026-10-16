namespace PresenzeXmlConverter;

/// <summary>
/// Avvio con Windows, solo calcolo: la riga di comando registrata e il riconoscimento dell'avvio in background.
/// Il programma parte nel tray senza aprire la finestra, così i controlli degli aggiornamenti girano anche quando
/// l'utente non l'ha aperto.
/// </summary>
internal static class StartupLaunch
{
    /// <summary>Nome del valore nel registro dell'utente.</summary>
    public const string RegistryValueName = "VyrnPresence";

    /// <summary>Parametro che chiede l'avvio nel tray, senza finestra.</summary>
    public const string BackgroundFlag = "--tray";

    /// <summary>La riga registrata: percorso fra virgolette e parametro.</summary>
    public static string Command(string executablePath) => $"\"{executablePath}\" {BackgroundFlag}";

    public static bool IsBackgroundStart(IEnumerable<string> args) =>
        args.Any(arg => string.Equals(arg, BackgroundFlag, StringComparison.OrdinalIgnoreCase));

    /// <summary>Il percorso dentro una riga registrata (null se la riga è vuota o malformata).</summary>
    public static string? PathOf(string? command)
    {
        var text = command?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }
        var space = text.IndexOf(' ');
        return space < 0 ? text : text[..space];
    }

    /// <summary>La riga registrata è quella di questo programma (stesso percorso, avvio nel tray)?</summary>
    public static bool IsCurrent(string? command, string executablePath) =>
        string.Equals(PathOf(command), executablePath, StringComparison.OrdinalIgnoreCase) &&
        command!.Contains(BackgroundFlag, StringComparison.OrdinalIgnoreCase);
}
