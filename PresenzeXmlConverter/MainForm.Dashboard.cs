namespace PresenzeXmlConverter;

/// <summary>
/// Dashboard: riepilogo a colpo d'occhio. In alto quattro schede con icona; sotto,
/// a sinistra l'area di rilascio del foglio con l'avanzamento del lavoro e le azioni, a
/// destra lo stato del sistema (file, database, aggiornamenti, regole di conversione); in fondo le ultime
/// consegne. Le altezze sono dichiarate: nessuna scheda si allunga a riempire spazio vuoto.
/// </summary>
internal sealed partial class MainForm
{
    private readonly MetricCard _kpiRequests = new("RICHIESTE", Glyph.File, Theme.BrandInk, Theme.BrandSoft);
    private readonly MetricCard _kpiEmployees = new("DIPENDENTI", Glyph.Users, Theme.SuccessInk, Theme.SuccessSoft);
    private readonly MetricCard _kpiMovements = new("MOVIMENTI", Glyph.Table, Theme.BrandInk, Theme.BrandSoft);
    private readonly MetricCard _kpiHours = new("ORE ASSEGNATE", Glyph.Clock, Theme.SuccessInk, Theme.SuccessSoft);
    private readonly DropZone _dashDropZone = new();
    private readonly StepperBar _dashStepper = new();
    private readonly StatusRow _statusFile = new("File Excel", Glyph.File);
    private readonly StatusRow _statusDatabase = new("Database", Glyph.Database);
    private readonly StatusRow _statusUpdates = new("Aggiornamenti", Glyph.Refresh);
    private readonly StatusRow _statusRules = new("Regole", Glyph.Chip);
    private readonly StatusRow _statusCertificate = new("Certificato", Glyph.Shield);

    // La firma si legge dal file una volta sola: la dashboard si aggiorna spesso.
    private static readonly Lazy<DateTime?> CertificateExpiry = new(ExecutableSignature.CurrentCertificateExpiry);
    private readonly DashboardInsightsView _dashInsights = new();
    private IconButton? _dashGenerateButton;

    /// <summary>La consegna in anteprima è già stata esportata? Serve al terzo passo dell'avanzamento.</summary>
    private bool _currentPreviewExported;

