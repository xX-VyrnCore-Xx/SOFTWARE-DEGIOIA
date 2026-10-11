namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Guardia di regressione: l'app è single-file ma le librerie native (SQLite)
/// restano file separati nella cartella di publish. Se l'installer non le copia,
/// l'app installata termina all'avvio con DllNotFoundException senza mostrare
/// alcuna finestra ("non si avvia mai").
/// </summary>
public class InstallerPackagingTests
{
    [Fact]
    public void InstallerShipsNativeSqliteLibraryRequiredForStartup()
    {
        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is null)
        {
            // Cartella repo non disponibile in questo ambiente: niente da verificare.
            return;
        }

        var script = File.ReadAllText(Path.Combine(repositoryRoot, "Installer.iss"));
        Assert.Contains(@"Source: ""dist\app\e_sqlite3.dll""", script, StringComparison.Ordinal);

        var publishDirectory = Path.Combine(repositoryRoot, "dist", "app");
        if (Directory.Exists(publishDirectory))
        {
            Assert.True(
                File.Exists(Path.Combine(publishDirectory, "e_sqlite3.dll")),
                "Il publish single-file deve produrre dist/app/e_sqlite3.dll: senza quel file l'app installata non si avvia.");
        }
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
