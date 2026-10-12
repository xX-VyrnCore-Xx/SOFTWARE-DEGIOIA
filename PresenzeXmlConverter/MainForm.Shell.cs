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
    /// Larghezza della colonna della barra di navigazione (barra 186 px + margine).
    /// Il valore è il massimo che lascia respiro all'area di revisione anche nella
    /// finestra più piccola consentita: con una barra più larga i comandi
    /// dell'area di revisione uscirebbero dal bordo destro.
    /// </summary>
    internal const int NavColumnWidth = 198;

    private const int SectionDashboard = 0;
    private const int SectionConversion = 1;
    private const int SectionCodes = 2;
    private const int SectionHistory = 3;

    private readonly NavRail _navRail = new();
    private readonly Panel _sectionHost = new();
    private readonly TableLayoutPanel _dashboardSection = NewSection();
    private readonly TableLayoutPanel _codesSection = NewSection();
    private readonly TableLayoutPanel _historySection = NewSection();
    private readonly MappingEditorPanel _mappingPanel = new();
    private Control? _conversionSection;

    /* ----------------------------- Dashboard ----------------------------- */

    private readonly Label _dashRequestsValue = new();
    private readonly Label _dashEmployeesValue = new();
    private readonly Label _dashMovementsValue = new();
    private readonly Label _dashHoursValue = new();
    private readonly Label _dashSourceLabel = new();
    private readonly Label _dashSheetLabel = new();
    private readonly Label _dashExportsSummaryLabel = new();
    private readonly ThemedGrid _dashExportsGrid = new();

    /* -------------------------- Storico esportazioni --------------------- */

    private readonly Label _historySummaryLabel = new();
    private readonly ThemedGrid _historyGrid = new();
    private readonly Button _historyOpenButton = new();
    private readonly Button _historyCopyButton = new();
    private readonly Button _historyClearButton = new();
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
        _navRail.AddItem("Nuova conversione", NavIcon.Convert);
        _navRail.AddItem("Gestione codici", NavIcon.Codes);
        _navRail.AddItem("Storico esportazioni", NavIcon.History);
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

        _sectionHost.Dock = DockStyle.Fill;
        _sectionHost.BackColor = Theme.Canvas;
        _sectionHost.Margin = Padding.Empty;
        _sectionHost.Controls.Add(BuildDashboardSection());
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
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, NavColumnWidth));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(railHost, 0, 0);
        body.Controls.Add(_sectionHost, 1, 0);
        root.Controls.Add(body, 0, 1);

        // La dashboard è la schermata di benvenuto: dice a colpo d'occhio dove si
        // è arrivati e da lì si parte con un clic.
        _navRail.SelectedIndex = SectionDashboard;
        ApplySection();
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
        _codesSection.Visible = index == SectionCodes;
        _historySection.Visible = index == SectionHistory;

        switch (index)
        {
            case SectionDashboard:
                RefreshDashboard();
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
        var summary = DashboardSummary.From(_requests, _movements, _exports.Count);
        _dashRequestsValue.Text = summary.RequestsText;
        _dashEmployeesValue.Text = summary.EmployeesText;
        _dashMovementsValue.Text = summary.MovementsText;
        _dashHoursValue.Text = summary.CompactTotalText;
        _dashSourceLabel.Text = DashboardSummary.DescribeSource(
            _sourcePath is null ? null : Path.GetFileName(_sourcePath),
            !string.IsNullOrWhiteSpace(_xml));
        _dashSheetLabel.Text = _inspection is null
            ? "Nessun foglio letto"
            : $"Foglio: {_inspection.SheetName}";
        _dashExportsSummaryLabel.Text = ExportHistory.DescribeSummary(_exports, DateTime.Now);
        PopulateDashboardExports();
    }

    /* ----------------------- Costruzione della dashboard ------------------ */

    private Control BuildDashboardSection()
    {
        var page = _dashboardSection;
        page.RowCount = 4;
        page.ColumnCount = 1;
        page.Padding = new Padding(0, 0, 4, 0);
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        page.Controls.Add(BuildSectionHeading(
            "Dashboard",
            "Riepilogo della conversione in corso e delle ultime esportazioni."), 0, 0);

        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        for (var column = 0; column < 4; column++)
        {
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        metrics.Controls.Add(CreateMetricTile("RICHIESTE", _dashRequestsValue, Theme.BrandInk, Theme.BrandSoft), 0, 0);
        metrics.Controls.Add(CreateMetricTile("DIPENDENTI", _dashEmployeesValue, Theme.SuccessInk, Theme.SuccessSoft), 1, 0);
        metrics.Controls.Add(CreateMetricTile("MOVIMENTI", _dashMovementsValue, Theme.BrandInk, Theme.BrandSoft), 2, 0);
        metrics.Controls.Add(CreateMetricTile("ORE ASSEGNATE", _dashHoursValue, Theme.SuccessInk, Theme.SuccessSoft), 3, 0);
        // "42h 30m" usa il font grande dei valori: la forma estesa non entrerebbe
        // in una card larga un quarto dell'area.
        _dashHoursValue.Font = Theme.Fonts.CardTitle;
        page.Controls.Add(metrics, 0, 1);

        var columns = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        columns.Controls.Add(BuildDashboardLeft(), 0, 0);
        columns.Controls.Add(BuildDashboardExports(), 1, 0);
        page.Controls.Add(columns, 0, 2);

        page.Controls.Add(BuildSectionHint(
            "Suggerimento: trascina un file .xlsx sulla finestra per importarlo senza aprire il dialogo."), 0, 3);
        return page;
    }

    private Control BuildDashboardLeft()
    {
        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Canvas,
            Margin = new Padding(0, 0, 8, 0)
        };
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // Le azioni rapide vanno su due righe di pulsanti (32 + 6 px l'una):
        // la riga è alta quanto il contenuto più la spaziatura della card.
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        left.Controls.Add(BuildSourceCard(), 0, 0);
        left.Controls.Add(BuildQuickActionsCard(), 0, 1);
        return left;
    }

    private Control BuildSourceCard()
    {
        var card = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.Surface,
            SurfaceBorder = Theme.Divider,
            Radius = 12,
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(0, 0, 0, 8)
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

        layout.Controls.Add(CreateCardTitle("FILE SORGENTE"), 0, 0);

        _dashSourceLabel.Dock = DockStyle.Fill;
        _dashSourceLabel.TextAlign = ContentAlignment.TopLeft;
        _dashSourceLabel.ForeColor = Theme.InkSecondary;
        _dashSourceLabel.Font = Theme.Fonts.Small;
        _dashSourceLabel.AutoEllipsis = true;
        _dashSourceLabel.Margin = Padding.Empty;
        _dashSourceLabel.Text = DashboardSummary.DescribeSource(null, false);
        layout.Controls.Add(_dashSourceLabel, 0, 1);

        _dashSheetLabel.Dock = DockStyle.Fill;
        _dashSheetLabel.TextAlign = ContentAlignment.BottomLeft;
        _dashSheetLabel.ForeColor = Theme.MutedInk;
        _dashSheetLabel.Font = Theme.Fonts.Tiny;
        _dashSheetLabel.AutoEllipsis = true;
        _dashSheetLabel.Margin = Padding.Empty;
        _dashSheetLabel.Text = "Nessun foglio letto";
        layout.Controls.Add(_dashSheetLabel, 0, 2);

        card.Controls.Add(layout);
        return card;
    }

    private Control BuildQuickActionsCard()
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
            RowCount = 2,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(CreateCardTitle("AZIONI RAPIDE"), 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var open = Theme.CreateButton("Apri file Excel", Theme.ButtonTone.Primary, compact: true);
        open.Size = new Size(148, 32);
        open.Margin = new Padding(0, 0, 8, 6);
        open.Click += async (_, _) => await ImportWorkbookAsync();
        Describe(open, "Apri file Excel", "Scegli un foglio .xlsx da convertire in XML TRRIPW.");
        actions.Controls.Add(open);

        var generate = Theme.CreateButton("Genera anteprima", Theme.ButtonTone.Positive, compact: true);
        generate.Size = new Size(148, 32);
        generate.Margin = new Padding(0, 0, 8, 6);
        generate.Click += (_, _) => GeneratePreview();
        Describe(generate, "Genera anteprima", "Converte le richieste in movimenti giornalieri e XML.");
        actions.Controls.Add(generate);

        var codes = Theme.CreateButton("Gestione codici", Theme.ButtonTone.Neutral, compact: true);
        codes.Size = new Size(148, 32);
        codes.Margin = new Padding(0, 0, 8, 6);
        codes.Click += (_, _) => ShowSection(SectionCodes);
        Describe(codes, "Gestione codici", "Modifica codici PAGHE e giustificativi delle assenze.");
        actions.Controls.Add(codes);

        var history = Theme.CreateButton("Storico esportazioni", Theme.ButtonTone.Neutral, compact: true);
        history.Size = new Size(148, 32);
        history.Margin = new Padding(0, 0, 8, 6);
        history.Click += (_, _) => ShowSection(SectionHistory);
        Describe(history, "Storico esportazioni", "Le ultime consegne: file, data e totale ore.");
        actions.Controls.Add(history);

        layout.Controls.Add(actions, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

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
            "Abinamenti fra i valori del foglio e i codici ufficiali PAGHE."), 0, 0);

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
            Dock = DockStyle.Fill,
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
            Text = "Doppio clic su una riga per aprire la cartella del file XML."
        };
        bar.Controls.Add(hint, 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        _historyOpenButton.Text = "Apri cartella";
        Theme.StyleButton(_historyOpenButton, Theme.ButtonTone.Primary, compact: true);
        _historyOpenButton.Size = new Size(132, 34);
        _historyOpenButton.Margin = new Padding(8, 0, 0, 0);
        _historyOpenButton.Click += (_, _) => OpenExportFolder(SelectedHistoryExport());
        Describe(_historyOpenButton, "Apri cartella", "Apre in Esplora file la cartella del file XML selezionato.");

        _historyCopyButton.Text = "Copia percorso";
        Theme.StyleButton(_historyCopyButton, Theme.ButtonTone.Neutral, compact: true);
        _historyCopyButton.Size = new Size(132, 34);
        _historyCopyButton.Margin = new Padding(8, 0, 0, 0);
        _historyCopyButton.Click += (_, _) => CopyExportPath(SelectedHistoryExport());
        Describe(_historyCopyButton, "Copia percorso", "Copia negli appunti il percorso completo del file selezionato.");

        _historyClearButton.Text = "Svuota storico";
        Theme.StyleButton(_historyClearButton, Theme.ButtonTone.Neutral, compact: true);
        _historyClearButton.Size = new Size(132, 34);
        _historyClearButton.Margin = new Padding(8, 0, 0, 0);
        _historyClearButton.Click += (_, _) => ClearHistory();
        Describe(_historyClearButton, "Svuota storico", "Elimina l'elenco delle esportazioni: i file XML su disco restano.");

        actions.Controls.Add(_historyClearButton);
        actions.Controls.Add(_historyCopyButton);
        actions.Controls.Add(_historyOpenButton);
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
    }

    private void UpdateHistoryButtons()
    {
        var hasSelection = _historyGrid.SelectedRows.Count > 0 && _exports.Count > 0;
        _historyOpenButton.Enabled = hasSelection;
        _historyCopyButton.Enabled = hasSelection;
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
            _database.SaveExport(new ExportRecord(
                DateTime.UtcNow,
                Path.GetFileName(path),
                Path.GetFullPath(path),
                companyCode,
                totals.Employees,
                totals.Movements,
                totals.TotalMinutes));
            _exports = _database.GetExports();
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
        "last-source-dir",
        AiModelSetting);

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
            Margin = new Padding(0, 16, 14, 0),
            Padding = Padding.Empty
        };
        _headerSearch.Dock = DockStyle.Top;
        _headerSearch.Height = 34;
        _headerSearch.Placeholder = "Cerca nei movimenti…";
        _headerSearch.Input.AccessibleName = "Cerca nei movimenti";
        _headerSearch.TextChanged += (_, _) => SearchFromHeader();
        host.Controls.Add(_headerSearch);
        return host;
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
    }
}
