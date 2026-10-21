namespace PresenzeXmlConverter;

internal sealed partial class MainForm
{
    /// <summary>Sezione «Rinomina PDF»: titolo e, sotto, il pannello che legge i codici e riscrive i file.</summary>
    private Control BuildPdfSection()
    {
        var page = _pdfSection;
        page.RowCount = 2;
        page.ColumnCount = 1;
        page.Padding = new Padding(0, 0, 4, 0);
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(BuildSectionHeading(
            "Rinomina PDF per codice cliente",
            "Scegli o trascina uno ZIP o dei PDF: il codice cliente si legge in fondo a ogni pagina."), 0, 0);
        page.Controls.Add(new PdfRenamePanel { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 1);
        return page;
    }
}
