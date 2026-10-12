using System.Runtime.InteropServices;

namespace PresenzeXmlConverter;

/// <summary>
/// Aspetto nativo della finestra su Windows: titolo scuro e angoli arrotondati.
///
/// La finestra principale ha un'intestazione scura disegnata nell'area client:
/// senza questo, Windows disegna sopra di essa una barra del titolo chiara e la
/// finestra sembra composta da due programmi diversi. Ogni chiamata è protetta:
/// su Windows più vecchi, o in un ambiente senza compositore, non cambia nulla
/// e non solleva errori.
/// </summary>
internal static class WindowChrome
{
    /// <summary>DWMWA_USE_IMMERSIVE_DARK_MODE: valore definitivo (Windows 10 1903 e successivi).</summary>
    private const int ImmersiveDarkMode = 20;

    /// <summary>Lo stesso attributo nella numerazione provvisoria di Windows 10 1809.</summary>
    private const int ImmersiveDarkModeLegacy = 19;

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

    /// <summary>Applica il tema scuro al titolo e chiede gli angoli arrotondati.</summary>
    public static void Apply(IntPtr window)
    {
        if (!OperatingSystem.IsWindows() || window == IntPtr.Zero)
        {
            return;
        }

        ApplyDarkTitleBar(window);
        ApplyRoundedCorners(window);
    }

    private static void ApplyDarkTitleBar(IntPtr window)
    {
        var enabled = 1;
        foreach (var attribute in DarkModeAttributesToTry(Environment.OSVersion.Version.Build))
        {
            if (SetWindowAttribute(window, attribute, enabled))
            {
                return;
            }
        }
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
