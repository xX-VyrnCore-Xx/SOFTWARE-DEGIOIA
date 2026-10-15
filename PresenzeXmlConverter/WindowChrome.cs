using System.Runtime.InteropServices;

namespace PresenzeXmlConverter;

/// <summary>
/// Aspetto nativo della finestra su Windows: barra del titolo chiara come l'intestazione dell'app e angoli arrotondati.
///
/// L'intestazione dell'applicazione è bianca: la barra del titolo di Windows, se segue il tema scuro del sistema,
/// resta nera sopra di essa e la finestra sembra composta da due programmi diversi. Si chiede quindi a Windows
/// la barra chiara (su Windows 11 anche con i colori esatti dell'app: fondo, testo e bordo). Ogni chiamata è
/// protetta: su Windows più vecchi, o in un ambiente senza compositore, non cambia nulla e non solleva errori.
/// </summary>
internal static class WindowChrome
{
    /// <summary>DWMWA_USE_IMMERSIVE_DARK_MODE: valore definitivo (Windows 10 1903 e successivi).</summary>
    private const int ImmersiveDarkMode = 20;

    /// <summary>Lo stesso attributo nella numerazione provvisoria di Windows 10 1809.</summary>
    private const int ImmersiveDarkModeLegacy = 19;

    /// <summary>DWMWA_BORDER_COLOR (Windows 11).</summary>
    private const int BorderColor = 34;

    /// <summary>DWMWA_CAPTION_COLOR (Windows 11).</summary>
    private const int CaptionColor = 35;

    /// <summary>DWMWA_TEXT_COLOR (Windows 11).</summary>
    private const int TextColor = 36;

    /// <summary>DWMWA_WINDOW_CORNER_PREFERENCE (Windows 11).</summary>
    private const int WindowCornerPreference = 33;

    /// <summary>DWMWCP_ROUND.</summary>
    private const int CornerRound = 2;

    /// <summary>Windows 10 1903: da qui l'attributo definitivo è il 20.</summary>
    private const int ModernDarkModeBuild = 18985;

    /// <summary>Windows 11: gli angoli arrotondati sono gestiti dal sistema.</summary>
    private const int Windows11Build = 22000;

    /// <summary>
    /// Attributi da provare, in ordine, per il titolo scuro. Si prova prima quello
    /// atteso dalla build dichiarata, ma si tenta anche l'altro: la versione
    /// riportata dal sistema può essere quella massima dichiarata nel manifesto
    /// dell'eseguibile, non quella reale. Si ferma al primo che risponde.
    /// </summary>
    internal static int[] DarkModeAttributesToTry(int windowsBuild)
    {
        var newer = windowsBuild >= ModernDarkModeBuild;
        return newer
            ? new[] { ImmersiveDarkMode, ImmersiveDarkModeLegacy }
            : new[] { ImmersiveDarkModeLegacy, ImmersiveDarkMode };
    }

    /// <summary>Windows 11 disegna da sé gli angoli arrotondati delle finestre standard.</summary>
    internal static bool SupportsRoundedCorners(int windowsBuild) => windowsBuild >= Windows11Build;

    /// <summary>
    /// Il colore nel formato COLORREF di Windows (0x00BBGGRR), come lo vogliono gli attributi DWM dei colori:
    /// scambiando rosso e blu si otterrebbe una barra di un altro colore.
    /// </summary>
    internal static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    /// <summary>Colori della barra del titolo (Windows 11): gli stessi dell'intestazione dell'app.</summary>
    internal static (Color Caption, Color Text, Color Border) TitleBarColors() =>
        (Theme.HeaderFill, Theme.Ink, Theme.Divider);

    /// <summary>Applica la barra del titolo chiara e chiede gli angoli arrotondati.</summary>
    public static void Apply(IntPtr window)
    {
        if (!OperatingSystem.IsWindows() || window == IntPtr.Zero)
        {
            return;
        }

        ApplyLightTitleBar(window);
        ApplyTitleBarColors(window);
        ApplyRoundedCorners(window);
    }

    /// <summary>Spegne il tema scuro della barra (anche se il sistema è in modalità scura).</summary>
    private static void ApplyLightTitleBar(IntPtr window)
    {
        var disabled = 0;
        foreach (var attribute in DarkModeAttributesToTry(Environment.OSVersion.Version.Build))
        {
            if (SetWindowAttribute(window, attribute, disabled))
            {
                return;
            }
        }
    }

    /// <summary>Su Windows 11 dà alla barra i colori dell'app (fondo, testo, bordo); prima di Windows 11 non fa nulla.</summary>
    private static void ApplyTitleBarColors(IntPtr window)
    {
        if (!SupportsRoundedCorners(Environment.OSVersion.Version.Build))
        {
            return;
        }
        var (caption, text, border) = TitleBarColors();
        SetWindowAttribute(window, CaptionColor, ToColorRef(caption));
        SetWindowAttribute(window, TextColor, ToColorRef(text));
        SetWindowAttribute(window, BorderColor, ToColorRef(border));
    }

    private static void ApplyRoundedCorners(IntPtr window)
    {
        if (!SupportsRoundedCorners(Environment.OSVersion.Version.Build))
        {
            return;
        }
        SetWindowAttribute(window, WindowCornerPreference, CornerRound);
    }

    /// <summary>
    /// Imposta un attributo della finestra; restituisce false se Windows non lo
    /// conosce (versione più vecchia) o se la chiamata non è disponibile.
    /// </summary>
    private static bool SetWindowAttribute(IntPtr window, int attribute, int value)
    {
        try
        {
            return DwmSetWindowAttribute(window, attribute, ref value, sizeof(int)) == 0;
        }
        catch (Exception)
        {
            // dwmapi assente o compositore non disponibile: la finestra resta standard.
            return false;
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