    private Control BuildDashboardSection()
    {
        var page = _dashboardSection;
        page.RowCount = 5;
        page.ColumnCount = 1;
        page.Padding = new Padding(0, 0, 4, 0);
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 246));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

        page.Controls.Add(BuildSectionHeading(
            "Dashboard",
            "Riepilogo della conversione in corso e delle ultime esportazioni."), 0, 0);

        // Quattro schede di riepilogo.
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
        var cards = new[] { _kpiRequests, _kpiEmployees, _kpiMovements, _kpiHours };
        for (var index = 0; index < cards.Length; index++)
        {
            cards[index].Dock = DockStyle.Fill;
            cards[index].Margin = new Padding(0, 0, index == cards.Length - 1 ? 0 : 10, 10);
            metrics.Controls.Add(cards[index], index, 0);
        }
        page.Controls.Add(metrics, 0, 1);

        // Conversione (a sinistra) e stato del sistema (a destra).
        var hero = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        hero.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        hero.Controls.Add(BuildDashboardConversionCard(), 0, 0);
        hero.Controls.Add(BuildDashboardStatusCard(), 1, 0);
        page.Controls.Add(hero, 0, 2);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var exports = BuildDashboardExports();
        exports.Margin = new Padding(0, 0, 10, 0);
        bottom.Controls.Add(exports, 0, 0);
        bottom.Controls.Add(BuildDashboardInsightsCard(), 1, 0);
        page.Controls.Add(bottom, 0, 3);
        page.Controls.Add(BuildSectionHint(
            "Suggerimento: puoi trascinare un file Excel (.xlsx, .xlsm) in qualunque punto della finestra."), 0, 4);

        _dashDropZone.Click += async (_, _) => await ImportWorkbookAsync();
        _dashDropZone.FileDropped += async path => await LoadWorkbookAsync(path);
        _dashDropZone.RemoveRequested += RemoveWorkbook;
        RefreshDashboardCards();
        return page;
    }

    private Control BuildDashboardConversionCard()
    {
        var card = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.Surface,
            SurfaceBorder = Theme.Divider,
            Radius = 14,
            Padding = new Padding(20, 14, 20, 14),
            Margin = new Padding(0, 0, 10, 10)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        layout.Controls.Add(CreateCardTitle("NUOVA CONVERSIONE"), 0, 0);

        _dashDropZone.Dock = DockStyle.Fill;
        _dashDropZone.Margin = new Padding(0, 2, 0, 6);
        Describe(
            _dashDropZone,
            "Scegli o trascina il foglio Excel",
            "Apre un foglio .xlsx da convertire in XML TRRIPW: fai clic oppure trascina il file qui. Con la «×» (o Canc) rimuovi il file caricato.");
        layout.Controls.Add(_dashDropZone, 0, 1);

        _dashStepper.Dock = DockStyle.Fill;
        _dashStepper.Margin = new Padding(0, 0, 0, 0);
        layout.Controls.Add(_dashStepper, 0, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Surface,
            Margin = Padding.Empty,
            Padding = new Padding(0, 4, 0, 0)
        };
        _dashGenerateButton = new IconButton("Genera anteprima", Glyph.Arrow, Theme.ButtonTone.Positive)
        {
            Margin = new Padding(0, 0, 8, 0)
        };
        _dashGenerateButton.Click += (_, _) => GeneratePreview();
        Describe(_dashGenerateButton, "Genera anteprima", "Converte le richieste in movimenti giornalieri e XML (Ctrl+G).");
        actions.Controls.Add(_dashGenerateButton);

        var codes = new IconButton("Gestione codici", Glyph.Code) { Margin = new Padding(0, 0, 8, 0) };
        codes.Click += (_, _) => ShowSection(SectionCodes);
        Describe(codes, "Gestione codici", "Modifica codici PAGHE e giustificativi delle assenze.");
        actions.Controls.Add(codes);

        var history = new IconButton("Storico", Glyph.Clock) { Margin = Padding.Empty };
        history.Click += (_, _) => ShowSection(SectionHistory);
        Describe(history, "Storico esportazioni", "Le ultime consegne: file, data e totale ore.");
        actions.Controls.Add(history);
        layout.Controls.Add(actions, 0, 3);

        card.Controls.Add(layout);
        return card;
    }

    private Control BuildDashboardInsightsCard()
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
        layout.Controls.Add(CreateCardTitle("ANALISI DEL MESE"), 0, 0);
        _dashInsights.Dock = DockStyle.Fill;
        _dashInsights.Margin = Padding.Empty;
        layout.Controls.Add(_dashInsights, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildDashboardStatusCard()
    {
        var card = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.Surface,
            SurfaceBorder = Theme.Divider,
            Radius = 14,
            Padding = new Padding(20, 14, 20, 14),
            Margin = new Padding(0, 0, 0, 10)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        for (var row = 0; row < 5; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        }
        layout.Controls.Add(CreateCardTitle("STATO DEL SISTEMA"), 0, 0);
        var rows = new[] { _statusFile, _statusDatabase, _statusUpdates, _statusRules, _statusCertificate };
        for (var index = 0; index < rows.Length; index++)
        {
            rows[index].Dock = DockStyle.Fill;
            rows[index].Margin = Padding.Empty;
            layout.Controls.Add(rows[index], 0, index + 1);
        }
        card.Controls.Add(layout);
        return card;
    }

    /// <summary>
    /// Aggiorna schede, area di rilascio, avanzamento e stato del sistema. I numeri
    /// arrivano da <see cref="DashboardSummary"/>: nessun conto duplicato qui.
    /// </summary>
    private void RefreshDashboardCards()
    {
        var summary = DashboardSummary.From(_requests, _movements, _exports.Count);
        _kpiRequests.AnimateTo(summary.RequestsText);
        _kpiRequests.Caption = _inspection is null ? "nessun foglio letto" : $"dal foglio «{_inspection.SheetName}»";
        _kpiEmployees.AnimateTo(summary.EmployeesText);
        _kpiEmployees.Caption = "dipendenti distinti";
        _kpiMovements.AnimateTo(summary.MovementsText);
        _kpiMovements.Caption = summary.HasMovements
            ? $"su {summary.DaysText} giorni lavorativi"
            : "genera l'anteprima";
        // Andamento delle ultime otto esportazioni (dalla più vecchia alla più recente) accanto a tre indicatori.
        var recent = _exports.Take(8).Reverse().ToArray();
        _kpiEmployees.Spark = recent.Select(export => (float)export.Employees).ToArray();
        _kpiMovements.Spark = recent.Select(export => (float)export.Movements).ToArray();
        _kpiHours.Spark = recent.Select(export => (float)export.TotalMinutes).ToArray();
        _kpiHours.AnimateTo(summary.CompactTotalText);
        _kpiHours.Caption = "nell'anteprima corrente";
        // Confronto con l'ultima esportazione (se l'anteprima è già stata esportata, con quella prima).
        var previousExport = (_currentPreviewExported ? _exports.Skip(1) : _exports).FirstOrDefault();
        _kpiHours.Trend = TrendChip.Compare(summary.TotalMinutes, previousExport?.TotalMinutes);

        _dashInsights.Insights = _movements.Count == 0
            ? DashboardInsights.Empty
            : DashboardInsights.From(
                _movements,
                CalendarAlerts.Find(
                    _movements,
                    _generationOptions,
                    SafeProfiles(() => _database.GetEmployeeProfiles())));

        UpdateDropZones();
        _dashStepper.Current = _currentPreviewExported && !string.IsNullOrWhiteSpace(_xml)
            ? 3
            : !string.IsNullOrWhiteSpace(_xml)
                ? 2
                : _requests.Count > 0 ? 1 : 0;

        _statusFile.SetState(
            _sourcePath is null ? "nessun file" : Path.GetFileName(_sourcePath),
            _sourcePath is null ? StatusTone.Neutral : StatusTone.Good);
        RefreshDatabaseStatusRow();
        RefreshUpdatesStatusRow();
        _statusRules.SetState(
            GenerationSettings.Describe(_generationOptions),
            StatusTone.Good);
        var certificate = CertificateStatus.Describe(CertificateExpiry.Value, DateTime.Now);
        _statusCertificate.SetState(
            certificate.Text,
            certificate.Level switch
            {
                HealthLevel.Good => StatusTone.Good,
                HealthLevel.Warning => StatusTone.Warning,
                HealthLevel.Bad => StatusTone.Bad,
                _ => StatusTone.Neutral
            });

        if (_dashGenerateButton is not null)
        {
            _dashGenerateButton.Enabled = _generateButton.Enabled;
        }
    }

    private void RefreshDatabaseStatusRow()
    {
        try
        {
            var health = _database.GetHealth();
            _statusDatabase.SetState(
                health.IsTemporary ? "temporaneo" : $"collegato · {health.DescribeBackupsShort(DateTime.UtcNow)}",
                health.IsTemporary ? StatusTone.Warning : StatusTone.Good);
        }
        catch (Exception exception) when (
            exception is Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            _statusDatabase.SetState("non leggibile", StatusTone.Bad);
        }
    }

    private void RefreshUpdatesStatusRow()
    {
        var text = _updateStatusLabel.Text.Replace("Aggiornamenti", string.Empty, StringComparison.Ordinal).Trim(' ', '·')
            .Replace("token GitHub", "token", StringComparison.Ordinal);
        var warning = _updateStatusLabel.ForeColor == Theme.WarningInk;
        _statusUpdates.SetState(
            text.Length == 0 ? "verifica in corso" : text,
            warning ? StatusTone.Warning : StatusTone.Good);
    }

    /// <summary>Le aree di rilascio (dashboard e conversione) mostrano lo stesso file.</summary>
    private void UpdateDropZones()
    {
        foreach (var zone in DropZones())
        {
            zone.Busy = _isBusy;
            if (_inspection is null || _sourcePath is null)
            {
                zone.ShowEmpty();
            }
            else
            {
                var employees = _requests.Select(request => request.EmployeeName)
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .Count();
                zone.ShowLoaded(
                    Path.GetFileName(_sourcePath),
                    $"Foglio «{_inspection.SheetName}» · {_requests.Count:N0} richieste · {employees:N0} dipendenti · clic per cambiare file");
            }
        }
    }

    private IEnumerable<DropZone> DropZones()
    {
        yield return _dashDropZone;
        if (_conversionDropZone is not null)
        {
            yield return _conversionDropZone;
        }
    }

    private DropZone? _conversionDropZone;
}
