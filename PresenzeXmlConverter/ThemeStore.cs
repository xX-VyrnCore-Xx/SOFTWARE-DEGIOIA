namespace PresenzeXmlConverter;

/// <summary>
/// Il file di testo con la scelta del tema (lettura, scrittura e passaggio una tantum al tema «Vetro»): solo file,
/// senza finestre, così si verifica con un test.
/// </summary>
internal static class ThemeStore
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeGioia",
        "PresenzeXmlConverter",
        "theme.txt");

    /// <summary>Senza una scelta salvata si parte dal tema «Vetro».</summary>
    public const ThemeMode DefaultMode = ThemeMode.Glass;

    private static string MigrationMarkerPath { get; } = Path.Combine(Path.GetDirectoryName(FilePath)!, "theme-glass-default.txt");

    public static ThemeMode Load(string? path = null, string? markerPath = null)
    {
        try
        {
            var file = path ?? FilePath;
            if (!File.Exists(file))
            {
                return DefaultMode;
            }
            var mode = ThemeChoice.Parse(File.ReadAllText(file));
            // «Come Windows» salvato prima dell'arrivo del tema «Vetro» è quasi sempre il valore scritto da sé aprendo
            // le Impostazioni, non una scelta: una volta sola si passa a «Vetro». Chi poi sceglie di nuovo «Come
            // Windows» viene rispettato (il segnale di avvenuto passaggio resta).
            var marker = markerPath ?? (path is null ? MigrationMarkerPath : null);
            if (marker is not null && mode == ThemeMode.System && !File.Exists(marker))
            {
                Save(ThemeMode.Glass, file);
                File.WriteAllText(marker, "1");
                return ThemeMode.Glass;
            }
            return mode;
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
}
