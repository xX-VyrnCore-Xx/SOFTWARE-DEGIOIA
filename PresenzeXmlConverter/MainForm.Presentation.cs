using System.Text.RegularExpressions;

namespace PresenzeXmlConverter;

internal sealed partial class MainForm
{
    // Modern, sophisticated color palette
    private static readonly Color Ink = Color.FromArgb(15, 26, 40);
    private static readonly Color InkSecondary = Color.FromArgb(55, 75, 100);
    private static readonly Color MutedInk = Color.FromArgb(103, 120, 142);
    private static readonly Color BrandBlue = Color.FromArgb(59, 130, 246);          // Modern vibrant blue
    private static readonly Color BrandBlueDark = Color.FromArgb(30, 58, 138);       // Darker blue for contrast
    private static readonly Color BrandBlueLite = Color.FromArgb(219, 234, 254);     // Light blue for backgrounds
    private static readonly Color BrandGreen = Color.FromArgb(34, 197, 94);          // Modern vibrant green
    private static readonly Color BrandGreenDark = Color.FromArgb(20, 83, 45);       // Darker green
    private static readonly Color BrandGreenLite = Color.FromArgb(220, 252, 231);    // Light green
    private static readonly Color Accent = Color.FromArgb(168, 85, 247);             // Purple accent
    private static readonly Color Surface = Color.White;
    private static readonly Color Canvas = Color.FromArgb(248, 250, 254);            // Lighter, more refined
    private static readonly Color CanvasDark = Color.FromArgb(241, 245, 250);
    private static readonly Color SurfaceHover = Color.FromArgb(243, 247, 252);
    private static readonly Color Divider = Color.FromArgb(229, 231, 235);
    private static readonly Color DividerLight = Color.FromArgb(242, 244, 248);
    private static readonly Font GridCodeFont = new("Cascadia Code", 8.2f);
    private static readonly Font GridNumberFont = new("Segoe UI Semibold", 9, FontStyle.Bold);
    private static readonly Font PreviewTabFont = new("Segoe UI Semibold", 8.5f, FontStyle.Bold);

    private readonly Label _sourceInfoLabel = new();
    private readonly Label _requestMetricLabel = new();
    private readonly Label _employeeMetricLabel = new();
    private readonly Label _previewTitleLabel = new();
    private readonly Label _previewSubtitleLabel = new();
    private readonly Label _previewCountLabel = new();
    private readonly TextBox _previewSearch = new();
    private readonly Button _copyXmlButton = new();
    private readonly Panel _reviewStatusBadge = new();
    private bool _isApplyingFilter;
    private bool _hasWarnings;

    private void BuildLayout()
    {
        Text = "PresenzeFlow";
        MinimumSize = new Size(1024, 650);
        Size = new Size(1360, 860);
        BackColor = Canvas;
        Icon = _applicationIcon;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18, 14, 18, 10),
            BackColor = Canvas
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        Controls.Add(root);

        var header = BuildBrandHeader();
        root.Controls.Add(header, 0, 0);

        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Canvas,
            Margin = new Padding(0)
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(workspace, 0, 1);

