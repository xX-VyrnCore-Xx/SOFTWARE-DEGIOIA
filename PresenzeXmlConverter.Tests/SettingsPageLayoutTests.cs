namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Le schede del dialogo Impostazioni non devono mai chiedere più spazio di quello che hanno. Prima ogni
/// riga aveva un'altezza in pixel scritta a mano (e un conto per verificarla): bastava un carattere più
/// grande, per esempio con la scala dello schermo al 125%, perché il titolo si sovrapponesse al sottotitolo
/// e l'ultima riga di testo venisse tagliata. Ora le righe sono alte quanto il loro contenuto
/// (<c>SizeType.AutoSize</c>): qui si verificano i limiti della finestra e che nessuna riga sia tornata fissa;
/// che ogni scheda entri davvero lo misura il test grafico, sul dialogo vero.
/// </summary>
public class SettingsPageLayoutTests
{
    /// <summary>Altezza utile di un'area di lavoro 1024x768 con la barra delle applicazioni.</summary>
    private const int SmallestWorkingArea = 728;

    [Fact]
    public void TheDialogFitsOnTheSmallestScreenUsedByTheRunner()
    {
        Assert.True(
            SettingsPageLayout.MinimumWindowHeight + SettingsPageLayout.WindowFrameHeight <= SmallestWorkingArea,
            "La finestra minima non entrerebbe in un'area di lavoro 1024x768.");
        Assert.True(
            SettingsPageLayout.DefaultClientHeight + SettingsPageLayout.WindowFrameHeight <= SmallestWorkingArea,
            "La finestra predefinita non entrerebbe in un'area di lavoro 1024x768.");
        Assert.True(SettingsPageLayout.DefaultClientWidth >= SettingsPageLayout.MinimumWindowWidth);
        Assert.True(SettingsPageLayout.DefaultClientHeight >= SettingsPageLayout.MinimumWindowHeight);
    }

    [Fact]
    public void TheButtonsAreTallEnoughForTheirText()
    {
        Assert.True(SettingsPageLayout.ActionButtonHeight >= 34);
    }

    /// <summary>
    /// Nessuna riga a altezza fissa e nessuna barra di scorrimento: sono proprietà del sorgente, non del
    /// singolo calcolo, e vanno verificate sul file vero (la guardia gira in CI, dove i sorgenti sono accanto ai test).
    /// </summary>
    [Fact]
    public void TheDialogHasNoFixedRowHeightsAndNeverScrolls()
    {
        var path = Path.Combine(FindSourceRoot(), "PresenzeXmlConverter", "SettingsDialog.cs");
        Assert.True(File.Exists(path), $"Sorgente del dialogo non trovato: {path}");
        var text = File.ReadAllText(path);

        Assert.DoesNotContain("SizeType.Absolute", text, StringComparison.Ordinal);
        Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(text, @"CreatePage\(\)").Count - 1);
        Assert.DoesNotContain("AutoScroll = true", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ScrollBars.Vertical", text, StringComparison.Ordinal);
        // Niente caselle e pulsanti radio di Windows: gli interruttori e il controllo segmentato dell'app.
        Assert.DoesNotContain("new CheckBox", text, StringComparison.Ordinal);
        Assert.DoesNotContain("new RadioButton", text, StringComparison.Ordinal);
    }

    /// <summary>Radice dei sorgenti: la stessa ricerca usata dalle altre guardie.</summary>
    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PresenzeXmlConverter", "SettingsDialog.cs")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Sorgenti non trovati: la guardia non ha potuto controllare nulla.");
    }
}
