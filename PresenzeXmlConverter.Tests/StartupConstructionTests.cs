using System.Text.RegularExpressions;

namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Guardia di regressione sull'avvio: la versione 1.15.0 costruiva la finestra con
/// <c>Trace?.Measure("interfaccia", BuildLayout)</c>. Con la diagnostica spenta
/// (il caso normale) l'operatore <c>?.</c> salta l'intera chiamata, quindi
/// <c>BuildLayout</c> non girava mai: finestra vuota, nessun pulsante per importare
/// l'Excel. Il costruttore non è eseguibile nei test (richiede Windows), quindi la
/// regola viene verificata leggendo il sorgente.
/// </summary>
public class StartupConstructionTests
{
    [Fact]
    public void StartupPhasesAreNeverSkippedByANullConditionalCall()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            return;
        }

        foreach (var file in Directory.GetFiles(Path.Combine(root, "PresenzeXmlConverter"), "MainForm*.cs"))
        {
            // Contano solo le righe di codice: commenti e documentazione possono
            // citare il difetto per spiegarlo.
            var source = string.Join(
                "\n",
                File.ReadAllLines(file).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            var offenders = Regex.Matches(source, @"Trace\?\.Measure\(");
            Assert.True(
                offenders.Count == 0,
                $"{Path.GetFileName(file)}: 'Trace?.Measure(...)' salta l'azione quando la diagnostica è spenta. Usa MeasureStartup.");
        }
    }

    [Fact]
    public void LayoutAndUpdatesAreBuiltThroughTheAlwaysRunningHelper()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            return;
        }

        var source = File.ReadAllText(Path.Combine(root, "PresenzeXmlConverter", "MainForm.cs"));
        Assert.Contains("MeasureStartup(\"interfaccia\", BuildLayout);", source, StringComparison.Ordinal);
        Assert.Contains("MeasureStartup(\"aggiornamenti\", InitializeUpdates);", source, StringComparison.Ordinal);
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Installer.iss")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return null;
    }
}
