using Microsoft.Win32;

namespace PresenzeXmlConverter;

/// <summary>
/// Dove si ricorda la scelta del tema. È un piccolo file di testo accanto ai dati e non il database: il tema serve
/// prima di creare la prima finestra, quando il database non è ancora aperto. Se manca o è illeggibile si segue Windows.
/// </summary>
internal static class ThemePreference
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeGioia",
        "PresenzeXmlConverter",
        "theme.txt");

    /// <summary>Senza una scelta salvata si parte dal tema «Vetro».</summary>
    public const ThemeMode DefaultMode = ThemeMode.Glass;

    public static ThemeMode Load(string? path = null)
    {
        try
        {
            var file = path ?? FilePath;
            return File.Exists(file) ? ThemeChoice.Parse(File.ReadAllText(file)) : DefaultMode;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DefaultMode;
        }
    }

    /// <summary>Salva la scelta; restituisce false se non è stato possibile (l'app continua con il tema di prima).</summary>
    public static bool Save(ThemeMode mode, string? path = null)
    {
        try
        {
            var file = path ?? FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, ThemeChoice.Serialize(mode));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Windows è impostato sul tema scuro per le app? (Windows 10 1809 e successivi; altrimenti chiaro.)</summary>
    public static bool SystemIsDark()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Sceglie e applica il tema da usare in questa esecuzione.</summary>
    public static bool ApplyAtStartup()
    {
        Theme.Apply(ThemeChoice.PaletteFor(Load(), SystemIsDark()));
        return Theme.IsDark;
    }
}
