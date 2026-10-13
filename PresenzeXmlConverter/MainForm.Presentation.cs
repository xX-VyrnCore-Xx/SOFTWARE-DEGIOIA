using System.Text.RegularExpressions;

namespace PresenzeXmlConverter;

internal sealed partial class MainForm
{
    // Colore di sfondo della fascia dei numeri di riga: deve coincidere con
    // RowHeadersDefaultCellStyle.BackColor, altrimenti la numerazione rimane
    // impressa su un rettangolo di un altro colore.
    private static readonly Color RowHeaderFill = Theme.Sunken;

    /// <summary>Indice della scheda "Controlli" fra le pagine dell'area di revisione.</summary>
    private const int ReviewTabIndex = 2;

    /// <summary>
    /// Testo mostrato quando la griglia non ha nessuna riga da mostrare: senza,
    /// l'area di revisione resta un rettangolo bianco e sembra che il programma
    /// non abbia caricato nulla.
    /// </summary>
    private const string EmptyMovementsText =
        "Importa un foglio Excel (oppure trascinalo qui) e genera l'anteprima per vedere i movimenti.";

    /// <summary>Stato vuoto quando è la ricerca a non trovare nulla.</summary>
    private const string NoMovementsFoundText = "Nessun movimento corrisponde alla ricerca.";

    private const int SearchFieldWidth = 220;

    /// <summary>Altezza della fascia di comandi in cima alla pagina di conversione.</summary>
    private const int ConversionBandHeight = 150;

    private readonly Label _requestMetricLabel = new();
    private readonly Label _employeeMetricLabel = new();
    private readonly Label _previewTitleLabel = new();
    private readonly Label _previewSubtitleLabel = new();
    private readonly Label _previewCountLabel = new();
    private readonly RoundedField _previewSearch = new();
    private readonly IconButton _copyXmlButton = new("Copia XML", Glyph.Code);
    private readonly StatusPill _reviewStatusBadge = new();
    private IconButton? _conversionGenerateButton;
    private bool _isApplyingFilter;
    private bool _hasWarnings;

    private void BuildLayout()
    {
        // Il titolo della finestra non si tocca qui: lo assegna il costruttore con
        // la versione in esecuzione (WindowCaption). Rimetterlo senza versione
        // annullava quella assegnazione.
        MinimumSize = new Size(SetupCardLayout.MinimumWindowWidth, SetupCardLayout.MinimumWindowHeight);
        Size = new Size(1360, 860);
        BackColor = Theme.Canvas;
        Icon = _applicationIcon;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18, 14, 18, 10),
            BackColor = Theme.Canvas
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        Controls.Add(root);

        root.Controls.Add(BuildBrandHeader(), 0, 0);

