namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Le schede del dialogo Impostazioni non devono mai chiedere più spazio di
/// quello che hanno: una scheda più bassa delle righe che contiene schiaccia i
/// campi e taglia l'ultima riga di testo (era già successo una volta,
/// con una scheda alta 150 px per righe che ne chiedevano 142). Tenendo
/// le altezze in <see cref="SettingsPageLayout"/> il conto si verifica qui,
/// senza schermo, e il dialogo non ha bisogno di barre di scorrimento: quelle
/// native di Windows restano grigie e fuori tema.
/// </summary>
public class SettingsPageLayoutTests
{
    /// <summary>Altezza del campo di testo arrotondato, senza i suoi margini.</summary>
    private const int FieldHeight = 36;

    /// <summary>Margini verticali del campo (2 sopra, 8 sotto): li dichiara il dialogo.</summary>
    private const int FieldMargins = 10;

    /// <summary>Altezza di un pulsante compatto.</summary>
    private const int ButtonHeight = 34;

    /// <summary>Altezza di una riga di testo con i font piccoli della palette.</summary>
    private const int TextLineHeight = 16;

    /// <summary>Altezza utile di un'area di lavoro 1024x768 con la barra delle applicazioni.</summary>
    private const int SmallestWorkingArea = 728;

    [Fact]
    public void EveryPageFitsAtTheMinimumWindowSize()
    {
        var clientHeight = SettingsPageLayout.MinimumWindowHeight - SettingsPageLayout.WindowFrameHeight;
        foreach (var page in SettingsPageLayout.Pages)
        {
            Assert.True(
                SettingsPageLayout.Fits(clientHeight, page),
                $"Scheda alta {SettingsPageLayout.PageHeight(page)} px in uno spazio di " +
                $"{SettingsPageLayout.AvailablePageHeight(clientHeight)} px: servirebbe una barra di scorrimento.");
        }
    }

    [Fact]
    public void EveryPageFitsAtTheDefaultWindowSize()
    {
        foreach (var page in SettingsPageLayout.Pages)
        {
            Assert.True(
                SettingsPageLayout.Fits(SettingsPageLayout.DefaultClientHeight, page),
                $"La scheda da {SettingsPageLayout.PageHeight(page)} px non entra nella finestra predefinita.");
        }
    }

    [Fact]
    public void EveryRowLeavesRoomForWhatItHosts()
    {
        // Campi, pulsanti e caselle: le righe devono contenere il controllo e i suoi margini.
        Assert.True(SettingsPageLayout.FieldRow >= FieldHeight + FieldMargins);
        Assert.True(SettingsPageLayout.ActionRow >= ButtonHeight);
        Assert.True(SettingsPageLayout.CheckRow >= TextLineHeight);
        Assert.True(SettingsPageLayout.HolidaysRow >= FieldHeight + FieldMargins);

        // Righe di testo: mai meno di una riga, e per i testi guida almeno due,
        // perché una frase lunga va a capo.
        Assert.True(SettingsPageLayout.HintRow >= TextLineHeight * 2);
        Assert.True(SettingsPageLayout.FieldLabelRow >= TextLineHeight * 2);
        Assert.True(SettingsPageLayout.NoteRow >= TextLineHeight * 2);
        Assert.True(SettingsPageLayout.StatusRow >= TextLineHeight * 2);
        Assert.True(SettingsPageLayout.SignatureRow >= TextLineHeight);
    }

    [Fact]
    public void EveryDeclaredRowIsPositiveAndThePaddingIsEven()
    {
        foreach (var page in SettingsPageLayout.Pages)
        {
            Assert.NotEmpty(page);
            Assert.All(page, height => Assert.True(height > 0, $"Riga di altezza {height}."));
        }
        Assert.True(SettingsPageLayout.PagePadding > 0);
        Assert.Equal(0, SettingsPageLayout.PagePadding % 2);
    }

    [Fact]
    public void TheDialogFitsOnTheSmallestScreenUsedByTheRunner()
    {
        // Una finestra più alta dell'area di lavoro verrebbe tagliata dal sistema.
        Assert.True(
            SettingsPageLayout.MinimumWindowHeight + SettingsPageLayout.WindowFrameHeight <= SmallestWorkingArea,
            "La finestra minima non entrerebbe in un'area di lavoro 1024x768.");
        Assert.True(
            SettingsPageLayout.DefaultClientHeight + SettingsPageLayout.WindowFrameHeight <= SmallestWorkingArea,
            "La finestra predefinita non entrerebbe in un'area di lavoro 1024x768.");
    }

    [Fact]
    public void TheFooterLeavesRoomForItsButtons()
    {
        // Il piè di pagina è fatto dal messaggio di errore più i pulsanti: con una
        // riga sola per entrambi i pulsanti da 34 px finivano in 30 px utili e il
        // bordo inferiore veniva tagliato.
        Assert.Equal(
            SettingsPageLayout.FooterMessageRow + SettingsPageLayout.ActionButtonHeight,
            SettingsPageLayout.FooterRow);
        Assert.True(SettingsPageLayout.FooterMessageRow >= TextLineHeight);
    }

    /// <summary>
    /// Ogni scheda si costruisce dalle righe condivise e il dialogo non usa
    /// barre di scorrimento: sono due proprietà del sorgente, non del singolo
    /// calcolo, e vanno verificate sul file vero (come per la finestra principale).
    /// </summary>
    [Fact]
    public void TheDialogBuildsEveryPageFromTheSharedLayoutAndNeverScrolls()
    {
        var path = Path.Combine(FindSourceRoot(), "PresenzeXmlConverter", "SettingsDialog.cs");
        Assert.True(File.Exists(path), $"Sorgente del dialogo non trovato: {path}");
        var text = File.ReadAllText(path);

        foreach (var page in new[] { "ConversionRows", "UpdateRows", "NotificationRows" })
        {
            Assert.Contains($"CreatePage(SettingsPageLayout.{page})", text, StringComparison.Ordinal);
        }
        // Anche il contorno della finestra (intestazione, piè di pagina, spaziatura
        // della card) arriva da lì: i conti e il dialogo non possono separarsi.
        foreach (var shared in new[] { "HeaderRow", "FooterRow", "FooterMessageRow", "ActionButtonHeight", "ShellPadding", "WindowPadding" })
        {
            Assert.Contains($"SettingsPageLayout.{shared}", text, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("AutoScroll = true", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ScrollBars.Vertical", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Radice dei sorgenti: la stessa ricerca usata dalle altre guardie, così la
    /// verifica gira in CI (dove i file veri sono accanto ai test).
    /// </summary>
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
