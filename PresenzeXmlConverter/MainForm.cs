using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace PresenzeXmlConverter;

internal sealed partial class MainForm : Form
{
    private readonly LocalDatabase _database = OpenDatabase();
    private readonly RoundedField _companyCode = new();
    private readonly Label _fileLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _generateButton = new ThemedButton();
    private readonly Button _sheetButton = new ThemedButton();
    private readonly IconButton _exportButton = new("Scarica XML", Glyph.Download, Theme.ButtonTone.Primary);
    private readonly ThemedGrid _movementsGrid = new();
    private readonly RichTextBox _xmlPreview = new();
    private readonly ThemedTabControl _previewTabs = new();
    private readonly Icon _applicationIcon;
    private WorkbookInspection? _inspection;
    private IReadOnlyList<AttendanceRequest> _requests = Array.Empty<AttendanceRequest>();
    private List<DailyMovement> _movements = new();
    private string? _sourcePath;
    private string? _xml;
    private bool _isPopulatingPreview;
    private bool _isBusy;
    private bool _forceClose;
    private string? _lastExportDirectory;
    private readonly NotifyIcon _trayIcon = new();
    private readonly System.Windows.Forms.Timer _xmlRefreshDebounce = new() { Interval = 350 };
    private readonly System.Windows.Forms.Timer _companyCodeSaveDebounce = new() { Interval = 700 };
    private string? _lastSourceDirectory;
    private IReadOnlyList<string> _sheetNames = Array.Empty<string>();
    private ColumnSelection? _columns;
    /// <summary>Regole generali (Impostazioni).</summary>
    private GenerationOptions _globalOptions = GenerationOptions.Default;

    /// <summary>
    /// Regole in uso per la conversione: quelle proprie dell'azienda scelta, se ci sono,
    /// altrimenti le generali.
    /// </summary>
    private GenerationOptions _generationOptions = GenerationOptions.Default;

    public MainForm()
    {
        // Il titolo dice a colpo d'occhio quale build è installata: con le Release
        // automatiche se ne succedono anche più d'una al giorno. Assegnato solo qui:
        // il layout non lo tocca più, altrimenti sovrascriverebbe la versione.
        Text = WindowCaption;
        StartPosition = FormStartPosition.CenterScreen;
        // Le dimensioni sono espresse a 96 DPI: con lo scaling DPI tutte le posizioni
        // e i caratteri crescono insieme, così niente testo più grande della sua scatola.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Theme.Fonts.Ui;
        BackColor = Theme.Canvas;
        _applicationIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        Icon = _applicationIcon;

        MeasureStartup("interfaccia", BuildLayout);
        EnableWorkbookDrop();
        // Una sola lettura per tutte le preferenze che servono all'avvio: prima
        // apriva una connessione al database per ciascuna.
        var startup = Trace is null
            ? ReadStartupSettings()
            : Trace.Measure("preferenze", ReadStartupSettings);
        _companyCode.Value = startup["company-code"];
        _lastExportDirectory = startup["last-export-dir"].Length == 0 ? null : startup["last-export-dir"];
        _xmlRefreshDebounce.Tick += (_, _) =>
        {
            _xmlRefreshDebounce.Stop();
            RefreshXmlFromPreview();
        };
        // Il codice azienda si salva da solo poco dopo l'ultima digitazione.
        _companyCodeSaveDebounce.Tick += (_, _) =>
        {
            _companyCodeSaveDebounce.Stop();
            SaveCompanyCode();
        };
        _lastSourceDirectory = startup["last-source-dir"].Length == 0 ? null : startup["last-source-dir"];
        _globalOptions = GenerationSettings.Read(_database);
        _generationOptions = _globalOptions;
        MeasureStartup("aggiornamenti", InitializeUpdates);
        SetupTrayIcon();
        UpdateButtons();
    }

