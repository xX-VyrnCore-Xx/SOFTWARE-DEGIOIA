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
    /// Larghezza del contenuto della card di sinistra: la colonna è fissa (282 px)
    /// meno margine e spaziatura interna della card.
    /// </summary>
    private const int SetupContentWidth = 226;

    /// <summary>
    /// Aggiunge una riga all'impilamento verticale della card con l'altezza
    /// dichiarata. Il margine è quello della riga: l'altezza utile è la parte
    /// che resta.
    /// </summary>
    private static void AddSetupRow(Control stack, Control control, int height, Padding margin = default)
    {
        control.Dock = DockStyle.None;
        control.Margin = margin;
        control.Size = new Size(
            Math.Max(0, SetupContentWidth - margin.Horizontal),
            Math.Max(0, height - margin.Vertical));
        stack.Controls.Add(control);
    }

    /// <summary>
    /// Adatta l'impilamento alla card: le larghezze seguono lo spazio visibile,
    /// le altezze restano quelle dichiarate riga per riga. Un FlowLayoutPanel
    /// TopDown non ridimensiona mai i figli, quindi nessuna riga può essere
    /// compressa e nessun testo viene tagliato quando la finestra è bassa.
    /// </summary>
    private static void SyncSetupStack(Control container, FlowLayoutPanel stack)
    {
        var width = container.ClientSize.Width;
        if (width <= 0)
        {
            return;
        }
        foreach (Control child in stack.Controls)
        {
            var childWidth = Math.Max(0, width - child.Margin.Horizontal);
            if (child.Width != childWidth)
            {
                child.Width = childWidth;
            }
        }
        var height = stack.PreferredSize.Height;
        if (stack.Width != width || stack.Height != height)
        {
            stack.Size = new Size(width, height);
        }
    }

    private readonly Label _requestMetricLabel = new();
    private readonly Label _employeeMetricLabel = new();
    private readonly Label _previewTitleLabel = new();
    private readonly Label _previewSubtitleLabel = new();
    private readonly Label _previewCountLabel = new();
    private readonly RoundedField _previewSearch = new();
    private readonly Button _copyXmlButton = new();
    private readonly StatusPill _reviewStatusBadge = new();
    private bool _isApplyingFilter;
    private bool _hasWarnings;

    private void BuildLayout()
    {
        Text = "PresenzeFlow";
        MinimumSize = new Size(1024, 650);
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

        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 282));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Riga dichiarata esplicitamente: senza RowStyles la tabella ricade su una
        // riga autosize che non combacia con l'altezza reale della cella, così la
        // colonna di sinistra può ricevere meno spazio di quanto ne ha a
        // disposizione e i testi dei passi finiscono tagliati.
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(workspace, 0, 1);

        var setupCard = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 18, 20, 18),
            Margin = new Padding(0, 0, 16, 0)
        };
        // La card scorre quando la finestra è bassa. L'impilamento verticale è un
        // FlowLayoutPanel TopDown con altezze esplicite per ogni riga: il flusso
        // non ridimensiona MAI i figli, quindi le righe non possono essere
        // compresse e i testi non finiscono tagliati (sui runner CI e sui
        // portatili con lo schermo basso una tabella a righe fisse si schiacciava).
        var setupScroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        var setup = new FlowLayoutPanel
        {
            Location = new Point(0, 0),
            Size = new Size(SetupContentWidth, 1),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        setupScroll.Controls.Add(setup);
        // La larghezza segue la card, le altezze restano quelle dichiarate qui sotto.
        setupScroll.Layout += (_, _) => SyncSetupStack(setupScroll, setup);
        setupCard.Controls.Add(setupScroll);
        workspace.Controls.Add(setupCard, 0, 0);

        var title = new Label
        {
            Text = "Nuova conversione",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Ink,
            Font = Theme.Fonts.CardTitle
        };
        AddSetupRow(setup, title, 46);

        AddSetupRow(setup, CreateSectionRow("01   AZIENDA ZUCCHETTI", "fino a 6 caratteri"), 24);
        _companyCode.MaxLength = 6;
        _companyCode.InputFont = Theme.Fonts.Field;
        _companyCode.CharacterCasing = CharacterCasing.Upper;
        _companyCode.Placeholder = "es. ABC123";
        _companyCode.TextChanged += (_, _) =>
        {
            InvalidatePreview();
            UpdateWorkflowSummary();
            // Salvataggio automatico: le preferenze non si perdono se l'app si chiude
            // senza che il campo perda il focus.
            _companyCodeSaveDebounce.Stop();
            _companyCodeSaveDebounce.Start();
        };
        _companyCode.Leave += (_, _) => SaveCompanyCode();
        AddSetupRow(setup, _companyCode, 44, new Padding(0, 2, 0, 8));

        AddSetupRow(setup, CreateSectionRow("02   FILE SORGENTE", "formato .xlsx"), 24);
        var importButton = Theme.CreateButton("SELEZIONA FILE EXCEL", Theme.ButtonTone.Primary);
        importButton.Click += async (_, _) => await ImportWorkbookAsync();
        AddSetupRow(setup, importButton, 46, new Padding(0, 2, 0, 8));

        _fileLabel.AutoSize = false;
        _fileLabel.AutoEllipsis = true;
        _fileLabel.TextAlign = ContentAlignment.MiddleLeft;
        _fileLabel.Text = "Nessun file selezionato";
        _fileLabel.Font = Theme.Fonts.SmallBold;
        _fileLabel.ForeColor = Theme.InkSecondary;
        AddSetupRow(setup, _fileLabel, 28);

        // Foglio in uso: mostra il nome e, quando il file ne ha più di uno,
        // permette di passare a un altro senza reimportare il file.
        _sheetButton.Text = "Foglio: nessun file importato";
        Theme.StyleButton(_sheetButton, Theme.ButtonTone.Neutral, compact: true);
        _sheetButton.TextAlign = ContentAlignment.MiddleLeft;
        _sheetButton.Enabled = false;
        _sheetButton.Click += (_, _) => ShowSheetMenu();
        AddSetupRow(setup, _sheetButton, 28, new Padding(0, 1, 0, 1));

        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        metrics.Controls.Add(CreateMetricTile("RICHIESTE", _requestMetricLabel, Theme.BrandInk, Theme.BrandSoft), 0, 0);
        metrics.Controls.Add(CreateMetricTile("DIPENDENTI", _employeeMetricLabel, Theme.SuccessInk, Theme.SuccessSoft), 1, 0);
        AddSetupRow(setup, metrics, 88, new Padding(0, 2, 0, 2));

        var mappingButton = Theme.CreateButton("Codici e giustificativi", Theme.ButtonTone.Neutral);
        mappingButton.Click += (_, _) => EditMappings();
        AddSetupRow(setup, mappingButton, 40, new Padding(0, 3, 0, 3));

        var aiRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        aiRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        aiRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        aiRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var aiButton = Theme.CreateButton("Configura AI locale", Theme.ButtonTone.Neutral, compact: true);
        aiButton.Dock = DockStyle.Fill;
        aiButton.Margin = new Padding(0, 3, 4, 3);
        aiButton.Click += async (_, _) => await ConfigureAiAsync();
        aiRow.Controls.Add(aiButton, 0, 0);

        var settingsButton = Theme.CreateButton("Impostazioni", Theme.ButtonTone.Neutral, compact: true);
        settingsButton.Dock = DockStyle.Fill;
        settingsButton.Margin = new Padding(4, 3, 0, 3);
        settingsButton.Click += (_, _) => OpenSettings();
        aiRow.Controls.Add(settingsButton, 1, 0);
        AddSetupRow(setup, aiRow, 40);

        AddSetupRow(setup, BuildWorkflowSteps(), 82, new Padding(0, 8, 0, 4));

        _generateButton.Text = "Genera anteprima   →";
        Theme.StyleButton(_generateButton, Theme.ButtonTone.Positive);
        _generateButton.Click += (_, _) => GeneratePreview();
        AddSetupRow(setup, _generateButton, 50, new Padding(0, 4, 0, 0));
        // Prima misura esplicita: l'aggiunta dei figli non fa scattare il Layout
        // del pannello scrollabile, quindi la dimensione va calcolata qui.
        SyncSetupStack(setupScroll, setup);

        var reviewCard = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 14, 16, 14),
            Margin = Padding.Empty
        };
        workspace.Controls.Add(reviewCard, 1, 0);
        var reviewLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        reviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        reviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        reviewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        reviewCard.Controls.Add(reviewLayout);

        reviewLayout.Controls.Add(BuildPreviewHeading(), 0, 0);
        reviewLayout.Controls.Add(BuildPreviewToolbar(), 0, 1);
        reviewLayout.Controls.Add(BuildPreview(), 0, 2);
        root.Controls.Add(BuildFooter(), 0, 2);
        UpdateWorkflowSummary();
        UpdateButtons();
    }

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
            Margin = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 296));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var mark = new PictureBox
        {
            Image = _applicationIcon.ToBitmap(),
            Size = new Size(50, 50),
            SizeMode = PictureBoxSizeMode.Zoom,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Margin = new Padding(16, 15, 0, 0)
        };
        body.Controls.Add(mark, 0, 0);

        // Titolo e sottotitolo su righe distinte: non possono mai finire sopra
        // l'uno all'altra, qualunque sia il DPI o la larghezza della finestra.
        var titles = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.HeaderFill,
            Margin = Padding.Empty
        };
        titles.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        titles.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "PresenzeFlow",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.HeaderInk,
            Font = Theme.Fonts.HeaderTitle,
            Margin = Padding.Empty
        };
        titles.Controls.Add(title, 0, 1);

        var subtitle = new Label
        {
            Text = "Importa · Verifica · Esporta in XML TRRIPW",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.HeaderMuted,
            Font = Theme.Fonts.Small,
            Margin = new Padding(1, 0, 0, 4)
        };
        titles.Controls.Add(subtitle, 0, 2);
        body.Controls.Add(titles, 1, 0);

        // Azioni sempre a portata di mano nell'intestazione: aggiornamenti e
        // impostazioni non devono costringere a cercare fra le schede.
        var headerSide = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.HeaderFill,
            Margin = new Padding(0, 6, 4, 0)
        };
        headerSide.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerSide.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        headerSide.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headerActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        var headerSettings = Theme.CreateButton("Impostazioni", Theme.ButtonTone.Neutral, compact: true);
        headerSettings.Size = new Size(124, 28);
        headerSettings.Margin = new Padding(6, 0, 0, 0);
        headerSettings.Click += (_, _) => OpenSettings();
        var headerUpdates = Theme.CreateButton("Aggiornamenti", Theme.ButtonTone.Neutral, compact: true);
        headerUpdates.Size = new Size(124, 28);
        headerUpdates.Margin = Padding.Empty;
        headerUpdates.Click += async (_, _) => await CheckForUpdatesAsync(userInitiated: true);
        headerActions.Controls.Add(headerSettings);
        headerActions.Controls.Add(headerUpdates);
        headerSide.Controls.Add(headerActions, 0, 0);

        _reviewStatusBadge.Dock = DockStyle.Fill;
        _reviewStatusBadge.Margin = Padding.Empty;
        headerSide.Controls.Add(_reviewStatusBadge, 0, 1);
        body.Controls.Add(headerSide, 2, 0);

        // Accento sfumato in cima all'header: la card resta la stessa, ma con un
        // tocco di profondità in più. La riga è fissa (4 px) e il corpo riempie il resto.
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

    /// <summary>
    /// Le due caselle delle metriche: impilate su righe fisse, così il
    /// numero grande non può mai sforare la tile ad altezza ridotta.
    /// </summary>
    private static Panel CreateMetricTile(string caption, Label metric, Color accent, Color background)
    {
        var tile = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = background,
            SurfaceBorder = background,
            Radius = 10,
            Padding = new Padding(14, 8, 12, 8),
            Margin = new Padding(0, 0, 8, 0)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = background,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        // Riga fissa e non percentuale: con una riga percentuale il numero grande
        // veniva compresso a pochi pixel quando la card aveva meno spazio.
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        layout.Controls.Add(new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Section,
            Margin = Padding.Empty
        }, 0, 0);

        metric.Text = "0";
        metric.Dock = DockStyle.Fill;
        metric.TextAlign = ContentAlignment.MiddleLeft;
        metric.ForeColor = accent;
        metric.Font = Theme.Fonts.Metric;
        metric.Margin = Padding.Empty;
        layout.Controls.Add(metric, 0, 1);
        tile.Controls.Add(layout);
        return tile;
    }

    /// <summary>Mini-guida al flusso di lavoro, con cerchini numerati.</summary>
    private static Control BuildWorkflowSteps()
    {
        var steps = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            BackColor = Theme.Surface,
            Margin = new Padding(0, 8, 0, 4)
        };
        steps.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
        steps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        steps.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10));
        steps.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        steps.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        var importStep = new StepBadge("1", true) { Dock = DockStyle.Top, Margin = new Padding(0, 2, 0, 0) };
        steps.Controls.Add(importStep, 0, 0);
        steps.Controls.Add(new Label
        {
            Text = "Importa il file Excel",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.BrandInk,
            Font = Theme.Fonts.Step,
            Margin = Padding.Empty
        }, 1, 0);

        var reviewStep = new StepBadge("2", false) { Dock = DockStyle.Top, Margin = new Padding(0, 2, 0, 0) };
        steps.Controls.Add(reviewStep, 0, 1);
        steps.Controls.Add(new Label
        {
            Text = "Rivedi ed esporta l'XML",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Small,
            Margin = Padding.Empty
        }, 1, 1);
        return steps;
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
        _previewSubtitleLabel.Height = 22;
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
            Radius = 10,
            Padding = new Padding(12, 5, 12, 5),
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
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        // Riga percentuale esplicita: senza una riga dichiarata la tabella usa un
        // default che non combacia con l'altezza reale e i figli escono fuori.
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _previewCountLabel.Text = "In attesa di un file";
        _previewCountLabel.Dock = DockStyle.Fill;
        _previewCountLabel.TextAlign = ContentAlignment.MiddleLeft;
        _previewCountLabel.AutoEllipsis = true;
        _previewCountLabel.Font = Theme.Fonts.SmallBold;
        _previewCountLabel.ForeColor = Theme.InkSecondary;
        toolbar.Controls.Add(_previewCountLabel, 0, 0);

        _previewSearch.Size = new Size(220, 32);
        _previewSearch.Placeholder = "Cerca nei movimenti";
        _previewSearch.Margin = new Padding(0, 0, 10, 0);
        _previewSearch.TextChanged += (_, _) => FilterMovements();

        var toolbarActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 1, 0, 0),
            Margin = Padding.Empty
        };

        // Il campo di ricerca ha una larghezza fissa: con l'AutoSize di default
        // il TextBox veniva largo 100 px e il testo non si leggeva.
        _copyXmlButton.Text = "Copia XML";
        Theme.StyleButton(_copyXmlButton, Theme.ButtonTone.Neutral, compact: true);
        _copyXmlButton.Size = new Size(108, 32);
        _copyXmlButton.Margin = new Padding(6, 0, 0, 0);
        _copyXmlButton.Click += (_, _) => CopyXmlToClipboard();

        _exportButton.Text = "Scarica XML";
        Theme.StyleButton(_exportButton, Theme.ButtonTone.Primary, compact: true);
        _exportButton.Size = new Size(114, 32);
        _exportButton.Margin = new Padding(6, 0, 0, 0);
        _exportButton.Click += (_, _) => ExportXml();

        toolbarActions.Controls.Add(_exportButton);
        toolbarActions.Controls.Add(_copyXmlButton);
        toolbarActions.Controls.Add(_previewSearch);
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
        // lo spazio a sinistra con ellissi, il riepilogo AI resta a destra e i
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
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.Canvas,
            Padding = new Padding(2, 0, 2, 0),
            Margin = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.AutoEllipsis = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Text = "●   Pronto  ·  Formato di esportazione: XML TRRIPW";
        _statusLabel.Font = Theme.Fonts.Small;
        _statusLabel.ForeColor = Theme.MutedInk;
        content.Controls.Add(_statusLabel, 0, 0);

        // AutoSize: la colonna si adatta al testo e non c'è mai nulla da tagliare.
        _aiStatusLabel.AutoSize = true;
        _aiStatusLabel.Dock = DockStyle.Right;
        _aiStatusLabel.TextAlign = ContentAlignment.MiddleRight;
        _aiStatusLabel.Text = "AI locale opzionale";
        _aiStatusLabel.Font = Theme.Fonts.Small;
        _aiStatusLabel.ForeColor = Theme.MutedInk;
        _aiStatusLabel.Margin = new Padding(0, 0, 14, 0);
        content.Controls.Add(_aiStatusLabel, 1, 0);

        // Stato degli aggiornamenti sempre visibile: si vede a colpo d'occhio se
        // il collegamento alle Release è attivo.
        _updateStatusLabel.AutoSize = true;
        _updateStatusLabel.Dock = DockStyle.Right;
        _updateStatusLabel.TextAlign = ContentAlignment.MiddleRight;
        _updateStatusLabel.Text = "Aggiornamenti · verifica in corso";
        _updateStatusLabel.Font = Theme.Fonts.Small;
        _updateStatusLabel.ForeColor = Theme.MutedInk;
        content.Controls.Add(_updateStatusLabel, 2, 0);
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
