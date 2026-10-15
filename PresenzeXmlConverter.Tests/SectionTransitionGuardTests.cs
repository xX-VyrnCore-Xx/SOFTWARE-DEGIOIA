namespace PresenzeXmlConverter.Tests;

/// <summary>Regole sulla dissolvenza fra sezioni, verificate leggendo i sorgenti (i controlli non si aprono nei test).</summary>
public class SectionTransitionGuardTests
{
    [Fact]
    public void TheTransitionOnlyRunsWhenTheSectionReallyChangesAndNeverBlocksTheMouse()
    {
        var folder = SourceFolder();
        if (folder is null)
        {
            return;
        }
        var shell = File.ReadAllText(Path.Combine(folder, "MainForm.Shell.cs"));
        // Solo quando si passa da una sezione a un'altra (non all'avvio né quando si riseleziona la stessa).
        Assert.Contains("_shownSectionIndex >= 0 && _shownSectionIndex != index", shell, StringComparison.Ordinal);
        Assert.Contains("_sectionTransition.Play(", shell, StringComparison.Ordinal);

        var transition = File.ReadAllText(Path.Combine(folder, "SectionTransition.cs"));
        // Il velo lascia passare il mouse e rispetta gli effetti di Windows spenti.
        Assert.Contains("HitTestTransparent", transition, StringComparison.Ordinal);
        Assert.Contains("AnimationClock.EffectsEnabled", transition, StringComparison.Ordinal);
        // Un errore di disegno non deve mai impedire di cambiare sezione.
        Assert.Contains("catch (Exception exception) when", transition, StringComparison.Ordinal);
    }

    private static string? SourceFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Installer.iss")))
            {
                return Path.Combine(directory.FullName, "PresenzeXmlConverter");
            }
            directory = directory.Parent;
        }
        return null;
    }
}