    /// <summary>
    /// Esegue una fase di avvio, misurandola solo se la diagnostica è attiva.
    /// L'azione gira SEMPRE: con l'operatore condizionale sul tracciamento, spento
    /// nel caso normale, saltava anche l'azione e la finestra restava vuota.
    /// </summary>
    private static void MeasureStartup(string phase, Action action)
    {
        if (Trace is null)
        {
            action();
        }
        else
        {
            Trace.Measure(phase, action);
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        RestoreWindowBounds();
        // La geometria viene già riscalata dal AutoScaleMode.Dpi della finestra
        // (che scala bordi, righe dei layout e posizioni di tutti i controlli):
        // una seconda scalatura manuale, come c'era in precedenza, gonfiava la
        // finestra e le scatole a 125-150% lasciando invariati i font.
        // Qui serve solo riportare la finestra dentro l'area di lavoro.
        FitToScreen();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyNavWidth();
        ScheduleBoundsSave();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FitToScreen();
        // Da qui la finestra ha la scala DPI definitiva: la larghezza della barra
        // laterale può seguire la dimensione della finestra.
        _navWidthReady = true;
        ApplyNavWidth();
        StartTrackingWindowBounds();
        // Dopo che la finestra è disegnata si riempiono i dati che non servono a
        // vederla: lo storico delle esportazioni e la manutenzione del database.
        // Così l'apertura non aspetta il database.
        BeginInvoke(() =>
        {
            if (IsDisposed)
            {
                return;
            }
            LoadExportsNow();
            LoadCompaniesQuietly();
            StartDatabaseMaintenance();
            Trace?.WriteTo(StartupLogPath);
        });
    }

    /// <summary>
    /// Mantiene la finestra entro l'area di lavoro: con lo scaling DPI (125-150%)
    /// la dimensione progettata crescerebbe oltre lo schermo.
    /// </summary>
    private void FitToScreen()
    {
        var area = Screen.FromControl(this).WorkingArea;
        var clamped = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        if (clamped != Size)
        {
            Size = clamped;
        }
        var clampedMinimum = new Size(
            Math.Min(MinimumSize.Width, area.Width),
            Math.Min(MinimumSize.Height, area.Height));
        if (clampedMinimum != MinimumSize)
        {
            MinimumSize = clampedMinimum;
        }
    }

    /// <summary>
    /// Il foglio Excel si può anche trascinare sulla finestra: è la stessa cosa
    /// del dialogo di apertura, senza doverlo cercare. Durante una conversione il
    /// trascinamento non è accettato, per non sovrapporre due importazioni.
    /// </summary>
    private void EnableWorkbookDrop()
    {
        AllowDrop = true;
        DragEnter += (_, e) =>
        {
            e.Effect = !_isBusy && WorkbookDrop.FirstWorkbookPath(DroppedPaths(e.Data)) is not null
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        };
        DragDrop += async (_, e) =>
        {
            var path = WorkbookDrop.FirstWorkbookPath(DroppedPaths(e.Data));
            if (!_isBusy && path is not null)
            {
                await LoadWorkbookAsync(path);
            }
        };
    }

    /// <summary>
    /// Percorsi trascinati sulla finestra (vuoto se il trascinamento non porta file).
    /// </summary>
    private static string[] DroppedPaths(IDataObject? data) =>
        data?.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>();

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Il titolo nativo segue l'intestazione scura dell'app (Windows 10/11).
        WindowChrome.Apply(Handle);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !_forceClose)
        {
            var behavior = _database.GetSetting("close-behavior");
            if (behavior is not ("tray" or "exit"))
            {
                // La scelta viene proposta una sola volta: se l'utente spunta
                // "ricorda", non verrà più richiesta.
                using var dialog = new CloseBehaviorDialog();
                if (!CloseBehaviorDialog.IsChoice(dialog.ShowDialog(this)))
                {
                    e.Cancel = true;
                    return;
                }
                behavior = dialog.SelectedBehavior;
                if (dialog.RememberChoice)
                {
                    _database.SaveSetting("close-behavior", behavior);
                }
            }

            if (behavior == "tray")
            {
                e.Cancel = true;
                MinimizeToTray();
                return;
            }
        }

        // Chiusura vera. Sono tutte operazioni "di cortesia" (posizione della finestra,
        // copia di sicurezza, icona del tray): se una fallisce, l'uscita non deve
        // fermarsi, altrimenti il programma sembra non rispondere al comando Esci.
        try
        {
            _boundsSaveTimer.Stop();
            SaveWindowBounds();
            _database.MaintainOnExit();
        }
        catch (Exception)
        {
            // Salvataggio best-effort: niente deve impedire la chiusura.
        }
        try
        {
            _trayCleanup.Stop();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        catch (Exception)
        {
        }
        base.OnFormClosing(e);
    }

    /// <summary>
    /// Chiude il programma per davvero (non in tray). Se la chiusura normale venisse
    /// annullata da qualcosa, il ciclo dei messaggi viene fermato comunque.
    /// </summary>
    private void ExitApplication()
    {
        _forceClose = true;
        try
        {
            Close();
        }
        finally
        {
            if (!IsDisposed)
            {
                Application.Exit();
            }
        }
    }