        var setupCard = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 20, 22, 20),
            Margin = new Padding(0, 0, 16, 0)
        };
        var setup = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 12,
            BackColor = Surface
        };
        setup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        setup.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        setup.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        setup.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        setup.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        setup.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        setup.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        setup.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
        setup.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        setup.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        setup.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        setup.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        setup.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        setupCard.Controls.Add(setup);
        workspace.Controls.Add(setupCard, 0, 0);

        var title = new Label
        {
            Text = "Nuova conversione",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Ink,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            Margin = new Padding(0)
        };
        setup.Controls.Add(title, 0, 0);

        setup.Controls.Add(CreateSectionLabel("01   AZIENDA ZUCCHETTI"), 0, 1);
        _companyCode.MaxLength = 6;
        _companyCode.Dock = DockStyle.Fill;
        _companyCode.Font = new Font("Segoe UI", 13, FontStyle.Bold);
        _companyCode.BackColor = Color.FromArgb(249, 250, 253);
        _companyCode.ForeColor = Ink;
        _companyCode.CharacterCasing = CharacterCasing.Upper;
        _companyCode.BorderStyle = BorderStyle.FixedSingle;
        _companyCode.Margin = new Padding(0, 2, 0, 8);
        _companyCode.TextChanged += (_, _) =>
        {
            InvalidatePreview();
            UpdateWorkflowSummary();
        };
        _companyCode.Leave += (_, _) => SaveCompanyCode();
        setup.Controls.Add(_companyCode, 0, 2);

        setup.Controls.Add(CreateSectionLabel("02   FILE SORGENTE"), 0, 3);
        var importButton = CreateButton("SELEZIONA FILE EXCEL", BrandBlue, true);
        importButton.Dock = DockStyle.Fill;
        importButton.Height = 44;
        importButton.Margin = new Padding(0, 2, 0, 8);
        importButton.Click += async (_, _) => await ImportWorkbookAsync();
        setup.Controls.Add(importButton, 0, 4);

        _fileLabel.Dock = DockStyle.Fill;
        _fileLabel.AutoSize = false;
        _fileLabel.AutoEllipsis = true;
        _fileLabel.Text = "Nessun file selezionato";
        _fileLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _fileLabel.ForeColor = InkSecondary;
        _fileLabel.Margin = new Padding(1, 1, 0, 0);
        setup.Controls.Add(_fileLabel, 0, 5);

        _sourceInfoLabel.Dock = DockStyle.Fill;
        _sourceInfoLabel.Text = "Seleziona un file Excel da analizzare.";
        _sourceInfoLabel.Font = new Font("Segoe UI", 8.7f);
        _sourceInfoLabel.ForeColor = MutedInk;
        _sourceInfoLabel.Margin = new Padding(1, 0, 0, 5);
        setup.Controls.Add(_sourceInfoLabel, 0, 6);

        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Surface,
            Margin = new Padding(0, 4, 0, 4)
        };
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        metrics.RowStyles.Clear();
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var requestTile = CreateMetricTile("RICHIESTE", _requestMetricLabel, BrandBlue, BrandBlueLite);
        var employeeTile = CreateMetricTile("DIPENDENTI", _employeeMetricLabel, BrandGreen, BrandGreenLite);
        metrics.Controls.Add(requestTile, 0, 0);
        metrics.Controls.Add(employeeTile, 1, 0);
        setup.Controls.Add(metrics, 0, 7);

        var mappingButton = CreateButton("Codici e giustificativi", BrandBlueLite, false);
        mappingButton.Dock = DockStyle.Fill;
        mappingButton.Margin = new Padding(0, 4, 0, 4);
        mappingButton.Click += (_, _) => EditMappings();
        setup.Controls.Add(mappingButton, 0, 8);

        var aiButton = CreateButton("Assistente AI locale", BrandBlueLite, false);
        aiButton.Dock = DockStyle.Fill;
        aiButton.Margin = new Padding(0, 4, 0, 4);
        aiButton.Click += async (_, _) => await ConfigureAiAsync();
        setup.Controls.Add(aiButton, 0, 9);

        var guidance = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 7, 0, 6) };
        var verticalRule = new Panel
        {
            BackColor = Color.FromArgb(226, 232, 240),
            Location = new Point(8, 5),
            Size = new Size(2, 56)
        };
        guidance.Controls.Add(verticalRule);
        var importStep = CreateStepDot("1", true);
        importStep.Location = new Point(0, 3);
        guidance.Controls.Add(importStep);
        guidance.Controls.Add(new Label
        {
            Text = "Importa foglio e codici",
            Size = new Size(190, 24),
            Location = new Point(28, 2),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(59, 130, 246)
        });
        var reviewStep = CreateStepDot("2", false);
        reviewStep.Location = new Point(0, 36);
        guidance.Controls.Add(reviewStep);
        guidance.Controls.Add(new Label
        {
            Text = "Rivedi ed esporta l'XML",
            Size = new Size(190, 24),
            Location = new Point(28, 36),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = MutedInk
        });
        setup.Controls.Add(guidance, 0, 10);

        _generateButton.Text = "Genera anteprima  →";
        _generateButton.Dock = DockStyle.Fill;
        _generateButton.Margin = new Padding(0, 3, 0, 0);
        _generateButton.Height = 48;
        StyleButton(_generateButton, BrandGreen, true);
        _generateButton.Click += (_, _) => GeneratePreview();
        setup.Controls.Add(_generateButton, 0, 11);

        var reviewCard = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 16, 18, 16),
            Margin = new Padding(0)
        };
        workspace.Controls.Add(reviewCard, 1, 0);
        var reviewLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Surface
        };
        reviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        reviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        reviewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        reviewCard.Controls.Add(reviewLayout);

        reviewLayout.Controls.Add(BuildPreviewHeading(), 0, 0);
        reviewLayout.Controls.Add(BuildPreviewToolbar(), 0, 1);
        reviewLayout.Controls.Add(BuildPreview(), 0, 2);
        root.Controls.Add(BuildFooter(), 0, 2);
        UpdateWorkflowSummary();
        UpdateButtons();
    }

    private Panel BuildBrandHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 26, 40),
            Margin = new Padding(0, 0, 0, 16)
        };
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var mark = new PictureBox
        {
            Image = _applicationIcon.ToBitmap(),
            Size = new Size(52, 52),
            SizeMode = PictureBoxSizeMode.Zoom,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Margin = new Padding(16, 16, 0, 0)
        };
        body.Controls.Add(mark, 0, 0);

        // Titolo e sottotitolo su righe distinte: non possono mai finire sopra
        // l'uno all'altra, qualunque sia il DPI o la larghezza della finestra.
        var titles = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0)
        };
        titles.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        titles.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "PresenzeFlow",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            Margin = new Padding(0)
        };
        titles.Controls.Add(title, 0, 1);

        var subtitle = new Label
        {
            Text = "Trasforma i dati · Verifica · Esporta",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Color.FromArgb(186, 206, 232),
            Font = new Font("Segoe UI", 8.5f),
            Margin = new Padding(1, 0, 0, 4)
        };
        titles.Controls.Add(subtitle, 0, 2);
        body.Controls.Add(titles, 1, 0);

        _reviewStatusBadge.BackColor = Color.FromArgb(30, 58, 138);
        _reviewStatusBadge.Dock = DockStyle.Top;
        _reviewStatusBadge.Height = 38;
        _reviewStatusBadge.Margin = new Padding(0, 25, 0, 0);
        _reviewStatusBadge.Controls.Add(new Label
        {
            Text = "●   ELABORAZIONE LOCALE",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(191, 219, 254),
            Font = new Font("Segoe UI", 8.2f, FontStyle.Bold)
        });
        body.Controls.Add(_reviewStatusBadge, 2, 0);

        header.Controls.Add(body);
        return header;
    }

    private Control BuildPreviewHeading()
    {
        var heading = new Panel { Dock = DockStyle.Fill, BackColor = Surface };
        _previewTitleLabel.Text = "Area di revisione";
        _previewTitleLabel.AutoSize = true;
        _previewTitleLabel.Location = new Point(0, 2);
        _previewTitleLabel.Font = new Font("Segoe UI", 16, FontStyle.Bold);
        _previewTitleLabel.ForeColor = Ink;
        heading.Controls.Add(_previewTitleLabel);
        _previewSubtitleLabel.Text = "Importa un foglio Excel per visualizzare e controllare i movimenti.";
        _previewSubtitleLabel.AutoSize = false;
        _previewSubtitleLabel.Location = new Point(0, 38);
        _previewSubtitleLabel.Height = 24;
        _previewSubtitleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _previewSubtitleLabel.Font = new Font("Segoe UI", 8.9f);
        _previewSubtitleLabel.ForeColor = MutedInk;
        heading.Controls.Add(_previewSubtitleLabel);
        return heading;
    }

    private Control BuildPreviewToolbar()
    {
        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = CanvasDark,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(0, 2, 0, 6)
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        // Riga percentuale esplicita: senza una riga dichiarata la tabella usa un
        // default che non combacia con l'altezza reale e i figli escono fuori.
        toolbar.RowStyles.Clear();
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _previewCountLabel.Text = "In attesa di un file";
        _previewCountLabel.Dock = DockStyle.Fill;
        _previewCountLabel.TextAlign = ContentAlignment.MiddleLeft;
        _previewCountLabel.Font = new Font("Segoe UI", 8.7f, FontStyle.Bold);
        _previewCountLabel.ForeColor = InkSecondary;
        toolbar.Controls.Add(_previewCountLabel, 0, 0);

        _previewSearch.PlaceholderText = "Cerca nei movimenti";
        _previewSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _previewSearch.Margin = new Padding(0, 0, 8, 0);
        _previewSearch.Font = new Font("Segoe UI", 9);
        _previewSearch.BackColor = Surface;
        _previewSearch.TextChanged += (_, _) => FilterMovements();
        var toolbarActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 5, 0, 0),
            Margin = new Padding(0)
        };

        _copyXmlButton.Text = "Copia XML";
        StyleButton(_copyXmlButton, Color.FromArgb(225, 237, 249), false);
        _copyXmlButton.MinimumSize = Size.Empty;
        _copyXmlButton.Height = 34;
        _copyXmlButton.Margin = new Padding(5, 0, 0, 0);
        _copyXmlButton.Click += (_, _) => CopyXmlToClipboard();

        _exportButton.Text = "Scarica XML";
        StyleButton(_exportButton, BrandBlue, true);
        _exportButton.MinimumSize = Size.Empty;
        _exportButton.Height = 34;
        _exportButton.Margin = new Padding(5, 0, 0, 0);
        _exportButton.Click += (_, _) => ExportXml();
        toolbarActions.Controls.Add(_exportButton);
        toolbarActions.Controls.Add(_copyXmlButton);
        toolbarActions.Controls.Add(_previewSearch);
        toolbar.Controls.Add(toolbarActions, 1, 0);
        return toolbar;
    }

    private TabControl BuildPreview()
    {
        _previewTabs.Dock = DockStyle.Fill;
        _previewTabs.Appearance = TabAppearance.Normal;
        _previewTabs.SizeMode = TabSizeMode.Fixed;
        _previewTabs.ItemSize = new Size(160, 35);
        _previewTabs.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
        _previewTabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _previewTabs.DrawItem += DrawPreviewTab;
        _previewTabs.Padding = new Point(12, 4);
        _previewTabs.Margin = new Padding(0);

        var movementTab = new TabPage("Movimenti  ·  0") { BackColor = Surface, Padding = new Padding(7) };
        _movementsGrid.Dock = DockStyle.Fill;
        _movementsGrid.AllowUserToAddRows = false;
        _movementsGrid.AllowUserToDeleteRows = false;
        _movementsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _movementsGrid.BackgroundColor = Surface;
        _movementsGrid.BorderStyle = BorderStyle.None;
        _movementsGrid.RowHeadersVisible = true;
        _movementsGrid.RowHeadersWidth = 48;
        _movementsGrid.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 254);
        _movementsGrid.RowHeadersDefaultCellStyle.ForeColor = Color.FromArgb(148, 163, 184);
        _movementsGrid.RowHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _movementsGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _movementsGrid.MultiSelect = false;
        _movementsGrid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        _movementsGrid.EnableHeadersVisualStyles = false;
        _movementsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(242, 244, 248);
        _movementsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(55, 65, 81);
        _movementsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _movementsGrid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 4, 0);
        _movementsGrid.ColumnHeadersHeight = 45;
        _movementsGrid.RowTemplate.Height = 42;
        _movementsGrid.DefaultCellStyle.Font = new Font("Segoe UI", 8.9f);
        _movementsGrid.DefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59);
        _movementsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
        _movementsGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 26, 40);
        _movementsGrid.DefaultCellStyle.Padding = new Padding(9, 0, 5, 0);
        _movementsGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 254);
        _movementsGrid.GridColor = Color.FromArgb(237, 239, 244);
        _movementsGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
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
            using (var background = new SolidBrush(Color.FromArgb(248, 250, 254)))
            {
                args.Graphics.FillRectangle(background, bounds);
            }
            TextRenderer.DrawText(
                args.Graphics,
                (args.RowIndex + 1).ToString("D2"),
                new Font("Segoe UI", 8.2f),
                bounds,
                Color.FromArgb(148, 163, 184),
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
        movementTab.Controls.Add(_movementsGrid);

        var xmlTab = new TabPage("Anteprima XML") { BackColor = Color.FromArgb(15, 23, 42), Padding = new Padding(0) };
        _xmlPreview.Dock = DockStyle.Fill;
        _xmlPreview.ReadOnly = true;
        _xmlPreview.BorderStyle = BorderStyle.None;
        _xmlPreview.BackColor = Color.FromArgb(15, 23, 42);
        _xmlPreview.ForeColor = Color.FromArgb(226, 232, 240);
        _xmlPreview.Font = new Font("Cascadia Code", 9.3f);
        _xmlPreview.WordWrap = false;
        _xmlPreview.DetectUrls = false;
        _xmlPreview.Margin = new Padding(0);
        xmlTab.Controls.Add(_xmlPreview);
        _previewTabs.TabPages.Add(movementTab);
        _previewTabs.TabPages.Add(xmlTab);
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
        if (name is "hours" or "minutes") column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        if (name is "code" or "reason") column.DefaultCellStyle.Font = new Font("Cascadia Code", 8.3f);
        return column;
    }

    private Control BuildFooter()
    {
        // Due colonne separate: lo stato occupa tutto lo spazio a sinistra con
        // ellissi, il riepilogo AI resta a destra e non ci si sovrappongono mai.
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = CanvasDark,
            Padding = new Padding(14, 7, 14, 0),
            BorderStyle = BorderStyle.FixedSingle
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.AutoEllipsis = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Text = "●   Pronto  ·  Formato di esportazione: XML TRRIPW";
        _statusLabel.Font = new Font("Segoe UI", 8.6f);
        _statusLabel.ForeColor = MutedInk;
        footer.Controls.Add(_statusLabel, 0, 0);

        _aiStatusLabel.AutoSize = false;
        _aiStatusLabel.Dock = DockStyle.Fill;
        _aiStatusLabel.TextAlign = ContentAlignment.MiddleRight;
        _aiStatusLabel.AutoEllipsis = true;
        _aiStatusLabel.Text = "AI locale opzionale";
        _aiStatusLabel.Font = new Font("Segoe UI", 8.6f);
        _aiStatusLabel.ForeColor = MutedInk;
        footer.Controls.Add(_aiStatusLabel, 1, 0);
        return footer;
    }

    private static Label CreateSectionLabel(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
            Margin = new Padding(0, 1, 0, 2)
        };

    private static Panel CreateMetricTile(string caption, Label metric, Color accent, Color background)
    {
        var tile = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = background,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(14, 12, 14, 12)
        };
        tile.Paint += (s, e) =>
        {
            var panel = (Panel)s;
            var rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
            using (var pen = new Pen(Color.FromArgb(219, 234, 254), 1.5f))
            {
                e.Graphics.DrawRectangle(pen, rect);
            }
        };
        tile.Controls.Add(new Label
        {
            Text = caption,
            AutoSize = true,
            Location = new Point(14, 12),
            ForeColor = Color.FromArgb(107, 114, 128),
            Font = new Font("Segoe UI", 7.8f, FontStyle.Bold)
        });
        metric.Text = "0";
        metric.AutoSize = true;
        metric.Location = new Point(14, 32);
        metric.ForeColor = accent;
        metric.Font = new Font("Segoe UI", 22, FontStyle.Bold);
        tile.Controls.Add(metric);
        return tile;
    }

    private static Label CreateStepDot(string text, bool active) =>
        new()
        {
            Text = text,
            Size = new Size(20, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = active ? Color.FromArgb(59, 130, 246) : Color.FromArgb(226, 232, 240),
            ForeColor = active ? Color.White : Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };

    private void DrawPreviewTab(object? sender, DrawItemEventArgs args)
    {
        var selected = args.Index == _previewTabs.SelectedIndex;
        var bounds = args.Bounds;
        var background = selected ? Surface : Color.FromArgb(248, 250, 254);
        using var brush = new SolidBrush(background);
        args.Graphics.FillRectangle(brush, bounds);
        var title = _previewTabs.TabPages[args.Index].Text;
        var foreground = selected ? BrandBlue : MutedInk;
        var font = selected ? new Font("Segoe UI", 9, FontStyle.Bold) : new Font("Segoe UI", 9);
        TextRenderer.DrawText(
            args.Graphics,
            title,
            font,
            bounds,
            foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (selected)
        {
            using var underline = new SolidBrush(BrandBlue);
            args.Graphics.FillRectangle(underline, bounds.X + 12, bounds.Bottom - 3, bounds.Width - 24, 3);
        }
        using var separator = new Pen(Divider);
        args.Graphics.DrawLine(separator, bounds.Right - 1, bounds.Top + 6, bounds.Right - 1, bounds.Bottom - 6);
    }

    private static void FormatMovementCell(object? sender, DataGridViewCellFormattingEventArgs args)
    {
        if (sender is not DataGridView grid || args.RowIndex < 0)
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
            cellStyle.ForeColor = Color.FromArgb(30, 99, 159);
            cellStyle.SelectionForeColor = Color.FromArgb(30, 99, 159);
            cellStyle.Font = GridCodeFont;
        }
        else if (columnName == "date")
        {
            cellStyle.ForeColor = Color.FromArgb(82, 102, 127);
        }
        else if (columnName is "hours" or "minutes")
        {
            cellStyle.ForeColor = Color.FromArgb(25, 82, 71);
            cellStyle.Font = GridNumberFont;
        }
    }

    private void FilterMovements()
    {
        if (_isApplyingFilter || _movementsGrid.Rows.Count == 0)
        {
            return;
        }

        _isApplyingFilter = true;
        var query = _previewSearch.Text.Trim();
        var visible = 0;
        try
        {
            foreach (DataGridViewRow row in _movementsGrid.Rows)
            {
                var matches = query.Length == 0 ||
                    new[] { "employee", "code", "date", "policy", "reason", "source" }
                        .Any(column => CellText(row, column).Contains(query, StringComparison.CurrentCultureIgnoreCase));
                row.Visible = matches;
                if (matches) visible++;
            }
        }
        finally
        {
            _isApplyingFilter = false;
        }
        _previewCountLabel.Text =
            query.Length == 0
                ? $"{visible} movimenti   ·   tutti inclusi nell’XML"
                : $"{visible} visualizzati   ·   {_movementsGrid.Rows.Count} inclusi nell’XML";
    }

    private void UpdateWorkflowSummary()
    {
        var workers = _requests.Select(request => request.EmployeeName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        _requestMetricLabel.Text = _requests.Count.ToString("N0");
        _employeeMetricLabel.Text = workers.ToString("N0");
        _sourceInfoLabel.Text = _inspection is null
            ? "In attesa di un file Excel."
            : $"{_inspection.SheetName}   ·   {_requests.Count:N0} richieste · {workers:N0} dipendenti";

        if (_hasWarnings)
        {
            _reviewStatusBadge.BackColor = Color.FromArgb(89, 62, 38);
            _reviewStatusBadge.Controls[0].Text = "●   REVISIONE RICHIESTA";
            _reviewStatusBadge.Controls[0].ForeColor = Color.FromArgb(255, 228, 183);
        }
        else if (!string.IsNullOrWhiteSpace(_xml))
        {
            _reviewStatusBadge.BackColor = Color.FromArgb(35, 76, 70);
            _reviewStatusBadge.Controls[0].Text = "●   ANTEPRIMA PRONTA";
            _reviewStatusBadge.Controls[0].ForeColor = Color.FromArgb(205, 244, 228);
        }
        else
        {
            _reviewStatusBadge.BackColor = Color.FromArgb(38, 58, 81);
            _reviewStatusBadge.Controls[0].Text = "●   PRONTO PER INIZIARE";
            _reviewStatusBadge.Controls[0].ForeColor = Color.FromArgb(193, 213, 235);
        }
    }

    private void UpdatePreviewSummary(IReadOnlyList<DailyMovement> movements)
    {
        var workers = movements.Select(movement => movement.EmployeeCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var totalMinutes = movements.Sum(movement => movement.Hours * 60 + movement.Minutes);
        _previewTitleLabel.Text = "Revisione movimenti";
        _previewSubtitleLabel.Text =
            $"{workers:N0} dipendenti    ·    {totalMinutes / 60:N0} h {totalMinutes % 60:D2} min assegnati    ·    CSV/Excel riga origine disponibile";
        _previewTabs.TabPages[0].Text = $"Movimenti  ·  {movements.Count:N0}";
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
            _xmlPreview.SelectionColor = Color.FromArgb(226, 232, 240);
            _xmlPreview.Select(0, 0);
            return;
        }

        var selectionStart = _xmlPreview.SelectionStart;
        _xmlPreview.SuspendLayout();
        _xmlPreview.SelectAll();
        _xmlPreview.SelectionColor = Color.FromArgb(226, 232, 240);
        foreach (Match match in Regex.Matches(text, @"</?[\w:.-]+|/?>"))
        {
            _xmlPreview.Select(match.Index, match.Length);
            _xmlPreview.SelectionColor = Color.FromArgb(129, 198, 255);
        }
        foreach (Match match in Regex.Matches(text, "\"[^\"]*\"|'[^']*'"))
        {
            _xmlPreview.Select(match.Index, match.Length);
            _xmlPreview.SelectionColor = Color.FromArgb(167, 193, 97);
        }
        foreach (Match match in Regex.Matches(text, @"(?<=>)[^<\r\n]+(?=<)"))
        {
            _xmlPreview.Select(match.Index, match.Length);
            _xmlPreview.SelectionColor = Color.FromArgb(167, 243, 208);
        }
        _xmlPreview.Select(Math.Min(selectionStart, _xmlPreview.TextLength), 0);
        _xmlPreview.SelectionColor = Color.FromArgb(226, 232, 240);
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
