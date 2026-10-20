namespace PresenzeXmlConverter;

/// <summary>
/// Il file di testo con la scelta del tema (lettura, scrittura e passaggio una tantum al tema chiaro): solo file,
/// senza finestre, così si verifica con un test.
/// </summary>
internal static class ThemeStore
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeGioia",
        "PresenzeXmlConverter",
        "theme.txt");

    /// <summary>Senza una scelta salvata si parte dal tema chiaro, in stile Windows 11 / Office.</summary>
    public const ThemeMode DefaultMode = ThemeMode.Light;

    private static string MigrationMarkerPath { get; } = Path.Combine(Path.GetDirectoryName(FilePath)!, "theme-light-default.txt");

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
            // «Come Windows» e «Vetro» salvati prima del tema chiaro rifatto sono i predefiniti di versioni precedenti
            // (o il valore scritto da sé aprendo le Impostazioni), non una scelta: una volta sola si passa a «Chiaro».
            // Chi poi sceglie di nuovo «Come Windows», «Vetro» o «Scuro» viene rispettato (il segnale resta).
            var marker = markerPath ?? (path is null ? MigrationMarkerPath : null);
            if (marker is not null && (mode is ThemeMode.System or ThemeMode.Glass) && !File.Exists(marker))
            {
                Save(ThemeMode.Light, file);
                File.WriteAllText(marker, "1");
                return ThemeMode.Light;
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
