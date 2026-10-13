namespace PresenzeXmlConverter;

/// <summary>
/// Regole del trascinamento di un file sulla finestra principale.
///
/// Trascinare il foglio su PresenzeFlow evita di doverlo cercare nel dialogo di
/// apertura; le regole stanno qui, separate dall'interfaccia, perché su un
/// trascinamento arrivano percorsi di ogni tipo (cartelle, file temporanei di
/// Excel, più file insieme) e va scelto quello giusto senza sorprese.
/// </summary>
internal static class WorkbookDrop
{
    /// <summary>Formato principale (cartella di lavoro Excel).</summary>
    internal const string WorkbookExtension = ".xlsx";

    /// <summary>
    /// Formati accettati, gli stessi del dialogo di apertura: .xlsx e .xlsm (la cartella
    /// di lavoro con macro, che molti gestionali di presenze esportano). Le macro non
    /// vengono mai eseguite: il file si legge soltanto.
    /// </summary>
    internal static readonly string[] WorkbookExtensions = [".xlsx", ".xlsm"];

    /// <summary>Filtro del dialogo di apertura.</summary>
    internal const string OpenFileFilter = "Fogli Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm";

    /// <summary>L'estensione del file è fra quelle supportate (senza distinguere maiuscole)?</summary>
    public static bool HasSupportedExtension(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        WorkbookExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Il percorso è un foglio Excel? Guarda solo il nome: serve a decidere
    /// subito se accettare il trascinamento, anche per file non ancora leggibili.
    /// </summary>
    public static bool LooksLikeWorkbook(string? path) => HasSupportedExtension(path);

    /// <summary>
    /// Primo foglio Excel realmente utilizzabile fra i percorsi trascinati
    /// (null se non ce n'è nessuno): cartelle, file di altro tipo e file
    /// temporanei di Excel vengono ignorati in silenzio.
    /// </summary>
    public static string? FirstWorkbookPath(IEnumerable<string>? droppedPaths)
    {
        if (droppedPaths is null)
        {
            return null;
        }
        foreach (var path in droppedPaths)
        {
            if (LooksLikeWorkbook(path) && File.Exists(path))
            {
                return path;
            }
        }
        return null;
    }
}
