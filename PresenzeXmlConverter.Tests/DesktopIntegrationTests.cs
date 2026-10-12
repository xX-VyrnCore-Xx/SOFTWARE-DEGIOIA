namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Guardie sulle tre funzioni che collegano l'app a Windows e all'utente:
/// il titolo scuro nativo della finestra, il trascinamento del foglio Excel e
/// la firma digitale mostrata nelle Impostazioni. Gira anche su Linux perché
/// nessuna di queste classi dipende da WinForms.
/// </summary>
public class DesktopIntegrationTests
{
    /* ---------------------------- Titolo scuro ---------------------------- */

    [Fact]
    public void TheDarkTitleBarAttributeIsChosenForTheReportedWindowsBuild()
    {
        // Windows 10 1903 e successivi (compreso Windows 11): attributo 20,
        // poi 19 come riserva.
        Assert.Equal(new[] { 20, 19 }, WindowChrome.DarkModeAttributesToTry(19045));
        Assert.Equal(new[] { 20, 19 }, WindowChrome.DarkModeAttributesToTry(22621));

        // Windows 10 1809: l'attributo era ancora il 19, ma si prova anche l'altro
        // perché la versione riportata può essere quella dichiarata nel manifesto.
        Assert.Equal(new[] { 19, 20 }, WindowChrome.DarkModeAttributesToTry(17763));
    }

    [Fact]
    public void RoundedCornersAreOnlyRequestedWhereTheSystemKnowsThem()
    {
        Assert.False(WindowChrome.SupportsRoundedCorners(19045));
        Assert.True(WindowChrome.SupportsRoundedCorners(22000));
        Assert.True(WindowChrome.SupportsRoundedCorners(22621));
    }

    [Fact]
    public void ApplyingTheWindowChromeNeverThrows()
    {
        // Su un sistema senza compositore (o su un sistema che non è Windows)
        // l'applicazione non deve mai essere la causa di un errore.
        WindowChrome.Apply(IntPtr.Zero);
        WindowChrome.Apply(new IntPtr(1234));
    }

    /* --------------------------- Trascinamento ---------------------------- */

    [Theory]
    [InlineData("presenze.xlsx", true)]
    [InlineData("PRESENZE.XLSX", true)]
    [InlineData("presenze.xls", false)]
    [InlineData("presenze.xlsx.pdf", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyExcelFilesAreConsidered(string? path, bool expected)
    {
        Assert.Equal(expected, WorkbookDrop.LooksLikeWorkbook(path));
    }

    [Fact]
    public void TheFirstUsableDroppedFileWinsAndTheOthersAreIgnored()
    {
        var folder = Directory.CreateTempSubdirectory("presenzeflow-drop");
        try
        {
            var workbook = Path.Combine(folder.FullName, "presenze.xlsx");
            var secondWorkbook = Path.Combine(folder.FullName, "altro.xlsx");
            var notAWorkbook = Path.Combine(folder.FullName, "relazione.pdf");
            File.WriteAllText(workbook, "contenuto");
            File.WriteAllText(secondWorkbook, "contenuto");
            File.WriteAllText(notAWorkbook, "contenuto");

            // Cartelle, file di altro tipo e file inesistenti non devono passare.
            Assert.Null(WorkbookDrop.FirstWorkbookPath(null));
            Assert.Null(WorkbookDrop.FirstWorkbookPath(Array.Empty<string>()));
            Assert.Null(WorkbookDrop.FirstWorkbookPath(new[] { folder.FullName }));
            Assert.Null(WorkbookDrop.FirstWorkbookPath(new[] { notAWorkbook }));
            Assert.Null(WorkbookDrop.FirstWorkbookPath(new[] { Path.Combine(folder.FullName, "assente.xlsx") }));

            // Il primo foglio Excel realmente presente è quello importato.
            Assert.Equal(
                workbook,
                WorkbookDrop.FirstWorkbookPath(new[] { folder.FullName, notAWorkbook, workbook, secondWorkbook }));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /* ------------------------- Firma dell'eseguibile ---------------------- */

    [Fact]
    public void TheCertificateNameIsReadFromTheSubject()
    {
        Assert.Equal("DeGioia", ExecutableSignature.CommonNameOf("CN=DeGioia, O=DeGioia, OU=PresenzeFlow"));
        Assert.Equal("DeGioia", ExecutableSignature.CommonNameOf("cn=DeGioia"));
        Assert.Null(ExecutableSignature.CommonNameOf("O=DeGioia, OU=PresenzeFlow"));
        Assert.Null(ExecutableSignature.CommonNameOf("CN=   "));
        Assert.Null(ExecutableSignature.CommonNameOf(""));
        Assert.Null(ExecutableSignature.CommonNameOf(null));
    }

    [Fact]
    public void TheSignatureLineSaysWhoSignedTheFileAndUntilWhen()
    {
        var validUntil = new DateTime(2029, 10, 11);

        var signed = ExecutableSignature.FormatSigner("CN=DeGioia, O=DeGioia, OU=PresenzeFlow", validUntil);
        Assert.Contains("DeGioia", signed, StringComparison.Ordinal);
        Assert.Contains("11/10/2029", signed, StringComparison.Ordinal);
        Assert.Contains("firmato", signed, StringComparison.Ordinal);

        // Senza nome dell'editore la firma non è dichiarata: meglio dire che non c'è
        // che mostrare una riga vuota o un soggetto incomprensibile.
        Assert.Equal(ExecutableSignature.NotSignedText, ExecutableSignature.FormatSigner(null, validUntil));
        Assert.Equal(ExecutableSignature.NotSignedText, ExecutableSignature.FormatSigner("O=DeGioia", validUntil));
        Assert.Equal(ExecutableSignature.NotSignedText, ExecutableSignature.FormatSigner("", null));

        // Firma senza scadenza leggibile: si dichiara comunque l'editore.
        Assert.Contains("DeGioia", ExecutableSignature.FormatSigner("CN=DeGioia", null), StringComparison.Ordinal);
    }

    [Fact]
    public void ReadingTheSignatureOfTheRunningProgramNeverThrows()
    {
        // Sia con eseguibile firmato sia senza, la lettura non deve mai far fallire
        // l'apertura delle Impostazioni.
        var text = ExecutableSignature.DescribeCurrentExecutable();
        Assert.False(string.IsNullOrWhiteSpace(text));
    }
}
