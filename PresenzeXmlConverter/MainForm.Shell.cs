using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter;

/// <summary>
/// Struttura della finestra: la barra di navigazione laterale e le quattro
/// sezioni (Dashboard, Nuova conversione, Gestione codici, Storico esportazioni).
/// Prima la finestra aveva una sola schermata; con le sezioni il lavoro di
/// conversione resta dov'era, ma le informazioni di riepilogo (dashboard) e la
/// cronologia delle consegne hanno un posto proprio invece di un dialogo in più.
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>
    /// Larghezza della barra di navigazione con la finestra stretta (anche la più
    /// piccola consentita): lascia respiro all'area di revisione, e per questo usa
    /// le etichette brevi ("Conversione", "Codici", "Storico").
    /// </summary>
    internal const int NavColumnWidthCompact = 176;

    /// <summary>Larghezza con la finestra larga: ci stanno le etichette per intero.</summary>
    internal const int NavColumnWidthWide = 224;

    /// <summary>Larghezza utile della finestra da cui la barra passa alla forma ampia.</summary>
    internal const int NavWideThreshold = 1240;

    private const int SectionDashboard = 0;
    private const int SectionConversion = 1;
    private const int SectionCompanies = 2;
    private const int SectionCodes = 3;
    private const int SectionHistory = 4;

    private readonly NavRail _navRail = new();
    private readonly Panel _sectionHost = new();
    private readonly TableLayoutPanel _dashboardSection = NewSection();
    private readonly TableLayoutPanel _codesSection = NewSection();
    private readonly TableLayoutPanel _historySection = NewSection();
    private readonly MappingEditorPanel _mappingPanel = new();
    private Control? _conversionSection;

    /* ----------------------------- Dashboard ----------------------------- */

    private readonly Label _dashExportsSummaryLabel = new();
    private readonly ThemedGrid _dashExportsGrid = new();

    /* -------------------------- Storico esportazioni --------------------- */

    private readonly Label _historySummaryLabel = new();
    private readonly ThemedGrid _historyGrid = new();
    private readonly Button _historyOpenButton = new ThemedButton();
    private readonly Button _historyCopyButton = new ThemedButton();
    private readonly Button _historySaveCopyButton = new ThemedButton();
    private readonly Button _historyClearButton = new ThemedButton();
    private IReadOnlyList<ExportRecord> _exports = Array.Empty<ExportRecord>();
    private bool _codesLoaded;

    /// <summary>Numero di sezioni della barra: il test grafico le disegna tutte.</summary>
    internal int SectionCount => _navRail.ItemCount;

    /// <summary>
    /// Apre un'altra sezione. Usato dai comandi della dashboard, dalle
    /// scorciatoie e dal test grafico che disegna ogni sezione.
    /// </summary>
    internal void ShowSection(int index)
    {
        _navRail.Select(index);
        ApplySection();
    }

    /// <summary>
    /// Costruisce la struttura: barra a sinistra, area delle sezioni a destra.
    /// La sezione di conversione è quella costruita da <c>BuildLayout</c> (card a
    /// sinistra e area di revisione a destra).
    /// </summary>
    private void BuildShell(TableLayoutPanel root, Control conversionSection)
    {
        _conversionSection = conversionSection;

        _navRail.AddItem("Dashboard", NavIcon.Dashboard);
        _navRail.AddItem("Nuova conversione", NavIcon.Convert, "Conversione");
        _navRail.AddItem("Aziende", NavIcon.Company);
        _navRail.AddItem("Gestione codici", NavIcon.Codes, "Codici");
        _navRail.AddItem("Storico esportazioni", NavIcon.History, "Storico");
        _navRail.Dock = DockStyle.Top;
        _navRail.Height = _navRail.PreferredRailHeight;
        _navRail.Margin = Padding.Empty;
        _navRail.SelectionChanged += (_, _) => ApplySection();

        var railHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Canvas,
            Margin = new Padding(0, 0, 12, 0),
            Padding = Padding.Empty
        };
        railHost.Controls.Add(_navRail);
        railHost.Controls.Add(BuildRailFooter());

        _sectionHost.Dock = DockStyle.Fill;
        _sectionHost.BackColor = Theme.Canvas;
        _sectionHost.Margin = Padding.Empty;
        _sectionHost.Controls.Add(BuildDashboardSection());
        _sectionHost.Controls.Add(BuildCompaniesSection());
        _sectionHost.Controls.Add(BuildCodesSection());
        _sectionHost.Controls.Add(BuildHistorySection());
        _sectionHost.Controls.Add(conversionSection);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, NavColumnWidthCompact));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(railHost, 0, 0);
        body.Controls.Add(_sectionHost, 1, 0);
        root.Controls.Add(body, 0, 1);
        _shellBody = body;
        _navRail.UseShortTitles = true;

        // La dashboard è la schermata di benvenuto: dice a colpo d'occhio dove si
        // è arrivati e da lì si parte con un clic.
        _navRail.SelectedIndex = SectionDashboard;
        ApplySection();
    }

    private TableLayoutPanel? _shellBody;
    private bool _navWidthReady;

    private readonly Label _railDataLabel = new();
    private readonly Label _railBackupLabel = new();
    private readonly Label _railCompanyLabel = new();

    /// <summary>
    /// Piede della barra laterale: lo stato dei dati e la versione sono sempre
    /// sotto gli occhi, senza aprire nulla. Sono quattro righe fisse (nessun testo
    /// va a capo): ogni riga è corta abbastanza per la barra più stretta.
    /// </summary>
    private Control BuildRailFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 110,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Theme.Canvas,
            Padding = new Padding(16, 8, 8, 10),
            Margin = Padding.Empty
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 5; row++)
        {
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        }

        footer.Controls.Add(CreateCardTitle("DATI LOCALI"), 0, 0);

        _railDataLabel.Dock = DockStyle.Fill;
        _railDataLabel.TextAlign = ContentAlignment.MiddleLeft;
        _railDataLabel.ForeColor = Theme.SuccessInk;
        _railDataLabel.Font = Theme.Fonts.SmallBold;
        _railDataLabel.AutoEllipsis = true;
        _railDataLabel.Margin = Padding.Empty;
        _railDataLabel.Text = "●  Database collegato";
        footer.Controls.Add(_railDataLabel, 0, 1);

        _railBackupLabel.Dock = DockStyle.Fill;
        _railBackupLabel.TextAlign = ContentAlignment.MiddleLeft;
        _railBackupLabel.ForeColor = Theme.MutedInk;
        _railBackupLabel.Font = Theme.Fonts.Tiny;
        _railBackupLabel.AutoEllipsis = true;
        _railBackupLabel.Margin = Padding.Empty;
        _railBackupLabel.Text = "backup automatici";
        footer.Controls.Add(_railBackupLabel, 0, 2);

        _railCompanyLabel.Dock = DockStyle.Fill;
        _railCompanyLabel.TextAlign = ContentAlignment.MiddleLeft;
        _railCompanyLabel.ForeColor = Theme.MutedInk;
        _railCompanyLabel.Font = Theme.Fonts.Tiny;
        _railCompanyLabel.AutoEllipsis = true;
        _railCompanyLabel.Margin = Padding.Empty;
        _railCompanyLabel.Text = "nessuna azienda";
        footer.Controls.Add(_railCompanyLabel, 0, 3);

        var version = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Tiny,
            AutoEllipsis = true,
            Margin = Padding.Empty,
            Text = $"Versione {RunningVersion.ToString(3)}"
        };
        footer.Controls.Add(version, 0, 4);
        return footer;
    }

    /// <summary>
    /// Con la finestra larga la barra mostra le etichette intere, con quella
    /// stretta le brevi. Le misure sono in pixel del dispositivo: il valore va
    /// riscalato con il DPI, perché quelli dichiarati nel costruttore li scala la
    /// finestra da sola ma una modifica fatta a finestra aperta no.
    /// </summary>
    private void ApplyNavWidth()
    {
        if (_shellBody is null || !_navWidthReady)
        {
            return;
        }
        var wide = ClientSize.Width >= LogicalToDeviceUnits(NavWideThreshold);
        var width = LogicalToDeviceUnits(wide ? NavColumnWidthWide : NavColumnWidthCompact);
        var style = _shellBody.ColumnStyles[0];
        if ((int)style.Width != width)
        {
            style.Width = width;
        }
        _navRail.UseShortTitles = !wide;
    }

    private static TableLayoutPanel NewSection() => new()
    {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        BackColor = Theme.Canvas,
        Margin = Padding.Empty,
        Visible = false
    };

    /// <summary>Mostra la sezione scelta e aggiorna i dati che quella sezione mostra.</summary>
    private void ApplySection()
    {
        var index = _navRail.SelectedIndex;
        if (_conversionSection is null)
        {
            return;
        }
        _dashboardSection.Visible = index == SectionDashboard;
        _conversionSection.Visible = index == SectionConversion;
        _companiesSection.Visible = index == SectionCompanies;
        _codesSection.Visible = index == SectionCodes;
        _historySection.Visible = index == SectionHistory;

        switch (index)
        {
            case SectionDashboard:
                RefreshDashboard();
                break;
            case SectionCompanies:
                RefreshCompanies();
                break;
            case SectionCodes:
                EnsureCodesLoaded();
                break;
            case SectionHistory:
                RefreshHistory();
                break;
        }
    }

    /// <summary>
    /// Aggiorna le card della dashboard. I numeri arrivano da
    /// <see cref="DashboardSummary"/>, quindi sono gli stessi che contano le
    /// altre schermate: nessun conto duplicato qui.
    /// </summary>
    private void RefreshDashboard()
    {
        RefreshDashboardCards();
        _dashExportsSummaryLabel.Text = ExportHistory.DescribeSummary(_exports, DateTime.Now);
        PopulateDashboardExports();
    }

    /* ----------------------- Costruzione della dashboard ------------------ */

    private Control BuildDashboardExports()
    {
        var card = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.Surface,
            SurfaceBorder = Theme.Divider,
            Radius = 12,
            Padding = new Padding(16, 12, 16, 12),
            Margin = Padding.Empty
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.Controls.Add(CreateCardTitle("ULTIME ESPORTAZIONI"), 0, 0);

        _dashExportsGrid.ReadOnly = true;
        _dashExportsGrid.EmptyStateText = "Nessun XML esportato: le consegne compariranno qui.";
        _dashExportsGrid.Columns.Add(CreateGridColumn("when", "Quando", 30, true));
        _dashExportsGrid.Columns.Add(CreateGridColumn("file", "File XML", 42, true));
        _dashExportsGrid.Columns.Add(CreateGridColumn("totals", "Contenuto", 28, true));
        _dashExportsGrid.CellDoubleClick += (_, args) => OpenExportFolder(RowExport(args.RowIndex, _dashExportsGrid));
        // Nella dashboard questa card prende lo spazio che resta: con la finestra
        // bassa l'intestazione da 42 px veniva tagliata a metà. Intestazione e righe
        // più basse e, se nemmeno così entrano, l'intestazione sparisce del tutto.
        _dashExportsGrid.ColumnHeadersHeight = 30;
        _dashExportsGrid.RowTemplate.Height = 32;
        _dashExportsGrid.SizeChanged += (_, _) =>
            _dashExportsGrid.ColumnHeadersVisible =
                _dashExportsGrid.ClientSize.Height >= _dashExportsGrid.ColumnHeadersHeight + _dashExportsGrid.RowTemplate.Height;
        layout.Controls.Add(_dashExportsGrid, 0, 1);

        _dashExportsSummaryLabel.Dock = DockStyle.Fill;
        _dashExportsSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _dashExportsSummaryLabel.ForeColor = Theme.MutedInk;
        _dashExportsSummaryLabel.Font = Theme.Fonts.Tiny;
        _dashExportsSummaryLabel.AutoEllipsis = true;
        _dashExportsSummaryLabel.Margin = Padding.Empty;
        _dashExportsSummaryLabel.Text = ExportHistory.DescribeSummary([], DateTime.Now);
        layout.Controls.Add(_dashExportsSummaryLabel, 0, 2);

        card.Controls.Add(layout);
        return card;
    }

    private void PopulateDashboardExports() =>
        PopulateExports(_dashExportsGrid, _exports, compact: true);

    /* ------------------------ Sezione Gestione codici -------------------- */

    private Control BuildCodesSection()
    {
        var page = _codesSection;
        page.RowCount = 3;
        page.ColumnCount = 1;
        page.Padding = new Padding(0, 0, 4, 0);
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        page.Controls.Add(BuildSectionHeading(
            "Gestione codici",
            "Abbinamenti fra i valori del foglio e i codici ufficiali PAGHE."), 0, 0);

        var card = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.Surface,
            SurfaceBorder = Theme.Divider,
            Radius = 12,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(0, 0, 0, 8)
        };
        card.Controls.Add(_mappingPanel);
        page.Controls.Add(card, 0, 1);
        page.Controls.Add(BuildCodesActions(), 0, 2);
        return page;
    }

    private Control BuildCodesActions()
    {
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var hint = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Tiny,
            AutoEllipsis = true,
            Margin = Padding.Empty,
            Text = "I codici restano nel profilo Windows: le conversioni successive li ritrovano già pronti."
        };
        bar.Controls.Add(hint, 0, 0);

        var actions = new FlowLayoutPanel
        {
            // Colonna AutoSize: il pannello dichiara la propria larghezza, altrimenti
            // la colonna lo stringe e i pulsanti più a sinistra vengono tagliati.
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        var save = Theme.CreateButton("Salva abbinamenti", Theme.ButtonTone.Primary);
        save.Size = new Size(178, 36);
        save.Margin = new Padding(8, 0, 0, 0);
        save.Click += (_, _) => SaveCodesPage();
        Describe(save, "Salva abbinamenti", "Salva nel database locale i codici inseriti nelle due griglie.");
        var reload = Theme.CreateButton("Ricarica elenchi", Theme.ButtonTone.Neutral);
        reload.Size = new Size(150, 36);
        reload.Margin = Padding.Empty;
        reload.Click += (_, _) =>
        {
            _codesLoaded = false;
            EnsureCodesLoaded();
            SetStatus("●   Elenchi dei codici ricaricati dal database locale.", false);
        };
        Describe(reload, "Ricarica elenchi", "Rilegge dal database gli abbinamenti salvati.");
        actions.Controls.Add(save);
        actions.Controls.Add(reload);
        bar.Controls.Add(actions, 1, 0);
        return bar;
    }

    /// <summary>
    /// Carica le griglie dei codici una sola volta: ricaricarle a ogni ingresso
    /// nella sezione cancellerebbe le correzioni non ancora salvate.
    /// </summary>
    private void EnsureCodesLoaded()
    {
        if (_codesLoaded)
        {
            return;
        }
        try
        {
            _mappingPanel.Load(
                _database.GetEmployeeMappings(),
                _database.GetPolicyMappings(),
                _requests.Select(request => request.EmployeeName).Distinct(StringComparer.OrdinalIgnoreCase),
                _requests.Select(request => request.Policy).Distinct(StringComparer.OrdinalIgnoreCase));
            _codesLoaded = true;
        }
        catch (SqliteException exception)
        {
            ShowError("Impossibile leggere gli abbinamenti", exception);
        }
    }

    private void SaveCodesPage()
    {
        if (!_mappingPanel.TrySave(out var error))
        {
            SetStatus($"✗   {error}", true);
            return;
        }
        try
        {
            foreach (var mapping in _mappingPanel.EmployeeMappings)
            {
                _database.SaveEmployeeMapping(mapping.Key, mapping.Value);
            }
            foreach (var mapping in _mappingPanel.PolicyMappings)
            {
                _database.SavePolicyMapping(mapping.Key, mapping.Value);
            }
            InvalidatePreview();
            SetStatus("●   Abbinamenti salvati nel database locale.", false);
        }
        catch (SqliteException exception)
        {
            ShowError("Impossibile salvare gli abbinamenti", exception);
        }
    }

    /* ----------------------- Sezione Storico esportazioni ---------------- */

    private Control BuildHistorySection()
    {
        var page = _historySection;
        page.RowCount = 3;
        page.ColumnCount = 1;
        page.Padding = new Padding(0, 0, 4, 0);
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        page.Controls.Add(BuildSectionHeading(
            "Storico esportazioni",
            "Ogni file XML salvato, con data, contenuto e cartella di destinazione."), 0, 0);

        var card = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.Surface,
            SurfaceBorder = Theme.Divider,
            Radius = 12,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(0, 0, 0, 8)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _historySummaryLabel.Dock = DockStyle.Fill;
        _historySummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _historySummaryLabel.ForeColor = Theme.InkSecondary;
        _historySummaryLabel.Font = Theme.Fonts.SmallBold;
        _historySummaryLabel.AutoEllipsis = true;
        _historySummaryLabel.Margin = Padding.Empty;
        _historySummaryLabel.Text = ExportHistory.DescribeSummary([], DateTime.Now);
        layout.Controls.Add(_historySummaryLabel, 0, 0);

        _historyGrid.ReadOnly = true;
        _historyGrid.EmptyStateText =
            "Nessuna esportazione registrata: salva un file XML e la consegna comparirà qui.";
        _historyGrid.Columns.Add(CreateGridColumn("when", "Quando", 20, true));
        _historyGrid.Columns.Add(CreateGridColumn("file", "File XML", 30, true));
        _historyGrid.Columns.Add(CreateGridColumn("folder", "Cartella", 34, true));
        _historyGrid.Columns.Add(CreateGridColumn("movements", "Movimenti", 14, true));
        _historyGrid.Columns.Add(CreateGridColumn("totals", "Ore", 16, true));
        _historyGrid.CellDoubleClick += (_, args) => OpenExportFolder(RowExport(args.RowIndex, _historyGrid));
        _historyGrid.SelectionChanged += (_, _) => UpdateHistoryButtons();
        layout.Controls.Add(_historyGrid, 0, 1);
        card.Controls.Add(layout);
        page.Controls.Add(card, 0, 1);

        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var hint = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Tiny,
            AutoEllipsis = true,
            Margin = Padding.Empty,
            Text = "Doppio clic su una riga per aprire la cartella. «Salva copia» risalva l'XML conservato."
        };
        bar.Controls.Add(hint, 0, 0);

        var actions = new FlowLayoutPanel
        {
            // Colonna AutoSize: il pannello dichiara la propria larghezza, altrimenti
            // la colonna lo stringe e i pulsanti più a sinistra vengono tagliati.
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        _historyOpenButton.Text = "Apri cartella";
        Theme.StyleButton(_historyOpenButton, Theme.ButtonTone.Primary, compact: true);
        _historyOpenButton.Size = new Size(124, 34);
        _historyOpenButton.Margin = new Padding(8, 0, 0, 0);
        _historyOpenButton.Click += (_, _) => OpenExportFolder(SelectedHistoryExport());
        Describe(_historyOpenButton, "Apri cartella", "Apre in Esplora file la cartella del file XML selezionato.");

        _historyCopyButton.Text = "Copia percorso";
        Theme.StyleButton(_historyCopyButton, Theme.ButtonTone.Neutral, compact: true);
        _historyCopyButton.Size = new Size(124, 34);
        _historyCopyButton.Margin = new Padding(8, 0, 0, 0);
        _historyCopyButton.Click += (_, _) => CopyExportPath(SelectedHistoryExport());
        Describe(_historyCopyButton, "Copia percorso", "Copia negli appunti il percorso completo del file selezionato.");

        _historyClearButton.Text = "Svuota storico";
        Theme.StyleButton(_historyClearButton, Theme.ButtonTone.Neutral, compact: true);
        _historyClearButton.Size = new Size(124, 34);
        _historyClearButton.Margin = new Padding(8, 0, 0, 0);
        _historyClearButton.Click += (_, _) => ClearHistory();
        Describe(_historyClearButton, "Svuota storico", "Elimina l'elenco delle esportazioni: i file XML su disco restano.");

        _historySaveCopyButton.Text = "Salva copia…";
        Theme.StyleButton(_historySaveCopyButton, Theme.ButtonTone.Positive, compact: true);
        _historySaveCopyButton.Size = new Size(124, 34);
        _historySaveCopyButton.Margin = new Padding(8, 0, 0, 0);
        _historySaveCopyButton.Click += (_, _) => SaveStoredXmlCopy(SelectedHistoryExport());
        Describe(
            _historySaveCopyButton,
            "Salva copia",
            "Risalva il file XML consegnato, conservato nel database: funziona anche se l'originale è stato spostato o cancellato.");

        actions.Controls.Add(_historyOpenButton);
        actions.Controls.Add(_historySaveCopyButton);
        actions.Controls.Add(_historyCopyButton);
        actions.Controls.Add(_historyClearButton);
        bar.Controls.Add(actions, 1, 0);
        page.Controls.Add(bar, 0, 2);
        UpdateHistoryButtons();
        return page;
    }

    private void RefreshHistory()
    {
        try
        {
            _exports = _database.GetExports();
        }
        catch (SqliteException exception)
        {
            ShowError("Impossibile leggere lo storico delle esportazioni", exception);
            return;
        }
        PopulateExports(_historyGrid, _exports, compact: false);
        _historySummaryLabel.Text = ExportHistory.DescribeSummary(_exports, DateTime.Now);
        UpdateHistoryButtons();
        UpdateNavSummaries();
    }

    private void UpdateHistoryButtons()
    {
        var hasSelection = _historyGrid.SelectedRows.Count > 0 && _exports.Count > 0;
        _historyOpenButton.Enabled = hasSelection;
        _historyCopyButton.Enabled = hasSelection;
        _historySaveCopyButton.Enabled = hasSelection && SelectedHistoryExport()?.HasStoredXml == true;
        _historyClearButton.Enabled = _exports.Count > 0;
    }

    private ExportRecord? SelectedHistoryExport()
    {
        if (_historyGrid.SelectedRows.Count == 0)
        {
            return null;
        }
        return RowExport(_historyGrid.SelectedRows[0].Index, _historyGrid);
    }

    private static ExportRecord? RowExport(int rowIndex, ThemedGrid grid) =>
        rowIndex >= 0 && rowIndex < grid.Rows.Count ? grid.Rows[rowIndex].Tag as ExportRecord : null;

    private void PopulateExports(ThemedGrid grid, IReadOnlyList<ExportRecord> exports, bool compact)
    {
        grid.Rows.Clear();
        var now = DateTime.Now;
        foreach (var record in exports)
        {
            var folder = Path.GetDirectoryName(record.FilePath) ?? string.Empty;
            var index = compact
                ? grid.Rows.Add(
                    ExportHistory.DescribeDay(record.LocalTime, now),
                    record.FileName,
                    ExportHistory.DescribeContents(record))
                : grid.Rows.Add(
                    ExportHistory.DescribeDay(record.LocalTime, now),
                    record.FileName,
                    folder,
                    record.Movements.ToString("N0"),
                    record.TotalText);
            grid.Rows[index].Tag = record;
        }
    }

    private void OpenExportFolder(ExportRecord? record)
    {
        if (record is null || record.FilePath.Length == 0)
        {
            return;
        }
        var folder = Path.GetDirectoryName(record.FilePath);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            SetStatus($"La cartella non esiste più: {folder}", true);
            return;
        }
        try
        {
            if (File.Exists(record.FilePath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{record.FilePath}\"")
                {
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            SetStatus($"Impossibile aprire la cartella: {exception.Message}", true);
        }
    }

    /// <summary>Risalva il file XML conservato nel database per una consegna passata.</summary>
    private void SaveStoredXmlCopy(ExportRecord? record)
    {
        if (record is null || !record.HasStoredXml)
        {
            return;
        }
        try
        {
            var xml = _database.GetExportXml(record.Id);
            if (xml is null)
            {
                SetStatus("La copia dell'XML non è leggibile nel database.", true);
                return;
            }
            using var picker = new SaveFileDialog
            {
                Title = "Salva una copia dell'XML consegnato",
                Filter = "File XML (*.xml)|*.xml",
                AddExtension = true,
                DefaultExt = "xml",
                FileName = record.FileName,
                OverwritePrompt = true
            };
            if (!string.IsNullOrEmpty(_lastExportDirectory) && Directory.Exists(_lastExportDirectory))
            {
                picker.InitialDirectory = _lastExportDirectory;
            }
            if (picker.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            File.WriteAllBytes(picker.FileName, xml);
            SetStatus($"●   Copia salvata: {picker.FileName}", false);
        }
        catch (Exception exception) when (
            exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            ShowError("Copia dell'XML non riuscita", exception);
        }
    }

    private void CopyExportPath(ExportRecord? record)
    {
        if (record is null || record.FilePath.Length == 0)
        {
            return;
        }
        try
        {
            Clipboard.SetText(record.FilePath);
            SetStatus("●   Percorso del file copiato negli appunti.", false);
        }
        catch (System.Runtime.InteropServices.ExternalException exception)
        {
            ShowError("Copia del percorso non riuscita", exception);
        }
    }

    private void ClearHistory()
    {
        if (_exports.Count == 0)
        {
            return;
        }
        var answer = MessageBox.Show(
            this,
            $"Elimino l'elenco delle {_exports.Count:N0} esportazioni registrate?\n\n" +
            "I file XML già salvati sul disco NON vengono toccati.",
            "Svuota lo storico",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            return;
        }
        try
        {
            _database.ClearExports();
            _exports = Array.Empty<ExportRecord>();
            RefreshHistory();
            RefreshDashboard();
            SetStatus("●   Storico delle esportazioni svuotato.", false);
        }
        catch (SqliteException exception)
        {
            ShowError("Impossibile svuotare lo storico", exception);
        }
    }

    /// <summary>
    /// Registra nell'archivio locale il file appena salvato. Un errore del
    /// database non deve far sembrare fallita l'esportazione: il file è su disco.
    /// </summary>
    private void RecordExport(string path, string companyCode)
    {
        try
        {
            var totals = MovementChecks.Summarize(_movements);
            // Si conserva ciò che è stato davvero scritto su disco, non l'anteprima:
            // l'XML potrebbe essere stato modificato a mano prima del salvataggio.
            byte[]? delivered = null;
            try
            {
                delivered = File.ReadAllBytes(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Senza la copia lo storico resta valido: manca solo "Salva copia".
            }
            _database.SaveExport(
                new ExportRecord(
                    DateTime.UtcNow,
                    Path.GetFileName(path),
                    Path.GetFullPath(path),
                    companyCode,
                    totals.Employees,
                    totals.Movements,
                    totals.TotalMinutes,
                    0,
                    ExportHistory.Fingerprint(companyCode, _movements),
                    Path.GetFileName(_sourcePath ?? string.Empty)),
                delivered);
            _exports = _database.GetExports();
            _currentPreviewExported = true;
            RefreshDashboard();
            if (_navRail.SelectedIndex == SectionHistory)
            {
                RefreshHistory();
            }
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            // Lo storico è un di più: l'esportazione è già riuscita.
        }
    }

    /* --------------------------- Elementi comuni ------------------------- */

    private static Control BuildSectionHeading(string title, string subtitle)
    {
        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        heading.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Ink,
            Font = Theme.Fonts.CardTitle,
            Margin = Padding.Empty
        }, 0, 0);
        heading.Controls.Add(new Label
        {
            Text = subtitle,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Small,
            AutoEllipsis = true,
            Margin = Padding.Empty
        }, 0, 1);
        return heading;
    }

    private static Label CreateCardTitle(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Theme.MutedInk,
        Font = Theme.Fonts.Section,
        Margin = Padding.Empty
    };

    private static Control BuildSectionHint(string text) => new Label
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Theme.FaintInk,
        Font = Theme.Fonts.Tiny,
        AutoEllipsis = true,
        Margin = new Padding(2, 4, 0, 0)
    };

    /* --------------------- Ricerca, avvio e dati differiti --------------- */

    private readonly RoundedField _headerSearch = new();
    private bool _searchSyncing;

    /// <summary>
    /// Misura delle fasi di avvio. È attiva solo con
    /// <c>PRESENZEFLOW_TRACE_STARTUP=1</c>: quando è spenta non costa nulla e
    /// l'avvio resta identico a prima.
    /// </summary>
    private static readonly StartupTrace? Trace =
        StartupTrace.IsEnabled ? new StartupTrace() : null;

    private static string StartupLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeGioia",
        "PresenzeXmlConverter",
        "startup.log");

    private Dictionary<string, string> ReadStartupSettings() => _database.GetSettings(
        "company-code",
        "last-export-dir",
        "last-source-dir");

    /// <summary>
    /// Campo di ricerca dell'intestazione: filtra i movimenti da qualunque
    /// sezione e porta l'utente dove i risultati si vedono.
    /// </summary>
    private Control BuildHeaderSearch()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.HeaderFill,
            Margin = Padding.Empty,
            Padding = new Padding(12, 0, 12, 0)
        };
        // Il campo sta al centro dello spazio fra titolo e pulsanti, largo al più 460 px:
        // su uno schermo largo non diventa una barra lunghissima, su uno stretto cede
        // spazio ma resta usabile.
        _headerSearch.Dock = DockStyle.None;
        _headerSearch.OnDark = true;
        _headerSearch.Placeholder = "Cerca nei movimenti…  (Ctrl+F)";
        _headerSearch.Input.AccessibleName = "Cerca nei movimenti";
        _headerSearch.TextChanged += (_, _) => SearchFromHeader();
        host.Controls.Add(_headerSearch);
        host.Resize += (_, _) => LayoutHeaderSearch(host);
        LayoutHeaderSearch(host);
        return host;
    }

    private void LayoutHeaderSearch(Panel host)
    {
        const int Height = 34;
        var available = Math.Max(0, host.ClientSize.Width - host.Padding.Horizontal);
        var width = Math.Min(LogicalToDeviceUnits(460), available);
        var height = LogicalToDeviceUnits(Height);
        _headerSearch.SetBounds(
            host.Padding.Left + ((available - width) / 2),
            Math.Max(0, (host.ClientSize.Height - height) / 2),
            width,
            height);
    }

    private void SearchFromHeader()
    {
        if (_searchSyncing)
        {
            return;
        }
        _searchSyncing = true;
        try
        {
            var value = _headerSearch.Value;
            if (value.Length > 0)
            {
                ShowSection(SectionConversion);
                _previewTabs.SelectedIndex = 0;
            }
            // Il filtro vero resta quello dell'area di revisione: qui si scrive
            // nello stesso campo, così la ricerca si comporta in un modo solo.
            _previewSearch.Value = value;
        }
        finally
        {
            _searchSyncing = false;
        }
    }

    /// <summary>
    /// Carica lo storico delle esportazioni dopo che la finestra è visibile: è
    /// l'unica lettura del database che non serve per disegnare la prima
    /// schermata, quindi non deve far aspettare l'utente.
    /// </summary>
    private void LoadExportsNow()
    {
        try
        {
            _exports = _database.GetExports();
        }
        catch (SqliteException)
        {
            _exports = Array.Empty<ExportRecord>();
        }
        RefreshDashboard();
        UpdateHistoryButtons();
        UpdateNavSummaries();
    }
}
