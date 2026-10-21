namespace PresenzeXmlConverter;

/// <summary>Quale interfaccia aprire all'avvio: quella classica (WinForms) o la nuova (WPF, anteprima).</summary>
internal enum UiKind
{
    Classic,
    Modern
}

/// <summary>
/// La scelta dell'interfaccia: un parametro sulla riga di comando (<c>--wpf</c>, <c>--classic</c>) vale per quell'avvio,
/// altrimenti si legge il file di testo salvato. Senza una scelta resta la classica: la nuova è opt-in finché non è
/// completa. Solo file e testo, senza finestre, così si verifica con un test.
/// </summary>
internal static class UiMode
{
    public const string ModernFlag = "--wpf";
    public const string ClassicFlag = "--classic";

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeGioia",
        "PresenzeXmlConverter",
        "ui.txt");

    public static UiKind Choose(IEnumerable<string> args, string? path = null)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, ClassicFlag, StringComparison.OrdinalIgnoreCase))
            {
                return UiKind.Classic;
            }
            if (string.Equals(arg, ModernFlag, StringComparison.OrdinalIgnoreCase))
            {
                return UiKind.Modern;
            }
        }
        return Load(path);
    }

    public static UiKind Load(string? path = null)
    {
        try
        {
            var file = path ?? FilePath;
            if (File.Exists(file) && string.Equals(File.ReadAllText(file).Trim(), "wpf", StringComparison.OrdinalIgnoreCase))
            {
                return UiKind.Modern;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Un file illeggibile non deve impedire l'avvio: si resta sulla classica.
        }
        return UiKind.Classic;
    }

    public static void Save(UiKind kind, string? path = null)
    {
        try
        {
            var file = path ?? FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, kind == UiKind.Modern ? "wpf" : "classic");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // La preferenza non salvata si ripete alla prossima scelta: niente di grave.
        }
    }
}
