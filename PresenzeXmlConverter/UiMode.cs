namespace PresenzeXmlConverter;

/// <summary>Quale interfaccia aprire all'avvio: quella classica (WinForms) o la nuova (WPF).</summary>
internal enum UiKind
{
    Classic,
    Modern
}

/// <summary>
/// La scelta dell'interfaccia: un parametro sulla riga di comando (<c>--wpf</c>, <c>--classic</c>) vale per quell'avvio,
/// altrimenti si legge il file di testo salvato. Senza una scelta parte la nuova interfaccia; la classica resta il ripiego:
/// se la nuova non riesce ad aprirsi (errore o chiusura improvvisa) per due avvii di fila, si torna alla classica da soli.
/// Solo file e testo, senza finestre, così si verifica con un test.
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

    /// <summary>Quanti avvii consecutivi della nuova interfaccia senza finestra comparsa bastano per tornare alla classica.</summary>
    public const int FailedStartsBeforeFallback = 2;

    public static string StartMarkerPath { get; } = Path.Combine(Path.GetDirectoryName(FilePath)!, "ui-start.txt");

    /// <summary>
    /// Da chiamare prima di aprire la nuova interfaccia: segna un avvio in corso. Restituisce <c>false</c> (e salva la
    /// classica) se gli ultimi avvii non sono mai arrivati a mostrare la finestra.
    /// </summary>
    public static bool BeginModernStart(string? path = null, string? markerPath = null)
    {
        var marker = markerPath ?? StartMarkerPath;
        try
        {
            var failed = File.Exists(marker) && int.TryParse(File.ReadAllText(marker).Trim(), out var count) ? count : 0;
            if (failed >= FailedStartsBeforeFallback)
            {
                File.Delete(marker);
                Save(UiKind.Classic, path);
                return false;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
            File.WriteAllText(marker, (failed + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Senza marcatore non c'è rilevamento dei blocchi, ma l'avvio prosegue.
        }
        return true;
    }

    /// <summary>La nuova interfaccia è comparsa: l'avvio è riuscito.</summary>
    public static void ModernStartSucceeded(string? markerPath = null)
    {
        try
        {
            File.Delete(markerPath ?? StartMarkerPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Un marcatore rimasto vale al più un avvio in più in conto.
        }
    }

    public static UiKind Load(string? path = null)
    {
        try
        {
            var file = path ?? FilePath;
            if (File.Exists(file) && string.Equals(File.ReadAllText(file).Trim(), "classic", StringComparison.OrdinalIgnoreCase))
            {
                return UiKind.Classic;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Un file illeggibile non deve impedire l'avvio: vale la scelta predefinita.
        }
        return UiKind.Modern;
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
