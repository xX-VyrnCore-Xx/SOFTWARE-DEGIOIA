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
    private const int SectionActivity = 5;
    private const int SectionPdf = 6;

    private readonly NavRail _navRail = new();
    private readonly Panel _sectionHost = new();
    private readonly TableLayoutPanel _dashboardSection = NewSection();
    private readonly TableLayoutPanel _codesSection = NewSection();
    private readonly TableLayoutPanel _historySection = NewSection();
    private readonly TableLayoutPanel _pdfSection = NewSection();
    private readonly MappingEditorPanel _mappingPanel = new();
    private readonly MonthCalendarView _calendarView = new();
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
    private readonly Button _historyVerifyButton = new ThemedButton();
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
        _navRail.AddItem("Registro attività", NavIcon.Activity, "Registro");
        _navRail.AddItem("Rinomina PDF", NavIcon.Pdf, "PDF");
        _navRail.Dock = DockStyle.Top;
        _navRail.Height = _navRail.PreferredRailHeight;
        _navRail.Margin = Padding.Empty;
        _navRail.SelectionChanged += (_, _) => ApplySection();

        // Barra laterale in stile macOS: pannello arrotondato più scuro della tela, voci a pillola piena.
        var railHost = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.Sidebar,
            SurfaceBorder = Theme.Sidebar,
            Radius = 14,
            Margin = new Padding(0, 0, 12, 0),
            Padding = new Padding(4, 10, 4, 4)
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
        _sectionHost.Controls.Add(BuildActivitySection());
        _sectionHost.Controls.Add(BuildPdfSection());
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

        // L'elenco dei risultati della ricerca sta sopra a tutto, sotto il campo della testata.
        Controls.Add(_searchPopup);
        _searchPopup.BringToFront();
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
        // Le tre etichette restano il punto in cui il resto del codice aggiorna lo stato; non sono mostrate:
        // le legge e le disegna il blocco, che si ridisegna a ogni loro cambiamento.
        _railDataLabel.ForeColor = Theme.SuccessInk;
        _railDataLabel.Text = "●  Database collegato";
        _railBackupLabel.Text = "backup automatici";
        _railCompanyLabel.Text = "nessuna azienda";
        return new RailStatusCard(_railDataLabel, RunningVersion.ToString(3))
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            Margin = Padding.Empty
        };
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

    private SectionTransition? _sectionTransition;
    private int _shownSectionIndex = -1;

    /// <summary>Mostra la sezione scelta e aggiorna i dati che quella sezione mostra.</summary>
    private void ApplySection()
    {
        var index = _navRail.SelectedIndex;
        if (_conversionSection is null)
        {
            return;
        }
        var changed = _shownSectionIndex >= 0 && _shownSectionIndex != index;
        _shownSectionIndex = index;
        _dashboardSection.Visible = index == SectionDashboard;
        _conversionSection.Visible = index == SectionConversion;
        _companiesSection.Visible = index == SectionCompanies;
        _codesSection.Visible = index == SectionCodes;
        _historySection.Visible = index == SectionHistory;
        _activitySection.Visible = index == SectionActivity;
        _pdfSection.Visible = index == SectionPdf;

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
            case SectionActivity:
                RefreshActivity();
                break;
        }

        // Dissolvenza in entrata della sezione nuova (solo quando si cambia davvero sezione).
        if (changed)
        {
            _sectionTransition ??= new SectionTransition(_sectionHost);
            _sectionTransition.Play(index switch
            {
                SectionDashboard => _dashboardSection,
                SectionConversion => _conversionSection,
                SectionCompanies => _companiesSection,
                SectionCodes => _codesSection,
                SectionActivity => _activitySection,
                SectionPdf => _pdfSection,
                _ => _historySection
            });
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
        _dashExportsGrid.UseRowSelection();
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
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
            Text = "Codici salvati su questo PC."
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
        var save = Theme.CreateButton("Salva abbinamenti", Theme.ButtonTone.Primary).WithIcon(Glyph.Save);
        save.Size = new Size(178, 36);
        save.Margin = new Padding(8, 0, 0, 0);
        save.Click += (_, _) => SaveCodesPage();
        Describe(save, "Salva abbinamenti", "Salva nel database locale i codici inseriti nelle due griglie.");
        var reload = Theme.CreateButton("Ricarica elenchi", Theme.ButtonTone.Neutral).WithIcon(Glyph.Refresh);
        reload.Size = new Size(150, 36);
        reload.Margin = Padding.Empty;
        reload.Click += (_, _) =>
        {
            _codesLoaded = false;
            EnsureCodesLoaded();
            SetStatus("●   Elenchi dei codici ricaricati dal database locale.", false);
        };
        Describe(reload, "Ricarica elenchi", "Rilegge dal database gli abbinamenti salvati.");
        var delete = Theme.CreateButton("Elimina", Theme.ButtonTone.Neutral).WithIcon(Glyph.Trash);
        delete.Size = new Size(116, 36);
        delete.Margin = new Padding(0, 0, 8, 0);
        delete.Click += (_, _) => DeleteSelectedCode();
        Describe(
            delete,
            "Elimina la riga selezionata",
            "Toglie dagli abbinamenti il dipendente o il giustificativo selezionato (tasto Canc). Se è nel file Excel aperto resta da compilare di nuovo.");
        _mappingPanel.AllowDelete = true;
        _mappingPanel.DeleteRequested += (_, _) => DeleteSelectedCode();
        var transfer = CreateTransferButton(() => Theme.CreateButton("Trasferisci", Theme.ButtonTone.Neutral).WithIcon(Glyph.Transfer));
        transfer.Size = new Size(138, 36);
        transfer.Margin = new Padding(0, 0, 8, 0);
        actions.Controls.Add(save);
        actions.Controls.Add(reload);
        actions.Controls.Add(delete);
        actions.Controls.Add(transfer);
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
                _requests.Select(request => request.Policy).Distinct(StringComparer.OrdinalIgnoreCase),
                null,
                _database.GetEmployeeProfiles());
            _codesLoaded = true;
            WireCodesAutosave();
        }
        catch (SqliteException exception)
        {
            ShowError("Impossibile leggere gli abbinamenti", exception);
        }
    }

    /// <summary>Elimina dagli abbinamenti la riga selezionata (dipendente o giustificativo), dopo conferma.</summary>
    private void DeleteSelectedCode()
    {
        if (!_codesLoaded || _mappingPanel.CurrentEntry() is not { } entry)
        {
            SetStatus("●   Seleziona la riga da eliminare.", true);
            return;
        }
        var what = entry.IsPolicy ? "il giustificativo" : "il dipendente";
        var extra = entry.IsPolicy ? string.Empty : " con le sue ore e il suo periodo";
        if (MessageBox.Show(
                this,
                $"Eliminare {what} «{entry.Name}»{extra} dagli abbinamenti?{Environment.NewLine}{Environment.NewLine}" +
                "I file XML già esportati non cambiano. Se compare ancora nel file Excel aperto, dovrai inserirne di nuovo il codice.",
                "Elimina abbinamento",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }
        try
        {
            if (entry.IsPolicy)
            {
                _database.DeletePolicyMapping(entry.Name);
            }
            else
            {
                _database.DeleteEmployeeMapping(entry.Name);
            }
        }
        catch (SqliteException exception)
        {
            ShowError("Impossibile eliminare l'abbinamento", exception);
            return;
        }
        _mappingPanel.RemoveEntry(entry.Name, entry.IsPolicy);
        InvalidatePreview();
        SetStatus($"●   {(entry.IsPolicy ? "Giustificativo" : "Dipendente")} «{entry.Name}» eliminato dagli abbinamenti.", false);
        Audit(ActivityKind.Codes, $"{(entry.IsPolicy ? "Giustificativo" : "Dipendente")} «{entry.Name}» eliminato dagli abbinamenti");
    }

    private void SaveCodesPage()
    {
        // «Salva abbinamenti» salva anche a metà: i codici mancanti non bloccano.
        SaveCodesDraft(announce: true);
    }

    private readonly System.Windows.Forms.Timer _codesAutosave = new() { Interval = 1200 };
    private bool _codesAutosaveWired;

    /// <summary>
    /// Salva ciò che è scritto nelle griglie dei codici anche se incompleto. Con <paramref name="announce"/> è il
    /// pulsante «Salva abbinamenti» (messaggio completo e anteprima da rigenerare); senza è il salvataggio
    /// automatico o quello alla chiusura (silenzioso, non tocca l'anteprima).
    /// </summary>
    private void SaveCodesDraft(bool announce)
    {
        if (!_codesLoaded || (!announce && !_mappingPanel.HasUnsavedChanges))
        {
            return;
        }
        _mappingPanel.TrySave(out var note, allowIncomplete: true);
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
            foreach (var profile in _mappingPanel.EmployeeProfiles)
            {
                _database.SaveEmployeeProfile(profile.Key, profile.Value);
            }
        }
        catch (SqliteException exception)
        {
            if (announce)
            {
                ShowError("Impossibile salvare gli abbinamenti", exception);
            }
            return;
        }

        var missing = _mappingPanel.MissingRequiredCount;
        if (announce)
        {
            InvalidatePreview();
            SetStatus(
                note is not null
                    ? $"●   Abbinamenti salvati in parte. {note}"
                    : _mappingPanel.Warnings is not null
                        ? $"●   {_mappingPanel.Warnings}"
                        : missing > 0
                        ? $"●   Abbinamenti salvati: mancano ancora {missing:N0} codici richiesti per creare l'anteprima."
                        : "●   Abbinamenti salvati nel database locale.",
                note is not null || missing > 0 || _mappingPanel.Warnings is not null);
            Notify(
                missing > 0 || note is not null ? ToastKind.Info : ToastKind.Success,
                missing > 0 ? "Bozza salvata" : "Abbinamenti salvati",
                missing > 0
                    ? $"I codici inseriti restano. Mancano ancora {missing:N0} codici richiesti."
                    : "I codici sono nel database locale.");
        }
        else
        {
            SetStatus(
                missing > 0
                    ? $"●   Bozza degli abbinamenti salvata automaticamente · mancano {missing:N0} codici richiesti."
                    : "●   Abbinamenti salvati automaticamente.",
                false);
        }
    }

    /// <summary>Salvataggio automatico: poco dopo l'ultima modifica, senza che l'utente debba ricordarsene.</summary>
    private void WireCodesAutosave()
    {
        if (_codesAutosaveWired)
        {
            return;
        }
        _codesAutosaveWired = true;
        _codesAutosave.Tick += (_, _) =>
        {
            _codesAutosave.Stop();
            if (!IsDisposed)
            {
                SaveCodesDraft(announce: false);
            }
        };
        _mappingPanel.Changed += (_, _) =>
        {
            _codesAutosave.Stop();
            _codesAutosave.Start();
        };
    }

    /* ----------------------- Sezione Storico esportazioni ---------------- */

    private Control BuildHistorySection()
    {
        var page = _historySection;
        page.RowCount = 3;
        page.ColumnCount = 1;
        page.Padding = new Padding(0, 0, 4, 0);
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
        _historyGrid.UseRowSelection();
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
            Text = "Doppio clic su una riga: apre la cartella."
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

        _historyVerifyButton.Text = "Verifica file";
        Theme.StyleButton(_historyVerifyButton, Theme.ButtonTone.Neutral, compact: true);
        _historyVerifyButton.Size = new Size(124, 34);
        _historyVerifyButton.Margin = new Padding(8, 0, 0, 0);
        _historyVerifyButton.Click += (_, _) => VerifyExportFile(SelectedHistoryExport());
        Describe(
            _historyVerifyButton,
            "Verifica file",
            "Controlla che il file XML sul disco sia identico a quello consegnato (copia conservata nello storico) e che rispetti ancora il tracciato.");

        actions.Controls.Add(_historyOpenButton);
        actions.Controls.Add(_historyVerifyButton);
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
        _historyVerifyButton.Enabled = hasSelection;
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
    /// <summary>Confronta il file sul disco con la copia consegnata e lo ricontrolla contro il tracciato.</summary>
    private void VerifyExportFile(ExportRecord? record)
    {
        if (record is null)
        {
            return;
        }
        try
        {
            var stored = record.HasStoredXml ? _database.GetExportXml(record.Id) : null;
            var current = File.Exists(record.FilePath) ? File.ReadAllBytes(record.FilePath) : null;
            var result = ExportIntegrity.Check(stored, current);
            var message = ExportIntegrity.Describe(result, record.FileName, current);
            if (current is not null)
            {
                var problems = TrriPwXmlValidator.Validate(current);
                message += problems.Count == 0
                    ? Environment.NewLine + "Il file rispetta il tracciato TRRIPW."
                    : Environment.NewLine + "Il file NON rispetta il tracciato:" + Environment.NewLine +
                      string.Join(Environment.NewLine, problems.Take(4).Select(problem => "• " + problem));
            }
            SetStatus(
                result == FileIntegrity.Identical ? $"File {record.FileName}: identico a quello consegnato." : $"File {record.FileName}: da controllare.",
                result != FileIntegrity.Identical);
            Audit(
                ActivityKind.Check,
                $"Verifica del file «{record.FileName}»",
                result switch
                {
                    FileIntegrity.Identical => "identico a quello consegnato",
                    FileIntegrity.Modified => "modificato dopo l'esportazione",
                    FileIntegrity.Missing => "file non trovato",
                    _ => "copia non conservata"
                });
            ShowInformation("Verifica del file consegnato", message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SqliteException)
        {
            ShowError("Verifica del file non riuscita", exception);
        }
    }

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
            picker.ClientGuid = FileDialogs.ClientId;
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
            Audit(ActivityKind.Export, "Storico delle esportazioni svuotato", "I file XML su disco non sono stati toccati");
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
            try
            {
                var summary = MonthlySummary.Build(
                    _movements,
                    _generationOptions,
                    SafeProfiles(() => _database.GetEmployeeProfiles()),
                    _inspection?.PeriodStart,
                    _inspection?.PeriodEnd);
                _database.SaveMonthlyTotals(companyCode, MonthlyTrend.FromSummary(companyCode, summary));
                _database.SaveAbsenceTotals(companyCode, LeaveBalance.FromSummary(companyCode, summary));
            }
            catch (SqliteException)
            {
                // L'andamento è un di più: lo storico delle esportazioni si salva lo stesso.
            }
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
            MarkSessionDirty();
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
        // Le due righe hanno l'altezza del loro testo (TextBlock): titolo e sottotitolo non si sovrappongono
        // a nessuna scala dello schermo.
        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty,
            Padding = new Padding(0, 2, 0, 12)
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        heading.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        heading.Controls.Add(new TextBlock
        {
            Text = title,
            Dock = DockStyle.Top,
            ForeColor = Theme.Ink,
            Font = Theme.Fonts.CardTitle,
            Margin = Padding.Empty
        }, 0, 0);
        heading.Controls.Add(new TextBlock
        {
            Text = subtitle,
            Dock = DockStyle.Top,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Small,
            Margin = new Padding(1, 2, 0, 0)
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

    private static Control BuildSectionHint(string text) => new HintLine(text)
    {
        Dock = DockStyle.Fill,
        Margin = new Padding(2, 2, 0, 0)
    };

    /* --------------------- Ricerca, avvio e dati differiti --------------- */

    private readonly RoundedField _headerSearch = new();
    private readonly SearchResultsPopup _searchPopup = new();
    private Dictionary<string, string> _searchEmployees = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _searchPolicies = new(StringComparer.OrdinalIgnoreCase);
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
        _headerSearch.Placeholder = "Cerca o scrivi > per i comandi  (Ctrl+K)";
        _headerSearch.Input.AccessibleName = "Cerca nei movimenti, aziende, codici ed esportazioni, oppure scrivi > per i comandi";
        _headerSearch.TextChanged += (_, _) => SearchFromHeader();
        _headerSearch.Input.GotFocus += (_, _) =>
        {
            RefreshSearchSources();
            ComputeSearchHits(_headerSearch.Value);
            UpdateSearchPopup();
        };
        _headerSearch.Input.LostFocus += (_, _) => _searchPopup.Visible = false;
        _headerSearch.Input.KeyDown += HeaderSearchKeyDown;
        _searchPopup.HitChosen += OnSearchHitChosen;
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
        PositionSearchPopup();
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
            ComputeSearchHits(value);
            var commandsOnly = CommandCatalog.IsCommandQuery(value);
            // Se oltre ai movimenti non c'è altro da proporre la ricerca fa quello che ha sempre fatto:
            // porta ai movimenti filtrati. Se ci sono aziende, codici, esportazioni o comandi, scegli tu.
            if (value.Length > 0 && !commandsOnly && !_searchHasOthers)
            {
                ShowSection(SectionConversion);
                _previewTabs.SelectedIndex = 0;
            }
            // Il filtro vero resta quello dell'area di revisione: qui si scrive
            // nello stesso campo, così la ricerca si comporta in un modo solo.
            // Con «>» si cercano comandi: i movimenti non vanno filtrati con quel testo.
            _previewSearch.Value = commandsOnly ? string.Empty : value;
            UpdateSearchPopup();
        }
        finally
        {
            _searchSyncing = false;
        }
    }

    /// <summary>Dipendenti e giustificativi si leggono dal database una volta a ogni ingresso nel campo, non a ogni tasto.</summary>
    private void RefreshSearchSources()
    {
        try
        {
            _searchEmployees = new Dictionary<string, string>(_database.GetEmployeeMappings(), StringComparer.OrdinalIgnoreCase);
            _searchPolicies = new Dictionary<string, string>(_database.GetPolicyMappings(), StringComparer.OrdinalIgnoreCase);
        }
        catch (SqliteException)
        {
            // La ricerca resta su aziende ed esportazioni: nessun errore da mostrare per un di più.
        }
    }

    private bool _searchHasOthers;

    /// <summary>
    /// Risultati della ricerca e comandi per il testo scritto. Con «&gt;» in testa solo comandi; a campo vuoto i comandi
    /// suggeriti (la palette di Ctrl+K); altrimenti i dati trovati con, in fondo, qualche comando pertinente.
    /// </summary>
    private void ComputeSearchHits(string value)
    {
        var commandsOnly = CommandCatalog.IsCommandQuery(value);
        var trimmed = value.Trim();
        IReadOnlyList<SearchHit> data = commandsOnly
            ? []
            : GlobalSearch.Find(value, _movements.Count, _companies, _searchEmployees, _searchPolicies, _exports);
        var wantsCommands = commandsOnly || trimmed.Length == 0 || trimmed.Length >= GlobalSearch.MinimumLength;
        var commands = wantsCommands
            ? CommandCatalog.Find(value, IsCommandAvailable)
            : (IReadOnlyList<AppCommand>)[];
        var hits = CommandCatalog.Compose(data, commands, commandsOnly);
        _searchPopup.SetHits(hits);
        _searchHasOthers = hits.Any(hit => hit.Kind != SearchKind.Movements);
    }

    /// <summary>Un comando è offerto solo se si può eseguire adesso (per esempio «Salva l'XML» solo con un'anteprima pronta).</summary>
    private bool IsCommandAvailable(AppCommand command) => command.Id switch
    {
        "preview" => _generateButton.Enabled,
        "export" => _exportButton.Enabled,
        "remove-file" => _sourcePath is not null,
        "theme-dark" => ThemePreference.Load() != ThemeMode.Dark,
        "theme-light" => ThemePreference.Load() != ThemeMode.Light,
        "theme-system" => ThemePreference.Load() != ThemeMode.System,
        _ => true
    };

    /// <summary>Esegue il comando scelto dalla palette.</summary>
    private void RunCommand(string id)
    {
        switch (id)
        {
            case "import":
                _ = ImportWorkbookAsync();
                break;
            case "preview":
                GeneratePreview();
                break;
            case "export":
                ExportXml();
                break;
            case "remove-file":
                RemoveWorkbook();
                break;
            case "go-dashboard":
                ShowSection(SectionDashboard);
                break;
            case "go-convert":
                ShowSection(SectionConversion);
                break;
            case "go-companies":
                ShowSection(SectionCompanies);
                break;
            case "go-codes":
                ShowSection(SectionCodes);
                break;
            case "go-history":
                ShowSection(SectionHistory);
                break;
            case "go-activity":
                ShowSection(SectionActivity);
                break;
            case "settings":
                OpenSettings();
                break;
            case "theme-dark":
                ChangeThemeFromCommand(ThemeMode.Dark);
                break;
            case "theme-light":
                ChangeThemeFromCommand(ThemeMode.Light);
                break;
            case "theme-system":
                ChangeThemeFromCommand(ThemeMode.System);
                break;
            case "rename-pdf":
                ShowSection(SectionPdf);
                break;
            case "backup":
                BackupDatabaseNow();
                break;
            case "open-data":
                OpenDataFolder();
                break;
            case "open-codes-backup":
                OpenCodesBackupFolder();
                break;
            case "updates":
                _ = CheckForUpdatesAsync(userInitiated: true);
                break;
            case "shortcuts":
                ShowShortcuts();
                break;
        }
    }

    private void ChangeThemeFromCommand(ThemeMode mode)
    {
        if (ThemePreference.Save(mode))
        {
            SetStatus("●   Tema salvato: chiudi e riapri il programma per vederlo.", false);
            Audit(ActivityKind.Settings, "Tema cambiato dalla palette comandi", ThemeChoice.Serialize(mode));
        }
        else
        {
            SetStatus("Impossibile salvare la scelta del tema: resta quello di prima.", true);
        }
    }

    private void UpdateSearchPopup()
    {
        var show = _headerSearch.Input.Focused && _searchHasOthers;
        if (show)
        {
            PositionSearchPopup();
        }
        _searchPopup.Visible = show;
        if (show)
        {
            _searchPopup.BringToFront();
        }
    }

    private void PositionSearchPopup()
    {
        if (_headerSearch.Parent is null || _searchPopup.HitCount == 0)
        {
            return;
        }
        var origin = PointToClient(_headerSearch.Parent.PointToScreen(_headerSearch.Location));
        _searchPopup.SetBounds(
            origin.X,
            origin.Y + _headerSearch.Height + 4,
            _headerSearch.Width,
            _searchPopup.PreferredHeight);
    }

    private void HeaderSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_searchPopup.Visible)
        {
            return;
        }
        switch (e.KeyCode)
        {
            case Keys.Down:
                _searchPopup.MoveSelection(1);
                break;
            case Keys.Up:
                _searchPopup.MoveSelection(-1);
                break;
            case Keys.Enter:
                _searchPopup.ChooseCurrent();
                break;
            case Keys.Escape:
                _searchPopup.Visible = false;
                break;
            default:
                return;
        }
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    /// <summary>Apre la sezione giusta e, dove si può, seleziona la riga trovata.</summary>
    private void OnSearchHitChosen(SearchHit hit)
    {
        _searchPopup.Visible = false;
        if (hit.Kind == SearchKind.Command)
        {
            // Il testo cercato ha fatto il suo lavoro: si svuota il campo (e il filtro dei movimenti) prima di agire.
            _searchSyncing = true;
            try
            {
                _headerSearch.Value = string.Empty;
                _previewSearch.Value = string.Empty;
            }
            finally
            {
                _searchSyncing = false;
            }
            Focus();
            RunCommand(hit.Key);
            return;
        }
        switch (hit.Kind)
        {
            case SearchKind.Movements:
                ShowSection(SectionConversion);
                _previewTabs.SelectedIndex = 0;
                _previewSearch.Value = hit.Key;
                break;
            case SearchKind.Company:
                ShowSection(SectionCompanies);
                SelectGridRow(_companiesGrid, row => row.Tag is CompanyRecord company &&
                    string.Equals(company.Code, hit.Key, StringComparison.OrdinalIgnoreCase));
                break;
            case SearchKind.Employee:
            case SearchKind.Policy:
                ShowSection(SectionCodes);
                _mappingPanel.SelectTab(hit.Kind == SearchKind.Employee ? 0 : 1);
                break;
            case SearchKind.Export:
                ShowSection(SectionHistory);
                SelectGridRow(_historyGrid, row => row.Tag is ExportRecord record &&
                    record.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) == hit.Key);
                break;
        }
    }

    private static void SelectGridRow(DataGridView grid, Func<DataGridViewRow, bool> match)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (!match(row))
            {
                continue;
            }
            grid.ClearSelection();
            row.Selected = true;
            if (row.Cells.Count > 0 && row.Cells[0].Visible)
            {
                grid.CurrentCell = row.Cells[0];
            }
            if (row.Index >= 0 && row.Index < grid.Rows.Count)
            {
                grid.FirstDisplayedScrollingRowIndex = Math.Max(0, row.Index - 2);
            }
            grid.Focus();
            return;
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