        // Pagina di conversione: in alto una fascia con i quattro comandi del lavoro
        // (azienda, file, riepilogo, azioni), sotto l'area di revisione a tutta larghezza.
        // Prima i comandi stavano in una colonna di 300 px che toglieva spazio alla
        // tabella e tagliava i testi; ora la tabella usa tutta la larghezza.
        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, ConversionBandHeight));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(workspace, 0, 1);
        workspace.Controls.Add(BuildConversionBand(), 0, 0);

        var reviewCard = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            Radius = 14,
            Padding = new Padding(18, 14, 18, 14),
            Margin = Padding.Empty
        };
        workspace.Controls.Add(reviewCard, 0, 1);
        var reviewLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        reviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        reviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        reviewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        reviewCard.Controls.Add(reviewLayout);

        reviewLayout.Controls.Add(BuildPreviewHeading(), 0, 0);
        reviewLayout.Controls.Add(BuildPreviewToolbar(), 0, 1);
        reviewLayout.Controls.Add(BuildPreview(), 0, 2);
        root.Controls.Add(BuildFooter(), 0, 2);
        // La schermata di conversione appena costruita entra nella struttura a
        // sezioni, con la barra di navigazione a sinistra (MainForm.Shell.cs).
        BuildShell(root, workspace);
        UpdateWorkflowSummary();
        UpdateButtons();
    }

    /// <summary>
    /// Fascia dei comandi: quattro schede affiancate. L'ordine è quello del lavoro:
    /// azienda, file sorgente, riepilogo di ciò che è stato letto, azioni.
    /// </summary>
    private Control BuildConversionBand()
    {
        var band = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        band.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19));
        band.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));
        band.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        band.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
        band.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // 01 Azienda.
        var company = NewBandCard(new Padding(0, 0, 10, 12));
        var companyLayout = NewBandLayout(company, [20, 44, 20]);
        companyLayout.Controls.Add(CreateSectionRow("01   AZIENDA", string.Empty), 0, 0);
        _companyCode.MaxLength = 6;
        _companyCode.InputFont = Theme.Fonts.Field;
        _companyCode.CharacterCasing = CharacterCasing.Upper;
        _companyCode.Placeholder = "ABC123";
        _companyCode.Input.AccessibleName = "Codice azienda ufficiale PAGHE";
        _companyCode.Dock = DockStyle.Fill;
        _companyCode.Margin = new Padding(0, 4, 0, 4);
        Describe(_companyCode, "Codice azienda Zucchetti", "Codice azienda/ente PAGHE Infinity, fino a 6 caratteri.");
        _companyCode.TextChanged += (_, _) =>
        {
            InvalidatePreview();
            // Salvataggio automatico: le preferenze non si perdono se l'app si chiude
            // senza che il campo perda il focus.
            _companyCodeSaveDebounce.Stop();
            _companyCodeSaveDebounce.Start();
            UpdateCompanyHintLabel();
        };
        _companyCode.Leave += (_, _) => SaveCompanyCode();
        companyLayout.Controls.Add(_companyCode, 0, 1);
        _companyHintLabel = new Label
        {
            Text = DefaultCompanyHint,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Tiny,
            AutoEllipsis = true,
            Margin = Padding.Empty
        };
        companyLayout.Controls.Add(_companyHintLabel, 0, 2);
        band.Controls.Add(company, 0, 0);

        // 02 File sorgente.
        var file = NewBandCard(new Padding(0, 0, 10, 12));
        var fileLayout = NewBandLayout(file, [20, -1, 30]);
        fileLayout.Controls.Add(CreateSectionRow("02   FILE SORGENTE", "formato .xlsx / .xlsm"), 0, 0);
        _conversionDropZone = new DropZone { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
        _conversionDropZone.Click += async (_, _) => await ImportWorkbookAsync();
        _conversionDropZone.FileDropped += async path => await LoadWorkbookAsync(path);
        Describe(
            _conversionDropZone,
            "Seleziona file Excel",
            "Apre un foglio .xlsx da convertire in XML TRRIPW. Puoi anche trascinare il file qui o sulla finestra.");
        fileLayout.Controls.Add(_conversionDropZone, 0, 1);

        // Foglio in uso: mostra il nome e, quando il file ne ha più di uno,
        // permette di passare a un altro senza reimportare il file.
        _sheetButton.Text = "Foglio: nessun file importato";
        Theme.StyleButton(_sheetButton, Theme.ButtonTone.Neutral, compact: true);
        _sheetButton.TextAlign = ContentAlignment.MiddleLeft;
        _sheetButton.AutoEllipsis = true;
        _sheetButton.Dock = DockStyle.Fill;
        _sheetButton.Margin = new Padding(0);
        _sheetButton.Enabled = false;
        _sheetButton.Click += (_, _) => ShowSheetMenu();
        _sheetButton.AccessibleName = "Foglio di lavoro letto";
        fileLayout.Controls.Add(_sheetButton, 0, 2);
        band.Controls.Add(file, 1, 0);

        // Riepilogo di ciò che è stato letto.
        var summary = NewBandCard(new Padding(0, 0, 10, 12));
        var summaryLayout = NewBandLayout(summary, [20, -1, -1]);
        summaryLayout.Controls.Add(CreateSectionRow("RIEPILOGO", string.Empty), 0, 0);
        summaryLayout.Controls.Add(CreateSummaryLine(_requestMetricLabel, "richieste", Theme.BrandInk), 0, 1);
        summaryLayout.Controls.Add(CreateSummaryLine(_employeeMetricLabel, "dipendenti", Theme.SuccessInk), 0, 2);
        band.Controls.Add(summary, 2, 0);

        // Azioni.
        var actions = NewBandCard(new Padding(0, 0, 0, 12));
        var actionsLayout = NewBandLayout(actions, [-1, 32]);
        _conversionGenerateButton = new IconButton("Genera anteprima", Glyph.Arrow, Theme.ButtonTone.Positive)
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8)
        };
        _conversionGenerateButton.Click += (_, _) => GeneratePreview();
        Describe(_conversionGenerateButton, "Genera anteprima XML", "Genera i movimenti giornalieri e l'anteprima XML (Ctrl+G).");
        actionsLayout.Controls.Add(_conversionGenerateButton, 0, 0);

        var secondary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        secondary.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var mappingButton = new IconButton("Codici", Glyph.Code)
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 4, 0)
        };
        mappingButton.Click += (_, _) => EditMappings();
        Describe(mappingButton, "Codici PAGHE e giustificativi", "Codici PAGHE e giustificativi delle assenze (es. Ferie → F01)");
        secondary.Controls.Add(mappingButton, 0, 0);
        var rulesButton = new IconButton("Regole", Glyph.Gear)
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 0, 0, 0)
        };
        rulesButton.Click += (_, _) => OpenSettings();
        Describe(rulesButton, "Regole di conversione", "Ore per giornata, sabato lavorativo e chiusure aziendali");
        secondary.Controls.Add(rulesButton, 1, 0);
        actionsLayout.Controls.Add(secondary, 0, 1);
        band.Controls.Add(actions, 3, 0);

        return band;
    }

    private static SurfacePanel NewBandCard(Padding margin) => new()
    {
        Dock = DockStyle.Fill,
        SurfaceFill = Theme.Surface,
        SurfaceBorder = Theme.Divider,
        Radius = 14,
        Padding = new Padding(16, 12, 16, 12),
        Margin = margin
    };

    /// <summary>Una colonna di righe nella scheda: altezza fissa in pixel, -1 per la riga elastica.</summary>
    private static TableLayoutPanel NewBandLayout(SurfacePanel card, int[] rows)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = rows.Length,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in rows)
        {
            layout.RowStyles.Add(height < 0
                ? new RowStyle(SizeType.Percent, 100)
                : new RowStyle(SizeType.Absolute, height));
        }
        card.Controls.Add(layout);
        return layout;
    }

    /// <summary>Numero grande e didascalia sulla stessa riga ("8  richieste").</summary>
    private static Control CreateSummaryLine(Label number, string caption, Color accent)
    {
        var line = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        line.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        line.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        number.Text = "0";
        number.AutoSize = true;
        number.Anchor = AnchorStyles.Left;
        number.ForeColor = accent;
        number.Font = Theme.Fonts.Stat;
        number.Margin = new Padding(0, 0, 8, 0);
        line.Controls.Add(number, 0, 0);
        line.Controls.Add(new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Small,
            AutoEllipsis = true,
            Margin = Padding.Empty
        }, 1, 0);
        return line;
    }

    private IconButton? _headerUpdatesButton;

    /// <summary>
    /// Intestazione scura. Tre zone, ciascuna dimensionata dal proprio contenuto (nessuna
    /// larghezza fissa che a DPI alti taglia il testo): marchio e titolo a sinistra, la
    /// ricerca al centro (elastica, al più 460 px), a destra lo stato dell'anteprima e due
    /// pulsanti a icona con descrizione al passaggio del mouse.
    /// </summary>
    private Control BuildBrandHeader()
    {
        var header = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.HeaderFill,
            SurfaceBorder = Theme.HeaderFill,
            Radius = 14,
            Margin = new Padding(0, 0, 0, 14)
        };
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.HeaderFill,
            Margin = Padding.Empty,
            Padding = new Padding(16, 0, 14, 0)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Marchio + titolo.
        var brand = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.HeaderFill,
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty
        };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        brand.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        brand.Controls.Add(new PictureBox
        {
            Image = _applicationIcon.ToBitmap(),
            Size = new Size(40, 40),
            SizeMode = PictureBoxSizeMode.Zoom,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 12, 0)
        }, 0, 0);
        var titles = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.HeaderFill,
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty
        };
        titles.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titles.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titles.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titles.Controls.Add(new Label
        {
            Text = "PresenzeFlow",
            AutoSize = true,
            ForeColor = Theme.HeaderInk,
            Font = Theme.Fonts.HeaderTitle,
            Margin = new Padding(0, 0, 0, -4)
        }, 0, 0);
        titles.Controls.Add(new Label
        {
            Text = "Importa · Verifica · Esporta in XML TRRIPW",
            AutoSize = true,
            ForeColor = Theme.HeaderMuted,
            Font = Theme.Fonts.Small,
            Margin = new Padding(2, 0, 0, 0)
        }, 0, 1);
        brand.Controls.Add(titles, 1, 0);
        body.Controls.Add(brand, 0, 0);

        body.Controls.Add(BuildHeaderSearch(), 1, 0);

        // Stato dell'anteprima e azioni sempre a portata di mano.
        var side = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.HeaderFill,
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty
        };
        for (var column = 0; column < 3; column++)
        {
            side.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        }
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _reviewStatusBadge.AutoSize = true;
        _reviewStatusBadge.Height = 30;
        _reviewStatusBadge.Anchor = AnchorStyles.None;
        _reviewStatusBadge.Margin = new Padding(0, 0, 14, 0);
        _reviewStatusBadge.AccessibleName = "Stato dell'anteprima";
        _reviewStatusBadge.AccessibleRole = AccessibleRole.StaticText;
        side.Controls.Add(_reviewStatusBadge, 0, 0);

        _headerUpdatesButton = new IconButton(string.Empty, Glyph.Refresh, Theme.ButtonTone.OnDark)
        {
            Height = 36,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, 0, 8, 0)
        };
        _headerUpdatesButton.Click += async (_, _) => await CheckForUpdatesAsync(userInitiated: true);
        Describe(_headerUpdatesButton, "Aggiornamenti", "Verifica subito se è disponibile una nuova versione.");
        side.Controls.Add(_headerUpdatesButton, 1, 0);

        var headerSettings = new IconButton(string.Empty, Glyph.Gear, Theme.ButtonTone.OnDark)
        {
            Height = 36,
            Anchor = AnchorStyles.None,
            Margin = Padding.Empty
        };
        headerSettings.Click += (_, _) => OpenSettings();
        Describe(headerSettings, "Impostazioni", "Regole di conversione e aggiornamenti (Ctrl+,).");
        side.Controls.Add(headerSettings, 2, 0);
        body.Controls.Add(side, 2, 0);

        // Accento sfumato in cima all'header: la riga è fissa (4 px) e il corpo riempie il resto.
        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.HeaderFill,
            Margin = Padding.Empty
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 4));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(new AccentBar { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 0);
        shell.Controls.Add(body, 0, 1);
        header.Controls.Add(shell);
        return header;
    }

    /// <summary>
    /// Riga di sezione con etichetta a sinistra e breve guida a destra:
    /// il testo di aiuto non può finire sotto l'etichetta perché le due
    /// colonne sono separate.
    /// </summary>
    private static Control CreateSectionRow(string text, string hint)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Section,
            Margin = Padding.Empty
        };
        row.Controls.Add(label, 0, 0);

        var help = new Label
        {
            Text = hint,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Theme.FaintInk,
            Font = Theme.Fonts.Tiny,
            Margin = new Padding(6, 0, 0, 0)
        };
        row.Controls.Add(help, 1, 0);
        return row;
    }

    private Control BuildPreviewHeading()
    {
        var heading = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Margin = Padding.Empty };
        _previewTitleLabel.Text = "Area di revisione";
        _previewTitleLabel.AutoSize = true;
        _previewTitleLabel.Location = new Point(0, 2);
        _previewTitleLabel.Font = Theme.Fonts.CardTitle;
        _previewTitleLabel.ForeColor = Theme.Ink;
        heading.Controls.Add(_previewTitleLabel);
        _previewSubtitleLabel.Text = "Importa un foglio Excel per visualizzare e controllare i movimenti.";
        _previewSubtitleLabel.AutoSize = false;
        _previewSubtitleLabel.Location = new Point(0, 34);
        // Due righe di testo piccolo: nell'area di revisione stretta (barra di
        // navigazione + card di sinistra) la frase non sta sempre su una riga sola.
        _previewSubtitleLabel.Height = 32;
        _previewSubtitleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _previewSubtitleLabel.Font = Theme.Fonts.Small;
        _previewSubtitleLabel.ForeColor = Theme.MutedInk;
        heading.Controls.Add(_previewSubtitleLabel);
        return heading;
    }

    private Control BuildPreviewToolbar()
    {
        var bar = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.Sunken,
            SurfaceBorder = Theme.Divider,
            Radius = 12,
            Padding = new Padding(14, 6, 10, 6),
            Margin = new Padding(0, 2, 0, 8)
        };
        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Sunken,
            Margin = Padding.Empty
        };
        // Il conteggio prende lo spazio che resta; i comandi si dimensionano sul testo.
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _previewCountLabel.Text = "In attesa di un file";
        _previewCountLabel.Dock = DockStyle.Fill;
        _previewCountLabel.TextAlign = ContentAlignment.MiddleLeft;
        _previewCountLabel.AutoEllipsis = true;
        _previewCountLabel.Font = Theme.Fonts.SmallBold;
        _previewCountLabel.ForeColor = Theme.InkSecondary;
        toolbar.Controls.Add(_previewCountLabel, 0, 0);

        _previewSearch.Size = new Size(SearchFieldWidth, 34);
        _previewSearch.Placeholder = "Cerca nei movimenti";
        _previewSearch.Margin = new Padding(0, 0, 10, 0);
        _previewSearch.Anchor = AnchorStyles.Right;
        _previewSearch.Input.AccessibleName = "Cerca nei movimenti";
        Describe(
            _previewSearch,
            "Cerca nei movimenti",
            "Filtra i movimenti: più parole in qualsiasi ordine, senza accenti né maiuscole (Ctrl+F).");
        _previewSearch.TextChanged += (_, _) => FilterMovements();

        var toolbarActions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Sunken,
            Margin = Padding.Empty
        };
        for (var column = 0; column < 3; column++)
        {
            toolbarActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        }
        toolbarActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _copyXmlButton.Anchor = AnchorStyles.Right;
        _copyXmlButton.Margin = new Padding(0, 0, 8, 0);
        _copyXmlButton.Click += (_, _) => CopyXmlToClipboard();
        Describe(_copyXmlButton, "Copia XML", "Copia l'anteprima XML negli appunti.");

        _exportButton.Anchor = AnchorStyles.Right;
        _exportButton.Margin = Padding.Empty;
        _exportButton.Click += (_, _) => ExportXml();
        Describe(
            _exportButton,
            "Scarica XML",
            "Salva il file XML TRRIPW nella cartella che scegli (Ctrl+S).");

        toolbarActions.Controls.Add(_previewSearch, 0, 0);
        toolbarActions.Controls.Add(_copyXmlButton, 1, 0);
        toolbarActions.Controls.Add(_exportButton, 2, 0);
        toolbar.Controls.Add(toolbarActions, 1, 0);
        bar.Controls.Add(toolbar);
        return bar;
    }

    private ThemedTabControl BuildPreview()
    {
        _previewTabs.Dock = DockStyle.Fill;
        _previewTabs.Margin = Padding.Empty;

        var movementPage = new Panel { BackColor = Theme.Surface, Padding = new Padding(7), Margin = Padding.Empty };
        _movementsGrid.RowHeadersVisible = true;
        _movementsGrid.RowHeadersWidth = 46;
        _movementsGrid.RowHeadersDefaultCellStyle.BackColor = RowHeaderFill;
        _movementsGrid.RowHeadersDefaultCellStyle.ForeColor = Theme.RowNumberInk;
        _movementsGrid.RowHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _movementsGrid.RowHeadersDefaultCellStyle.Font = Theme.Fonts.MonoSmall;
        _movementsGrid.Columns.Add(CreateGridColumn("employee", "Dipendente", 148, true));
        _movementsGrid.Columns.Add(CreateGridColumn("code", "Codice PAGHE", 100, false));
        _movementsGrid.Columns.Add(CreateGridColumn("date", "Data movimento", 103, false));
        _movementsGrid.Columns.Add(CreateGridColumn("policy", "Richiesta originale", 146, true));
        _movementsGrid.Columns.Add(CreateGridColumn("reason", "Codice", 65, false));
        _movementsGrid.Columns.Add(CreateGridColumn("hours", "Ore", 47, false));
        _movementsGrid.Columns.Add(CreateGridColumn("minutes", "Min", 44, false));
        _movementsGrid.Columns.Add(CreateGridColumn("source", "Riga Excel", 67, true));
        // Con la barra di navigazione l'area di revisione è stretta: la barra di
        // scorrimento orizzontale nativa è grigia di sistema (fuori tema), quindi non
        // la si usa. Quando le colonne non entrano si nascondono le meno importanti
        // (Riga Excel, poi Richiesta originale); restano nel dato e nell'XML.
        _movementsGrid.ScrollBars = ScrollBars.Vertical;
        _movementsGrid.Resize += (_, _) => AdaptMovementColumns();
        _movementsGrid.RowPostPaint += (_, args) =>
        {
            var bounds = new Rectangle(
                args.RowBounds.Left,
                args.RowBounds.Top,
                _movementsGrid.RowHeadersWidth - 8,
                args.RowBounds.Height);
            // Prima si cancella il numero già disegnato dalla griglia: senza questa
            // riga in capo restano sovrapposti ("1" sopra "01").
            using (var background = new SolidBrush(RowHeaderFill))
            {
                args.Graphics.FillRectangle(background, bounds);
            }
            TextRenderer.DrawText(
                args.Graphics,
                (args.RowIndex + 1).ToString("D2"),
                Theme.Fonts.MonoSmall,
                bounds,
                Theme.RowNumberInk,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        };
        _movementsGrid.CellFormatting += FormatMovementCell;
        _movementsGrid.CellValueChanged += (_, args) =>
        {
            if (!_isPopulatingPreview && args.RowIndex >= 0)
            {
                ScheduleXmlRefresh();
            }
        };
        _movementsGrid.CellEndEdit += (_, _) =>
        {
            _xmlRefreshDebounce.Stop();
            RefreshXmlFromPreview();
        };
        _movementsGrid.EmptyStateText = EmptyMovementsText;
        _movementsGrid.AccessibleName = "Movimenti generati";
        _movementsGrid.AccessibleDescription = "Movimenti giornalieri dell'anteprima: le colonne dei codici e delle ore sono modificabili.";
        _xmlPreview.AccessibleName = "Anteprima XML TRRIPW";
        _previewTabs.AccessibleName = "Area di revisione";
        movementPage.Controls.Add(_movementsGrid);

        var xmlPage = new Panel { BackColor = Theme.EditorFill, Padding = new Padding(0), Margin = Padding.Empty };
        _xmlPreview.Dock = DockStyle.Fill;
        _xmlPreview.ReadOnly = true;
        _xmlPreview.BorderStyle = BorderStyle.None;
        _xmlPreview.BackColor = Theme.EditorFill;
        _xmlPreview.ForeColor = Theme.EditorInk;
        _xmlPreview.Font = Theme.Fonts.Mono;
        _xmlPreview.WordWrap = false;
        _xmlPreview.DetectUrls = false;
        _xmlPreview.Margin = Padding.Empty;
        xmlPage.Controls.Add(_xmlPreview);
        _previewTabs.AddPage("Movimenti  ·  0", movementPage);
        _previewTabs.AddPage("Anteprima XML", xmlPage);
        _previewTabs.AddPage("Controlli", BuildReviewPanel());
        _previewTabs.SelectedIndex = 0;
        return _previewTabs;
    }

    /// <summary>
    /// Mostra le colonne facoltative solo se c'è posto per le larghezze minime di
    /// tutte quelle visibili: così non compare mai la barra orizzontale nativa.
    /// </summary>
    private void AdaptMovementColumns()
    {
        var grid = _movementsGrid;
        if (grid.ClientSize.Width < 50 || grid.Columns.Count == 0)
        {
            return;
        }
        var optional = new[] { "policy", "source" };
        var available = grid.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
        var required = grid.RowHeadersVisible ? grid.RowHeadersWidth : 0;
        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (!optional.Contains(column.Name) && column.Visible)
            {
                required += column.MinimumWidth;
            }
        }
        foreach (var name in optional)
        {
            var column = grid.Columns[name];
            if (column is null)
            {
                continue;
            }
            var fits = required + column.MinimumWidth <= available;
            if (fits)
            {
                required += column.MinimumWidth;
            }
            if (column.Visible != fits)
            {
                column.Visible = fits;
            }
        }
    }

    private static DataGridViewTextBoxColumn CreateGridColumn(
        string name,
        string title,
        float fillWeight,
        bool readOnly,
        bool hidden = false)
    {
        var column = new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = title,
            FillWeight = fillWeight,
            MinimumWidth = name is "hours" or "minutes" ? 42 : 58,
            ReadOnly = readOnly,
            Visible = !hidden,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        if (!readOnly && name == "code") column.MaxInputLength = 7;
        if (!readOnly && name == "reason") column.MaxInputLength = 3;
        if (name is "hours" or "minutes")
        {
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        if (name is "code" or "reason")
        {
            column.DefaultCellStyle.Font = Theme.Fonts.Code;
            column.DefaultCellStyle.ForeColor = Theme.CodeInk;
        }
        return column;
    }

    private Control BuildFooter()
    {
        // Regola sottile in alto e due colonne separate: lo stato occupa tutto
        // lo spazio a sinistra con ellissi, lo stato degli aggiornamenti resta a destra e i
        // due testi non si sovrappongono mai.
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Divider }, 0, 0);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Canvas,
            Padding = new Padding(2, 0, 2, 0),
            Margin = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.AutoEllipsis = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Text = "●   Pronto  ·  Formato di esportazione: XML TRRIPW";
        _statusLabel.Font = Theme.Fonts.Small;
        _statusLabel.ForeColor = Theme.MutedInk;
        content.Controls.Add(_statusLabel, 0, 0);

        // Stato degli aggiornamenti sempre visibile: si vede a colpo d'occhio se
        // il collegamento alle Release è attivo.
        _updateStatusLabel.AutoSize = true;
        _updateStatusLabel.Dock = DockStyle.Right;
        _updateStatusLabel.TextAlign = ContentAlignment.MiddleRight;
        _updateStatusLabel.Text = "Aggiornamenti · verifica in corso";
        _updateStatusLabel.Font = Theme.Fonts.Small;
        _updateStatusLabel.ForeColor = Theme.MutedInk;
        content.Controls.Add(_updateStatusLabel, 1, 0);
        footer.Controls.Add(content, 0, 1);
        return footer;
    }

    private static void FormatMovementCell(object? sender, DataGridViewCellFormattingEventArgs args)
    {
        if (sender is not DataGridView grid || args.RowIndex < 0 || args.ColumnIndex < 0)
        {
            return;
        }
        var cellStyle = args.CellStyle;
        if (cellStyle is null)
        {
            return;
        }
        var columnName = grid.Columns[args.ColumnIndex].Name;
        if (columnName == "code" || columnName == "reason")
        {
            cellStyle.Font = Theme.Fonts.Code;
            cellStyle.ForeColor = Theme.CodeInk;
            // Il testo di una cella selezionata deve restare leggibile: con lo
            // stesso tono del fondo di selezione diventava invisibile.
            cellStyle.SelectionForeColor = Theme.Ink;
        }
        else if (columnName == "date")
        {
            cellStyle.ForeColor = Theme.DateInk;
        }
        else if (columnName is "hours" or "minutes")
        {
            cellStyle.ForeColor = Theme.NumberInk;
            cellStyle.Font = Theme.Fonts.Number;
        }
    }

    /// <summary>
    /// Filtra i movimenti con più parole in qualsiasi ordine, ignorando accenti
    /// e maiuscole. Ogni parola deve comparire da qualche parte nella riga, così
    /// "mario ferie 12/05" restringe davvero il risultato.
    /// </summary>
    private void FilterMovements()
    {
        if (_isApplyingFilter || _movementsGrid.Rows.Count == 0)
        {
            return;
        }

        _isApplyingFilter = true;
        var terms = MovementSearch.SplitTerms(_previewSearch.Value);
        _movementsGrid.EmptyStateText = terms.Count == 0 ? EmptyMovementsText : NoMovementsFoundText;
        var visible = 0;
        try
        {
            foreach (DataGridViewRow row in _movementsGrid.Rows)
            {
                var matches = terms.Count == 0 || MovementSearch.Matches(RowSearchHaystack(row), terms);
                row.Visible = matches;
                if (matches) visible++;
            }
        }
        finally
        {
            _isApplyingFilter = false;
        }
        _previewCountLabel.Text =
            terms.Count == 0
                ? $"{visible:N0} movimenti   ·   tutti inclusi nell’XML"
                : $"{visible:N0} trovati   ·   {_movementsGrid.Rows.Count:N0} inclusi nell’XML";
    }

    /// <summary>Testo su cui viene cercata una riga della griglia (valori reali, anche appena modificati).</summary>
    private static string RowSearchHaystack(DataGridViewRow row) => MovementSearch.Describe(
        CellText(row, "employee"),
        CellText(row, "code"),
        CellText(row, "date"),
        CellText(row, "policy"),
        CellText(row, "reason"),
        CellText(row, "hours"),
        CellText(row, "minutes"),
        CellText(row, "source"));

    private void UpdateWorkflowSummary()
    {
        var workers = _requests.Select(request => request.EmployeeName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        _requestMetricLabel.Text = _requests.Count.ToString("N0");
        _employeeMetricLabel.Text = workers.ToString("N0");
        UpdateSheetButton();

        if (_hasWarnings)
        {
            _reviewStatusBadge.SetStatus("●   REVISIONE RICHIESTA", Theme.PillWarningFill, Theme.PillWarningInk);
        }
        else if (!string.IsNullOrWhiteSpace(_xml))
        {
            _reviewStatusBadge.SetStatus("●   ANTEPRIMA PRONTA", Theme.PillReadyFill, Theme.PillReadyInk);
        }
        else
        {
            _reviewStatusBadge.SetStatus("●   PRONTO PER INIZIARE", Theme.PillIdleFill, Theme.PillIdleInk);
        }
    }

    private void UpdatePreviewSummary(IReadOnlyList<DailyMovement> movements)
    {
        var totals = MovementChecks.Summarize(movements);
        _previewTitleLabel.Text = "Revisione movimenti";
        _previewSubtitleLabel.Text =
            $"{totals.Employees:N0} dipendenti    ·    {totals.Days:N0} giornate coinvolte    ·    " +
            $"{totals.TotalText} assegnati    ·    {_generationOptions.HoursPerDay:0.##} h al giorno";
        _previewTabs.SetPageText(0, $"Movimenti  ·  {movements.Count:N0}");
        _previewCountLabel.Text = $"{movements.Count:N0} movimenti   ·   tutti inclusi nell’XML";
        _previewSearch.Clear();
        UpdateWorkflowSummary();
    }

    private void ApplyXmlSyntaxHighlighting()
    {
        var text = _xmlPreview.Text;
        if (text.Length == 0)
        {
            return;
        }

        // Su XML molto grandi l'evidenziazione costa più del suo valore:
        // oltre il limite si mostra il testo in colore uniforme.
        const int highlightingLimit = 400_000;
        if (text.Length > highlightingLimit)
        {
            _xmlPreview.SelectAll();
            _xmlPreview.SelectionColor = Theme.EditorInk;
            _xmlPreview.Select(0, 0);
            return;
        }

        var selectionStart = _xmlPreview.SelectionStart;
        _xmlPreview.SuspendLayout();
        _xmlPreview.SelectAll();
        _xmlPreview.SelectionColor = Theme.EditorInk;
        foreach (Match match in Regex.Matches(text, @"</?[\w:.-]+|/?>"))
        {
            _xmlPreview.Select(match.Index, match.Length);
            _xmlPreview.SelectionColor = Theme.EditorTag;
        }
        foreach (Match match in Regex.Matches(text, "\"[^\"]*\"|'[^']*'"))
        {
            _xmlPreview.Select(match.Index, match.Length);
            _xmlPreview.SelectionColor = Theme.EditorString;
        }
        foreach (Match match in Regex.Matches(text, @"(?<=>)[^<\r\n]+(?=<)"))
        {
            _xmlPreview.Select(match.Index, match.Length);
            _xmlPreview.SelectionColor = Theme.EditorValue;
        }
        _xmlPreview.Select(Math.Min(selectionStart, _xmlPreview.TextLength), 0);
        _xmlPreview.SelectionColor = Theme.EditorInk;
        _xmlPreview.ResumeLayout();
    }

    private void CopyXmlToClipboard()
    {
        if (string.IsNullOrWhiteSpace(_xml))
        {
            return;
        }
        try
        {
            Clipboard.SetText(_xml);
            SetStatus("●   XML copiato negli appunti", false);
        }
        catch (System.Runtime.InteropServices.ExternalException exception)
        {
            ShowError("Copia XML non riuscita", exception);
        }
    }
}
