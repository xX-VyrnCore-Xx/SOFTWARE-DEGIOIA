using System.Text.RegularExpressions;

namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Regole sui menu a comparsa e sul trasferimento di codici e aziende, verificate leggendo i sorgenti
/// (i controlli non si possono aprire nei test).
/// </summary>
public class MenuLifetimeTests
{
    [Fact]
    public void NoContextMenuIsDisposedInItsClosedEvent()
    {
        // Windows Forms chiude il menu PRIMA di eseguire il clic della voce scelta: eliminarlo in «Closed»
        // faceva fallire il clic con «Cannot access a disposed object… ContextMenuStrip».
        var folder = SourceFolder();
        if (folder is null)
        {
            return;
        }
        foreach (var file in Directory.EnumerateFiles(folder, "*.cs"))
        {
            var text = File.ReadAllText(file);
            Assert.False(
                Regex.IsMatch(text, @"\.Closed\s*\+=\s*[^;]*\.Dispose\(\)"),
                $"{Path.GetFileName(file)}: un menu viene eliminato nell'evento Closed.");
        }
    }

    [Fact]
    public void TransferButtonIsOnBothCodesAndCompaniesPages()
    {
        var folder = SourceFolder();
        if (folder is null)
        {
            return;
        }
        Assert.Contains("CreateTransferButton(", File.ReadAllText(Path.Combine(folder, "MainForm.Shell.cs")), StringComparison.Ordinal);
        Assert.Contains("CreateTransferButton(", File.ReadAllText(Path.Combine(folder, "MainForm.Companies.cs")), StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> Palettes() => [[false], [true]];

    [Theory]
    [MemberData(nameof(Palettes))]
    public void MenuTextIsReadableOnEveryMenuBackground(bool dark)
    {
        var p = dark ? ThemePalette.Dark : ThemePalette.Light;
        Assert.True(ColorContrast.Ratio(MenuPalette.Text(p, true, false), MenuPalette.Back(p)) >= 7, "voce normale");
        Assert.True(ColorContrast.Ratio(MenuPalette.Text(p, true, true), MenuPalette.HoverFill(p)) >= 4.5, "voce evidenziata");
        Assert.True(ColorContrast.Ratio(MenuPalette.Text(p, false, false), MenuPalette.Back(p)) >= 1.8, "voce disattivata");
        Assert.True(ColorContrast.Ratio(MenuPalette.Border(p), MenuPalette.Back(p)) >= 1.15, "bordo visibile");
    }

    [Fact]
    public void MenusComboBoxesAndTooltipsUseTheThemedVersions()
    {
        var folder = SourceFolder();
        if (folder is null)
        {
            return;
        }
        foreach (var file in Directory.EnumerateFiles(folder, "*.cs"))
        {
            var name = Path.GetFileName(file);
            if (name == "ThemedMenus.cs")
            {
                continue;
            }
            var text = File.ReadAllText(file);
            Assert.False(Regex.IsMatch(text, @"new\s+ContextMenuStrip\s*\("), $"{name}: usa ThemedMenus.Create().");
            Assert.False(Regex.IsMatch(text, @"new\s+ToolTip\s*\("), $"{name}: usa ThemedMenus.CreateToolTip().");
            Assert.False(Regex.IsMatch(text, @"ComboBox\s+\w+\s*=\s*new\s*(ComboBox)?\s*(\(\))?\s*;"), $"{name}: usa ThemedComboBox.");
        }
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