    /// <summary>
    /// Un altro avvio dell'app ha chiesto di mostrarsi: la finestra torna in primo piano
    /// (dal tray o da ridotta a icona). Arriva da un thread del pool.
    /// </summary>
    internal void ActivateFromAnotherInstance()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }
        try
        {
            BeginInvoke(RestoreFromTray);
        }
        catch (InvalidOperationException)
        {
            // La finestra si sta chiudendo.
        }
    }

    private void SetupTrayIcon()
    {
        _trayIcon.Icon = _applicationIcon;
        _trayIcon.Text = WindowCaption;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Apri Vyrn Presence", null, (_, _) => RestoreFromTray());
        AddUpdateCommands(menu);
        AddDataCommands(menu);
        menu.Items.Add(new ToolStripSeparator());
        var notifications = new ToolStripMenuItem("Notifiche di fine conversione")
        {
            CheckOnClick = true,
            Checked = !string.Equals(_database.GetSetting("notifications"), "false", StringComparison.OrdinalIgnoreCase)
        };
        notifications.CheckedChanged += (_, _) =>
        {
            _database.SaveSetting("notifications", notifications.Checked ? "true" : "false");
            SetStatus(notifications.Checked ? "●   Notifiche attivate." : "●   Notifiche disattivate.", false);
        };
        menu.Items.Add(notifications);
        menu.Items.Add("Alla chiusura chiedi di nuovo", null, (_, _) =>
        {
            _database.SaveSetting("close-behavior", string.Empty);
            SetStatus("●   Alla chiusura della finestra il programma tornerà a chiedere come comportarsi.", false);
        });
        menu.Items.Add("Esci definitivamente", null, (_, _) => ExitApplication());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        // Un clic sulla notifica riporta la finestra davanti.
        _trayIcon.BalloonTipClicked += (_, _) => RestoreFromTray();
        _trayCleanup.Tick += (_, _) =>
        {
            _trayCleanup.Stop();
            // Con la finestra aperta l'icona serviva solo alla notifica: non resta nel tray.
            if (!IsDisposed && Visible)
            {
                _trayIcon.Visible = false;
            }
        };
    }

    private readonly System.Windows.Forms.Timer _trayCleanup = new() { Interval = 20_000 };

    /// <summary>
    /// L'utente sta guardando la finestra? Solo allora una notifica è superflua: una
    /// finestra aperta ma dietro altre, o ridotta a icona nella barra, non conta
    /// (prima bastava che fosse "visibile" e la notifica non arrivava mai).
    /// </summary>
    private bool IsWindowInUse =>
        Visible && WindowState != FormWindowState.Minimized && ReferenceEquals(Form.ActiveForm, this);

    /// <summary>
    /// Mostra una notifica dall'icona del tray. L'icona deve essere registrata da
    /// Windows prima di poter mostrare un fumetto: se è appena stata resa visibile la
    /// prima notifica andrebbe persa, quindi si attende un istante.
    /// </summary>
    private void ShowTrayNotification(string title, string text, int milliseconds = 6000)
    {
        if (IsDisposed || string.Equals(_database.GetSetting("notifications"), "false", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        try
        {
            var alreadyRegistered = _trayIcon.Visible;
            _trayIcon.Visible = true;

            void Show()
            {
                if (IsDisposed)
                {
                    return;
                }
                _trayIcon.ShowBalloonTip(milliseconds, title, text, ToolTipIcon.Info);
                _trayCleanup.Stop();
                _trayCleanup.Interval = milliseconds + 15_000;
                _trayCleanup.Start();
            }

            if (alreadyRegistered)
            {
                Show();
                return;
            }
            var delay = new System.Windows.Forms.Timer { Interval = 600 };
            delay.Tick += (_, _) =>
            {
                delay.Stop();
                delay.Dispose();
                Show();
            };
            delay.Start();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or System.ComponentModel.Win32Exception)
        {
            // Una notifica mancata non deve mai disturbare il lavoro.
        }
    }

    private void MinimizeToTray()
    {
        SaveWindowBounds();
        Hide();
        ShowTrayNotification(
            "Vyrn Presence in background",
            "Il programma resta attivo in tray: riceverai una notifica quando una conversione termina.",
            4000);
    }

    private void RestoreFromTray()
    {
        _trayIcon.Visible = false;
        Show();
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }
        Activate();
    }

    private void NotifyConversionCompleted(int movementCount)
    {
        if (IsWindowInUse)
        {
            return;
        }
        ShowTrayNotification(
            "Conversione completata",
            $"{movementCount:N0} movimenti pronti. Apri Vyrn Presence per revisionare ed esportare l'XML.");
    }

    private async Task ImportWorkbookAsync()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Seleziona il file presenze da convertire",
            Filter = WorkbookDrop.OpenFileFilter,
            CheckFileExists = true,
            Multiselect = false
        };
        if (!string.IsNullOrEmpty(_lastSourceDirectory) && Directory.Exists(_lastSourceDirectory))
        {
            picker.InitialDirectory = _lastSourceDirectory;
        }
        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await LoadWorkbookAsync(picker.FileName);
    }

    /// <summary>
    /// Importa il foglio indicato: ci arrivano sia il dialogo di apertura sia il
    /// file trascinato sulla finestra, così le due strade si comportano allo
    /// stesso modo (stesso controllo dei codici, stesse segnalazioni).
    /// </summary>
    private async Task LoadWorkbookAsync(string path)
    {
        try
        {
            SetBusy(true, "Lettura del file Excel in corso…");
            WorkbookInspection inspection;
            try
            {
                inspection = await Task.Run(() => AttendanceWorkbookReader.Inspect(path));
            }
            finally
            {
                SetBusy(false);
            }
            var columns = inspection.SuggestedColumns;
            if (columns is null)
            {
                using var mappingDialog = new ColumnMappingDialog(inspection.Headers, null);
                if (mappingDialog.ShowDialog(this) != DialogResult.OK || mappingDialog.Selection is null)
                {
                    return;
                }
                columns = mappingDialog.Selection;
            }

            var read = inspection.Read(columns, _generationOptions.HoursPerDay);
            _requests = read.Requests;
            SetSourceProblems(read.Problems);
            _inspection = inspection;
            _columns = columns;
            _sourcePath = path;
            LoadSheetNames(path, inspection.SheetNames);
            var sourceDirectory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(sourceDirectory))
            {
                _lastSourceDirectory = sourceDirectory;
                _database.SaveSetting("last-source-dir", sourceDirectory);
            }
            _fileLabel.Text = Path.GetFileName(path);
            ClearGeneratedPreview();
            ResetReview();
            UpdateWorkflowSummary();
            _previewTitleLabel.Text = "File importato · pronto per la conversione";
            _previewSubtitleLabel.Text = "Controlla l’azienda e i codici, quindi genera l’anteprima dei movimenti.";
            // Un file appena importato porta l'utente dove si lavora, anche se era
            // sulla dashboard: i codici da verificare sono cambiati.
            _codesLoaded = false;
            ShowSection(SectionConversion);
            RefreshDashboard();
            SetStatus("●   File importato correttamente", false);
            UpdateButtons();
        }
        catch (Exception exception) when (IsExpectedInputError(exception) ||
                                          exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            ShowError("Importazione Excel non riuscita", exception);
        }
    }

    private async void GeneratePreview()
    {
        if (_inspection is null || _requests.Count == 0)
        {
            MessageBox.Show(this, "Importa prima un file Excel.", "File mancante", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!TryValidateCode(_companyCode.Value, 6, "codice azienda", out var companyCode))
        {
            return;
        }

        try
        {
            _database.SaveSetting("company-code", companyCode);
            var requiredEmployees = _requests.Select(request => request.EmployeeName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var requiredPolicies = _requests.Select(request => request.Policy)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var employees = _database.GetEmployeeMappings();
            if (!SynchronizeEmployeeCodesFromWorkbook(employees))
            {
                return;
            }
            employees = _database.GetEmployeeMappings();
            var policies = _database.GetPolicyMappings();
            var missingEmployees = requiredEmployees.Where(name => !employees.ContainsKey(name)).ToArray();
            var missingPolicies = requiredPolicies.Where(policy => !policies.ContainsKey(policy)).ToArray();

            if (missingEmployees.Length > 0 || missingPolicies.Length > 0)
            {
                using var dialog = new MappingEditorDialog(
                    employees,
                    policies,
                    missingEmployees,
                    missingPolicies,
                    new Dictionary<string, string>());
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    SetStatus("Conversione annullata: gli abbinamenti richiesti non sono stati salvati.", true);
                    return;
                }
                SaveMappings(dialog);
                employees = _database.GetEmployeeMappings();
                policies = _database.GetPolicyMappings();
            }

            SetBusy(true, "Generazione dei movimenti e dell'XML in corso…");
            GenerationResult result;
            string xml;
            try
            {
                (result, xml) = await Task.Run(() =>
                {
                    var generation = AttendanceMovementGenerator.Generate(
                        _requests,
                        employees,
                        policies,
                        _inspection.PeriodStart,
                        _inspection.PeriodEnd,
                        _generationOptions);
                    var document = generation.Movements.Count == 0
                        ? string.Empty
                        : TrriPwXmlWriter.Create(companyCode, generation.Movements);
                    return (generation, document);
                });
            }
            finally
            {
                SetBusy(false);
            }
            if (result.Movements.Count == 0)
            {
                throw new InvalidDataException("Non è stato generato alcun movimento. Controlla date, durata e periodo del foglio.");
            }

            _movements = result.Movements.ToList();
            _xml = xml;
            _currentPreviewExported = false;
            PopulatePreview(result.Movements);
            ShowXmlPreview(_xml);
            UpdatePreviewSummary(result.Movements);
            // I controlli girano sempre: prima dell'esportazione l'utente deve
            // vedere che cosa richiede attenzione, non solo le ore prodotte.
            SetGenerationWarnings(result.Warnings);
            RunDeterministicChecks(result.Movements);
            UpdateButtons();
            NotifyConversionCompleted(result.Movements.Count);
            if (ErrorCount > 0)
            {
                _previewTabs.SelectedIndex = ReviewTabIndex;
                SetStatus(
                    $"{ErrorCount:N0} errori e {WarningCount:N0} attenzioni da verificare nella scheda Controlli.",
                    true);
                ShowReviewDialog();
            }
            else if (WarningCount > 0)
            {
                _previewTabs.SelectedIndex = ReviewTabIndex;
                SetStatus(
                    $"{WarningCount:N0} segnalazioni da verificare: le trovi nella scheda Controlli.",
                    true);
            }
            else
            {
                _previewTabs.SelectedIndex = 0;
                SetStatus("Anteprima pronta. Controlla i movimenti e il contenuto XML prima di scaricare.", false);
            }
            UpdateWorkflowSummary();
        }
        catch (Exception exception) when (IsExpectedInputError(exception))
        {
            ShowError("Generazione XML non riuscita", exception);
        }
    }

    /// <summary>
    /// Riepilogo modale delle segnalazioni più gravi: si apre solo quando ci sono
    /// errori, così il caso normale resta senza finestre da chiudere.
    /// </summary>
    private void ShowReviewDialog()
    {
        var lines = _findings
            .Where(finding => finding.Severity != MovementSeverity.Note)
            .Take(12)
            .Select(finding => $"[{finding.Label}] {finding.Message}")
            .ToArray();
        if (lines.Length == 0)
        {
            return;
        }
        var text = string.Join(Environment.NewLine, lines);
        if (ErrorCount + WarningCount > lines.Length)
        {
            text += $"{Environment.NewLine}… e altre {ErrorCount + WarningCount - lines.Length} segnalazioni nella scheda Controlli.";
        }
        MessageBox.Show(
            this,
            text,
            "Controlli sulla conversione",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void EditMappings()
    {
        try
        {
            var employees = _database.GetEmployeeMappings();
            var policies = _database.GetPolicyMappings();
            var requiredEmployees = _requests.Select(request => request.EmployeeName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var requiredPolicies = _requests.Select(request => request.Policy).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            using var dialog = new MappingEditorDialog(employees, policies, requiredEmployees, requiredPolicies);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                SaveMappings(dialog);
                InvalidatePreview();
                SetStatus("Abbinamenti aggiornati nel database locale.", false);
            }
        }
        catch (SqliteException exception)
        {
            ShowError("Impossibile leggere o salvare gli abbinamenti", exception);
        }
    }

    private void SaveMappings(MappingEditorDialog dialog)
    {
        foreach (var mapping in dialog.EmployeeMappings)
        {
            _database.SaveEmployeeMapping(mapping.Key, mapping.Value);
        }
        foreach (var mapping in dialog.PolicyMappings)
        {
            _database.SavePolicyMapping(mapping.Key, mapping.Value);
        }
    }

    private bool SynchronizeEmployeeCodesFromWorkbook(Dictionary<string, string> employeeMappings)
    {
        var suppliedCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var request in _requests.Where(request => !string.IsNullOrWhiteSpace(request.EmployeeCode)))
        {
            var code = request.EmployeeCode!.Trim();
            if (code.Length > 7)
            {
                throw new InvalidDataException(
                    $"Il codice PAGHE nel file Excel per “{request.EmployeeName}” supera il limite di 7 caratteri.");
            }
            if (suppliedCodes.TryGetValue(request.EmployeeName, out var previousCode) &&
                !string.Equals(previousCode, code, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Nel file Excel sono presenti codici PAGHE diversi per il dipendente “{request.EmployeeName}”.");
            }
            suppliedCodes[request.EmployeeName] = code;
        }

        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var supplied in suppliedCodes)
        {
            if (employeeMappings.TryGetValue(supplied.Key, out var existingCode) &&
                !string.Equals(existingCode, supplied.Value, StringComparison.OrdinalIgnoreCase))
            {
                var replace = MessageBox.Show(
                    this,
                    $"Per “{supplied.Key}” il codice nel file Excel ({supplied.Value}) è diverso da quello salvato ({existingCode}). Aggiornare il mapping locale e usare il codice del file?",
                    "Codice PAGHE modificato",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (replace != DialogResult.Yes)
                {
                    SetStatus("Conversione annullata: il codice Excel non coincide con quello salvato.", true);
                    return false;
                }
            }
            updates[supplied.Key] = supplied.Value;
        }

        var allCodes = new Dictionary<string, string>(employeeMappings, StringComparer.OrdinalIgnoreCase);
        foreach (var update in updates)
        {
            allCodes[update.Key] = update.Value;
        }
        var duplicate = allCodes
            .Where(mapping => _requests.Any(request => string.Equals(request.EmployeeName, mapping.Key, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(mapping => mapping.Value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"Il codice PAGHE “{duplicate.Key}” risulta assegnato a più dipendenti del file.");
        }

        foreach (var update in updates)
        {
            _database.SaveEmployeeMapping(update.Key, update.Value);
        }
        return true;
    }

    private void ExportXml()
    {
        if (string.IsNullOrWhiteSpace(_xml))
        {
            return;
        }

        if (!ConfirmNotAlreadyDelivered())
        {
            return;
        }

        var initialDirectory = _lastExportDirectory;
        if (string.IsNullOrEmpty(initialDirectory) || !Directory.Exists(initialDirectory))
        {
            initialDirectory = _sourcePath is not null ? Path.GetDirectoryName(_sourcePath) : null;
        }
        using var picker = new SaveFileDialog
        {
            Title = "Salva il file XML TRRIPW",
            Filter = "File XML (*.xml)|*.xml",
            AddExtension = true,
            DefaultExt = "xml",
            FileName = ExportHistory.SuggestFileName(_companyCode.Value, _sourcePath, _movements),
            OverwritePrompt = true
        };
        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
        {
            picker.InitialDirectory = initialDirectory;
        }
        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var companyCode = _companyCode.Value.Trim();
            if (_movements.Count > 0 && companyCode.Length is > 0 and <= 6)
            {
                // Scrittura in streaming: più veloce e senza duplicare il file in memoria.
                TrriPwXmlWriter.SaveFile(picker.FileName, companyCode, _movements);
            }
            else
            {
                TrriPwXmlWriter.SaveFile(picker.FileName, _xml!);
            }
            // Il file si rilegge e si controlla contro il tracciato prima di darlo per consegnato.
            var problems = TrriPwXmlValidator.ValidateFile(picker.FileName);
            if (problems.Count > 0)
            {
                throw new InvalidDataException(
                    "Il file salvato non rispetta il tracciato TRRIPW e non va consegnato:" + Environment.NewLine +
                    string.Join(Environment.NewLine, problems.Take(6).Select(problem => "• " + problem)));
            }
            var directory = Path.GetDirectoryName(picker.FileName);
            if (!string.IsNullOrEmpty(directory))
            {
                _lastExportDirectory = directory;
                _database.SaveSetting("last-export-dir", directory);
            }
            RecordExport(picker.FileName, companyCode);
            SetStatus($"XML salvato: {picker.FileName}", false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or AggregateException or InvalidDataException)
        {
            ShowError("Salvataggio XML non riuscito", exception);
        }
    }

    /// <summary>
    /// Se gli stessi movimenti sono già stati consegnati, lo dice prima di salvare:
    /// mandare due volte lo stesso file a PAGHE duplica le assenze. L'utente può
    /// comunque procedere (per esempio per rifare un file andato perso).
    /// </summary>
    private bool ConfirmNotAlreadyDelivered()
    {
        if (_movements.Count == 0)
        {
            return true;
        }
        ExportRecord? previous;
        try
        {
            previous = _database.FindExportByFingerprint(
                ExportHistory.Fingerprint(_companyCode.Value, _movements));
        }
        catch (SqliteException)
        {
            return true;
        }
        if (previous is null)
        {
            return true;
        }
        var answer = MessageBox.Show(
            this,
            $"Questi stessi movimenti sono già stati esportati {ExportHistory.DescribeDay(previous.LocalTime, DateTime.Now)} " +
            $"nel file «{previous.FileName}».{Environment.NewLine}{Environment.NewLine}" +
            "Mandare due volte lo stesso file a PAGHE duplica le assenze. Vuoi salvarlo di nuovo?",
            "Consegna già esportata",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        return answer == DialogResult.Yes;
    }

    private void PopulatePreview(IReadOnlyList<DailyMovement> movements)
    {
        _isPopulatingPreview = true;
        try
        {
            // Le righe si preparano tutte e si aggiungono in blocco: una a una, con la griglia
            // che si ricalcola a ogni riga, migliaia di movimenti richiedevano secondi.
            var rows = new List<DataGridViewRow>(movements.Count);
            foreach (var movement in movements)
            {
                var row = new DataGridViewRow();
                row.CreateCells(
                    _movementsGrid,
                    movement.EmployeeName,
                    movement.EmployeeCode,
                    movement.Date.ToString("dd/MM/yyyy"),
                    movement.Policy,
                    movement.ReasonCode,
                    movement.Hours,
                    movement.Minutes,
                    movement.SourceRow == 0 ? string.Empty : movement.SourceRow.ToString());
                rows.Add(row);
            }
            _movementsGrid.SuspendLayout();
            try
            {
                _movementsGrid.Rows.Clear();
                _movementsGrid.Rows.AddRange(rows.ToArray());
            }
            finally
            {
                _movementsGrid.ResumeLayout();
            }
        }
        finally
        {
            _isPopulatingPreview = false;
        }
    }

    private void RefreshXmlFromPreview()
    {
        if (_isPopulatingPreview || _movementsGrid.Rows.Count == 0)
        {
            return;
        }

        try
        {
            var companyCode = _companyCode.Value.Trim();
            if (companyCode.Length is 0 or > 6)
            {
                throw new InvalidDataException("Il codice azienda deve contenere da 1 a 6 caratteri.");
            }

            var movements = new List<DailyMovement>(_movementsGrid.Rows.Count);
            foreach (DataGridViewRow row in _movementsGrid.Rows)
            {
                var employeeName = CellText(row, "employee");
                var employeeCode = CellText(row, "code").Trim();
                var policy = CellText(row, "policy");
                var reasonCode = CellText(row, "reason").Trim();
                var dateText = CellText(row, "date").Trim();
                if (employeeCode.Length is 0 or > 7 || reasonCode.Length is 0 or > 3)
                {
                    throw new InvalidDataException($"Codice dipendente o giustificativo non valido nella riga {row.Index + 1} dell’anteprima.");
                }
                if (!DateTime.TryParseExact(
                        dateText,
                        ["dd/MM/yyyy", "yyyy-MM-dd"],
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out var date))
                {
                    throw new InvalidDataException($"Data non valida nella riga {row.Index + 1} dell’anteprima.");
                }
                if (!int.TryParse(CellText(row, "hours"), out var hours) ||
                    !int.TryParse(CellText(row, "minutes"), out var minutes) ||
                    hours is < 0 or > 99 ||
                    minutes is < 0 or > 59)
                {
                    throw new InvalidDataException($"Ore o minuti non validi nella riga {row.Index + 1} dell’anteprima.");
                }

                movements.Add(new DailyMovement(employeeName, employeeCode, policy, reasonCode, date, hours, minutes));
            }

            _movements = movements;
            _xml = TrriPwXmlWriter.Create(companyCode, movements);
            ShowXmlPreview(_xml);
            PersistMovementMappings();
            RunDeterministicChecks(movements);
            UpdatePreviewSummary(movements);
            UpdateButtons();
            SetStatus(ErrorCount > 0
                ? $"Anteprima modificata: {ErrorCount:N0} errori da correggere nella scheda Controlli."
                : "Anteprima modificata: XML aggiornato. Verifica nuovamente prima di scaricare.", ErrorCount > 0);
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException)
        {
            _xml = null;
            UpdateButtons();
            SetStatus(exception.Message, true);
        }
    }

    private static string CellText(DataGridViewRow row, string columnName) =>
        Convert.ToString(row.Cells[columnName].Value)?.Trim() ?? string.Empty;

    private void SaveCompanyCode()
    {
        var value = _companyCode.Value.Trim();
        if (value.Length is > 0 and <= 6)
        {
            _database.SaveSetting("company-code", value);
            ApplyCompanyRules(announce: true);
            try
            {
                // Ogni codice usato entra nell'anagrafica (senza toccare ragione sociale e
                // note di un'azienda già presente): le aziende si ritrovano da sole.
                _database.SaveCompany(value, string.Empty, string.Empty, keepExistingDetails: true);
                if (!_companies.Any(company => string.Equals(company.Code, value, StringComparison.OrdinalIgnoreCase)))
                {
                    RefreshCompanies();
                }
            }
            catch (Exception exception) when (exception is SqliteException or InvalidDataException)
            {
                // Il codice resta salvato fra le preferenze: l'anagrafica si riallinea al prossimo avvio.
            }
        }
    }

    private void InvalidatePreview()
    {
        if (_xml is null)
        {
            return;
        }
        ClearGeneratedPreview();
        _previewTitleLabel.Text = "Anteprima da rigenerare";
        _previewSubtitleLabel.Text = "Le impostazioni sono cambiate. Crea una nuova anteprima prima di esportare.";
        SetStatus("Anteprima non più valida; genera nuovamente l’XML.", true);
        UpdateButtons();
    }

    private void ClearGeneratedPreview()
    {
        _xml = null;
        _currentPreviewExported = false;
        _xmlPreview.Clear();
        _movementsGrid.Rows.Clear();
        _previewTabs.SetPageText(0, "Movimenti  ·  0");
        _previewCountLabel.Text = _requests.Count == 0
            ? "In attesa di un file"
            : $"{_requests.Count:N0} richieste sorgente  ·  non ancora convertite";
        _previewSearch.Clear();
        _hasWarnings = false;
        UpdateWorkflowSummary();
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var hasSource = _inspection is not null && _requests.Count > 0;
        _generateButton.Enabled = !_isBusy && hasSource;
        _exportButton.Enabled = !_isBusy && !string.IsNullOrWhiteSpace(_xml);
        _copyXmlButton.Enabled = !_isBusy && !string.IsNullOrWhiteSpace(_xml);
        if (_dashGenerateButton is not null)
        {
            _dashGenerateButton.Enabled = _generateButton.Enabled;
        }
        if (_conversionGenerateButton is not null)
        {
            _conversionGenerateButton.Enabled = _generateButton.Enabled;
        }
        UpdateDropZones();
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _isBusy = busy;
        UseWaitCursor = busy;
        UpdateButtons();
        if (status is not null)
        {
            SetStatus(status, false);
        }
    }

    private void SetStatus(string text, bool isWarning)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = isWarning ? Theme.WarningInk : Theme.InkSecondary;
    }

    /// <summary>
    /// Ricrea l'XML dopo una modifica alla griglia solo dopo una piccola pausa:
    /// mentre scrivi non viene ricalcolato nulla e la digitazione resta fluida.
    /// </summary>
    private void ScheduleXmlRefresh()
    {
        _xmlRefreshDebounce.Stop();
        _xmlRefreshDebounce.Start();
    }

    private bool TryValidateCode(string value, int maximumLength, string label, out string normalized)
    {
        normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximumLength)
        {
            MessageBox.Show(
                this,
                $"Inserisci un {label} da 1 a {maximumLength} caratteri, come previsto dal tracciato.",
                "Codice non valido",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _companyCode.FocusInput();
            return false;
        }
        return true;
    }

    private static bool IsExpectedInputError(Exception exception)
    {
        return exception is InvalidDataException or
            IOException or
            UnauthorizedAccessException or
            SqliteException or
            System.Xml.XmlException or
            FormatException or
            OverflowException or
            ArgumentException;
    }

    private readonly System.Windows.Forms.Timer _boundsSaveTimer = new() { Interval = 800 };
    private bool _boundsTrackingReady;

    /// <summary>
    /// Posizione e dimensione si salvano mentre si usa la finestra (poco dopo ogni
    /// spostamento o ridimensionamento), non solo alla chiusura: un aggiornamento
    /// che chiude il programma dall'esterno, un arresto forzato o un blocco non
    /// devono far dimenticare dove stava la finestra.
    /// </summary>
    private void StartTrackingWindowBounds()
    {
        _boundsSaveTimer.Tick += (_, _) =>
        {
            _boundsSaveTimer.Stop();
            SaveWindowBounds();
        };
        _boundsTrackingReady = true;
    }

    private void ScheduleBoundsSave()
    {
        if (!_boundsTrackingReady || IsDisposed || !Visible)
        {
            return;
        }
        _boundsSaveTimer.Stop();
        _boundsSaveTimer.Start();
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        ScheduleBoundsSave();
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        ScheduleBoundsSave();
    }

    /// <summary>
    /// Ripristina posizione e dimensione dell'ultima sessione, ma solo se la
    /// finestra cade ancora dentro un monitor collegato (es. dopo aver
    /// scollegato uno schermo secondario).
    /// </summary>
    private void RestoreWindowBounds()
    {
        var raw = _database.GetSetting("window-bounds");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }
        var parts = raw.Split(';');
        if (parts.Length != 4 ||
            !int.TryParse(parts[0], out var x) ||
            !int.TryParse(parts[1], out var y) ||
            !int.TryParse(parts[2], out var width) ||
            !int.TryParse(parts[3], out var height) ||
            width < 400 || height < 300)
        {
            return;
        }

        var bounds = new Rectangle(x, y, width, height);
        if (!Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(bounds)))
        {
            return;
        }
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        if (_database.GetSetting("window-maximized") == "1")
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    private void SaveWindowBounds()
    {
        try
        {
            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            if (bounds.Width < 200 || bounds.Height < 200)
            {
                return;
            }
            _database.SaveSetting("window-bounds", $"{bounds.X};{bounds.Y};{bounds.Width};{bounds.Height}");
            // Lo stato «ingrandita» cambia solo con la finestra aperta in modo normale o
            // ingrandita: ridotta a icona resta quello di prima.
            if (WindowState != FormWindowState.Minimized)
            {
                _database.SaveSetting("window-maximized", WindowState == FormWindowState.Maximized ? "1" : "0");
            }
        }
        catch (SqliteException)
        {
            // Il ripristino della geometria è un extra: se non si salva, l'app funziona comunque.
        }
    }

    /// <summary>
    /// Dopo una correzione nell'anteprima, ribadisce nel database locale gli
    /// abbinamenti nome→codice usati, così i codici non vanno reinseriti.
    /// </summary>
    private void PersistMovementMappings()
    {
        try
        {
            var employees = _database.GetEmployeeMappings();
            var policies = _database.GetPolicyMappings();
            foreach (var movement in _movements)
            {
                if (!string.IsNullOrWhiteSpace(movement.EmployeeName) &&
                    !string.IsNullOrWhiteSpace(movement.EmployeeCode) &&
                    (!employees.TryGetValue(movement.EmployeeName, out var employeeCode) ||
                     !string.Equals(employeeCode, movement.EmployeeCode, StringComparison.OrdinalIgnoreCase)))
                {
                    _database.SaveEmployeeMapping(movement.EmployeeName, movement.EmployeeCode);
                    employees[movement.EmployeeName] = movement.EmployeeCode;
                }
                if (!string.IsNullOrWhiteSpace(movement.Policy) &&
                    !string.IsNullOrWhiteSpace(movement.ReasonCode) &&
                    (!policies.TryGetValue(movement.Policy, out var reasonCode) ||
                     !string.Equals(reasonCode, movement.ReasonCode, StringComparison.OrdinalIgnoreCase)))
                {
                    _database.SavePolicyMapping(movement.Policy, movement.ReasonCode);
                    policies[movement.Policy] = movement.ReasonCode;
                }
            }
        }
        catch (SqliteException)
        {
            // La persistenza dei codici non deve interrompere la generazione dell'XML.
        }
    }

    private void ShowError(string title, Exception exception)
    {
        SetStatus(exception.Message, true);
        MessageBox.Show(this, exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
