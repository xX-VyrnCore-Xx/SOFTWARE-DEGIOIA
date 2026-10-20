using System.Text.RegularExpressions;

namespace PresenzeXmlConverter.Tests;

/// <summary>Ogni finestra «Apri»/«Salva» deve usare l'identificativo proprio, altrimenti Windows la riapre a tutto schermo.</summary>
public class FileDialogGuardTests
{
    [Fact]
    public void EveryFileDialogUsesTheAppClientId()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Installer.iss")))
        {
            directory = directory.Parent;
        }
        if (directory is null)
        {
            return;
        }
        var source = Path.Combine(directory.FullName, "PresenzeXmlConverter");
        var created = 0;
        var configured = 0;
        foreach (var file in Directory.EnumerateFiles(source, "*.cs"))
        {
            var text = File.ReadAllText(file);
            created += Regex.Matches(text, @"new\s+(Open|Save)FileDialog\b").Count;
            configured += Regex.Matches(text, @"\.ClientGuid\s*=\s*FileDialogs\.ClientId").Count;
        }
        Assert.True(created > 0, "Nessuna finestra Apri/Salva trovata: la guardia non controlla nulla.");
        Assert.Equal(created, configured);
    }

    [Fact]
    public void TheClientIdIsAStableNonEmptyValue() =>
        Assert.NotEqual(Guid.Empty, FileDialogs.ClientId);
}
