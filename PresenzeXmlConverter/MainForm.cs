using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace PresenzeXmlConverter;

internal sealed partial class MainForm : Form
{
    private readonly LocalDatabase _database = new();
    private readonly LocalAiAssistant _ai = new();
    private readonly RoundedField _companyCode = new();
    private readonly Label _fileLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _aiStatusLabel = new();
    private readonly Button _generateButton = new();
    private readonly Button _sheetButton = new();
    private readonly Button _exportButton = new();
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
    private GenerationOptions _generationOptions = GenerationOptions.Default;

    /// <summary>Impostazione che ricorda il modello AI scelto dall'utente.</summary>
    private const string AiModelSetting = "ai-preferred-model";

    /// <summary>
    /// Client dedicato ai download dei componenti (motore AI e modelli): segue i
    /// reindirizzamenti e ha un timeout molto più lungo di una normale richiesta.
    /// </summary>
    private static readonly HttpClient DownloadClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(20)
    })
    {
        Timeout = TimeSpan.FromMinutes(60)
    };

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

        BuildLayout();
        EnableWorkbookDrop();
        _companyCode.Value = _database.GetSetting("company-code") ?? string.Empty;
        _lastExportDirectory = _database.GetSetting("last-export-dir");
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
        _lastSourceDirectory = _database.GetSetting("last-source-dir");
        _generationOptions = GenerationSettings.Read(_database);
        _ai.PreferredModel = ResolvePreferredModel();
        InitializeUpdates();
        SetupTrayIcon();
        UpdateButtons();
        UpdateAiStatusAsync();
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

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FitToScreen();
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
                if (dialog.ShowDialog(this) != DialogResult.OK)
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

        SaveWindowBounds();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.OnFormClosing(e);
    }

    private void SetupTrayIcon()
    {
        _trayIcon.Icon = _applicationIcon;
        _trayIcon.Text = WindowCaption;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Apri PresenzeFlow", null, (_, _) => RestoreFromTray());
        AddUpdateCommands(menu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Esci definitivamente", null, (_, _) =>
        {
            _forceClose = true;
            Close();
        });
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void MinimizeToTray()
    {
        Hide();
        _trayIcon.Visible = true;
        _trayIcon.ShowBalloonTip(
            4000,
            "PresenzeFlow in background",
            "Il programma resta attivo in tray: riceverai una notifica quando una conversione termina.",
            ToolTipIcon.Info);
    }

    private void RestoreFromTray()
    {
        _trayIcon.Visible = false;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void NotifyConversionCompleted(int movementCount)
    {
        if (Visible)
        {
            return;
        }
        _trayIcon.Visible = true;
        _trayIcon.ShowBalloonTip(
            5000,
            "Conversione completata",
            $"{movementCount:N0} movimenti pronti. Apri PresenzeFlow per revisionare ed esportare l'XML.",
            ToolTipIcon.Info);
    }

    private async Task ImportWorkbookAsync()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Seleziona il file presenze da convertire",
            Filter = "Fogli Excel (*.xlsx)|*.xlsx",
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
                ColumnSelection? aiSuggestion = null;
                if (await _ai.IsAvailableAsync())
                {
                    try
                    {
                        if (await _ai.HasModelAsync())
                        {
                            aiSuggestion = await _ai.SuggestColumnsAsync(inspection.Headers);
                        }
                    }
                    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
                    {
                        SetStatus($"AI non disponibile per il riconoscimento delle colonne: {exception.Message}. Puoi associarle manualmente.", true);
                    }
                }

                using var mappingDialog = new ColumnMappingDialog(inspection.Headers, aiSuggestion);
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
            LoadSheetNames(path);
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
                IReadOnlyDictionary<string, string> policySuggestions = new Dictionary<string, string>();
                if (missingPolicies.Length > 0 && await _ai.IsAvailableAsync())
                {
                    try
                    {
                        if (await _ai.HasModelAsync())
                        {
                            policySuggestions = await _ai.SuggestPolicyCodesAsync(missingPolicies, policies);
                        }
                    }
                    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
                    {
                        SetStatus($"AI non disponibile per i suggerimenti: {exception.Message}. Completa i codici manualmente.", true);
                    }
                }

                using var dialog = new MappingEditorDialog(
                    employees,
                    policies,
                    missingEmployees,
                    missingPolicies,
                    policySuggestions);
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
            PopulatePreview(result.Movements);
            _xmlPreview.Text = _xml;
            ApplyXmlSyntaxHighlighting();
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
            FileName = $"{Path.GetFileNameWithoutExtension(_sourcePath ?? "presenze")}_TRRIPW.xml",
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
            var directory = Path.GetDirectoryName(picker.FileName);
            if (!string.IsNullOrEmpty(directory))
            {
                _lastExportDirectory = directory;
                _database.SaveSetting("last-export-dir", directory);
            }
            SetStatus($"XML salvato: {picker.FileName}", false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or AggregateException or InvalidDataException)
        {
            ShowError("Salvataggio XML non riuscito", exception);
        }
    }

    private async Task ConfigureAiAsync()
    {
        try
        {
            if (await _ai.IsAvailableAsync())
            {
                await EnsureModelAsync();
                return;
            }

            // Ollama non risponde. Se è GIÀ installato non lo reinstalliamo:
            // proviamo ad avviarlo e, al massimo, scarichiamo solo il modello.
            var executable = FindOllamaExecutable();
            if (executable is not null)
            {
                SetAiStatus("Ollama è installato: avvio del servizio locale…", false);
                await StartOllamaAsync(executable);
                if (await _ai.IsAvailableAsync())
                {
                    await EnsureModelAsync();
                    return;
                }
                MessageBox.Show(
                    this,
                    "Ollama risulta installato ma non risponde. Avvialo (menu Start → Ollama) e riprova: " +
                    "il programma scaricherà solo il modello, senza reinstallare nulla.",
                    "Configurazione AI locale",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var answer = MessageBox.Show(
                this,
                "Ollama non è installato. Vuoi scaricare e installare il motore AI ufficiale? " +
                "Al termine il modello verrà scaricato automaticamente. Il servizio AI resta solo in locale.",
                "Configurazione AI locale",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            if (!await InstallOllamaAsync())
            {
                SetAiStatus("Installazione di Ollama non riuscita.", true);
                return;
            }

            await StartOllamaAsync(FindOllamaExecutable());
            if (await _ai.IsAvailableAsync())
            {
                await EnsureModelAsync();
            }
            else
            {
                SetAiStatus("Ollama installato; avvialo e riprova per scaricare il modello.", false);
            }
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException or TaskCanceledException or
                                          System.Text.Json.JsonException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            SetAiStatus("AI locale non disponibile.", false);
            ShowError("Configurazione AI non riuscita", exception);
        }
        finally
        {
            UpdateAiStatusAsync();
        }
    }

    /// <summary>
    /// Verifica che il modello sia presente e, se manca, lo scarica con una
    /// barra di avanzamento reale. Non tocca Ollama: quando è già installato si
    /// scarica soltanto il modello.
    /// </summary>
    private async Task EnsureModelAsync()
    {
        if (await _ai.HasModelAsync())
        {
            SetAiStatus($"AI locale pronta ({_ai.ActiveModel}).", true);
            MessageBox.Show(
                this,
                $"Il modello locale {_ai.ActiveModel} è pronto. L’AI può proporre colonne, abbinamenti e correzioni all’anteprima; i codici ufficiali restano sotto il tuo controllo.",
                "AI locale configurata",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var model = ResolvePreferredModel();
        var download = MessageBox.Show(
            this,
            $"Scaricare il modello locale {model}? {DescribeModelSize(model)} " +
            "L’avanzamento viene mostrato in una finestra dedicata e i dati restano sul tuo PC.",
            "Scarica modello AI locale",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (download != DialogResult.Yes)
        {
            return;
        }

        if (!HasEnoughDiskSpace())
        {
            ShowInformation(
                "Spazio su disco insufficiente",
                "Per il modello AI locale servono almeno 2,5 GB liberi sul disco di Windows. " +
                "Libera spazio e riprova dallo stesso pulsante.");
            return;
        }

        using var progress = new ProgressDialog(
            "AI locale",
            $"Download del modello {model}",
            "Connessione a Ollama…");
        progress.Show(this);
        try
        {
            await _ai.PullModelAsync(
                model,
                new Progress<(string Status, int Percent)>(update =>
                {
                    // La percentuale arriva dai byte reali: quando non è ancora
                    // nota si mostra lo stato raccontato da Ollama.
                    if (update.Percent >= 0)
                    {
                        progress.SetProgress(update.Percent, $"Download del modello {model}… {update.Percent}%");
                    }
                    else
                    {
                        progress.SetStatus($"Download del modello {model}… {update.Status}");
                    }
                }),
                progress.Token);
            progress.SetCompleted("Modello installato.");
            _database.SaveSetting(AiModelSetting, model);
            _ai.PreferredModel = model;
            SetAiStatus($"AI locale pronta ({model}).", true);
            MessageBox.Show(
                this,
                "Modello locale installato correttamente.",
                "Configurazione completata",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            SetStatus("●   Download del modello annullato: nessun file incompleto è stato mantenuto.", false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException or
                                          System.Text.Json.JsonException or InvalidOperationException)
        {
            ShowError("Download del modello non riuscito", exception);
        }
        finally
        {
            progress.Close();
        }
    }

    /// <summary>
    /// Installa Ollama preferendo winget (scarica solo ciò che serve e gestisce
    /// da sé versioni e aggiornamenti) e, se non è disponibile o fallisce,
    /// usando l'installer ufficiale con barra di avanzamento e tentativi ripetuti.
    /// </summary>
    private async Task<bool> InstallOllamaAsync()
    {
        if (await TryInstallOllamaWithWingetAsync())
        {
            return true;
        }

        var installer = await DownloadOllamaInstallerAsync();
        if (installer is null)
        {
            return false;
        }
        return await RunOllamaInstallerAsync(installer);
    }

    /// <summary>Percorso rapido: winget, se è disponibile sul PC.</summary>
    private async Task<bool> TryInstallOllamaWithWingetAsync()
    {
        var winget = FindWingetExecutable();
        if (winget is null)
        {
            return false;
        }

        SetAiStatus("Installazione di Ollama con winget…", false);
        try
        {
            var start = new ProcessStartInfo(winget)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in OllamaSetup.WingetInstallArguments())
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start);
            if (process is null)
            {
                return false;
            }
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
            await process.WaitForExitAsync(timeout.Token);

            // Non basta il codice di uscita: winget segnala in modi diversi
            // "installato" e "già presente". Conta che Ollama ci sia davvero.
            if (FindOllamaExecutable() is not null)
            {
                return true;
            }
            SetAiStatus($"winget ha terminato con codice {process.ExitCode}: uso l’installer ufficiale.", false);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or
                                          OperationCanceledException or IOException)
        {
            SetAiStatus("winget non disponibile: uso l’installer ufficiale.", false);
        }
        return false;
    }

    /// <summary>
    /// Scarica l'installer ufficiale mostrando l'avanzamento; in caso di rete
    /// instabile riprova fino a tre volte prima di arrendersi.
    /// </summary>
    private async Task<string?> DownloadOllamaInstallerAsync()
    {
        var setupDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeGioia",
            "PresenzeXmlConverter",
            "setup");
        Directory.CreateDirectory(setupDirectory);
        var destination = Path.Combine(setupDirectory, "OllamaSetup.exe");

        using var progress = new ProgressDialog(
            "Installazione dell’AI locale",
            "Scaricamento del motore AI (Ollama)",
            "Connessione al sito ufficiale…");
        progress.Show(this);
        try
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await DownloadFileAsync(
                        OllamaSetup.DownloadUrl,
                        destination,
                        new Progress<int>(percent =>
                            progress.SetProgress(percent, $"Scaricamento di Ollama… {percent}%")),
                        progress.Token);
                    progress.SetCompleted("Download completato.");
                    return destination;
                }
                catch (Exception exception) when (attempt < 3 &&
                                                  exception is HttpRequestException or IOException or TaskCanceledException)
                {
                    progress.SetStatus($"Download interrotto: nuovo tentativo ({attempt + 1}/3)…");
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                }
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("●   Installazione dell’AI locale annullata: nulla è stato scaricato.", false);
            return null;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException or
                                          System.ComponentModel.Win32Exception)
        {
            ShowError("Download di Ollama non riuscito", exception);
            return null;
        }
        finally
        {
            progress.Close();
        }

        ShowInformation(
            "Download di Ollama non riuscito",
            "Non è stato possibile scaricare Ollama. Controlla la connessione e riprova: " +
            "la conversione Excel → XML funziona comunque senza AI.");
        return null;
    }

    /// <summary>
    /// Esegue l'installer scaricato in modalità silenziosa e, se questa non
    /// funziona, lo ripropone mostrandolo all'utente.
    /// </summary>
    private async Task<bool> RunOllamaInstallerAsync(string installerPath)
    {
        try
        {
            SetAiStatus("Installazione di Ollama in corso…", false);
            using (var silent = Process.Start(new ProcessStartInfo(installerPath)
                   {
                       UseShellExecute = true,
                       Arguments = OllamaSetup.InstallerArguments(silent: true)
                   }))
            {
                if (silent is null)
                {
                    return false;
                }
                await silent.WaitForExitAsync();
                if (FindOllamaExecutable() is not null)
                {
                    return true;
                }
                SetAiStatus($"Installazione silenziosa non riuscita (codice {silent.ExitCode}): apro l’installer.", false);
            }

            using var interactive = Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });
            if (interactive is null)
            {
                return false;
            }
            await interactive.WaitForExitAsync();
            return FindOllamaExecutable() is not null;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            SetStatus($"Avvio dell’installer di Ollama non riuscito: {exception.Message}", true);
            return false;
        }
    }

    /// <summary>
    /// Ollama è installato? Prima le posizioni standard, poi quelle registrate
    /// (installazioni con cartella personalizzata) e infine il PATH.
    /// </summary>
    private static string? FindOllamaExecutable()
    {
        var candidates = new List<string>(OllamaSetup.CandidateExecutablePaths(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)));
        candidates.AddRange(RegistryOllamaCandidatePaths());

        var found = OllamaSetup.FindExecutable(candidates, File.Exists);
        return found ?? OllamaSetup.FindInPathList(
            Environment.GetEnvironmentVariable("PATH"),
            "ollama.exe",
            File.Exists);
    }

    /// <summary>
    /// Le installazioni fatte con "OllamaSetup.exe /DIR=..." non sono nelle
    /// posizioni standard: il registro delle installazioni le conosce.
    /// </summary>
    private static IReadOnlyList<string> RegistryOllamaCandidatePaths()
    {
        var results = new List<string>();
        if (!OperatingSystem.IsWindows())
        {
            return results;
        }

        var locations = new (RegistryKey Root, string Path)[]
        {
            (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall")
        };
        foreach (var (root, path) in locations)
        {
            try
            {
                using var uninstall = root.OpenSubKey(path);
                if (uninstall is null)
                {
                    continue;
                }
                foreach (var name in uninstall.GetSubKeyNames())
                {
                    using var entry = root.OpenSubKey($@"{path}\{name}");
                    if (entry?.GetValue("DisplayName") is not string displayName ||
                        !displayName.Contains("ollama", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (entry.GetValue("InstallLocation") is string location && location.Length > 0)
                    {
                        results.Add(Path.Combine(location, "ollama.exe"));
                    }
                }
            }
            catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // Chiave non leggibile: si prosegue con le altre.
            }
        }
        return results;
    }

    private static string? FindWingetExecutable()
    {
        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps",
            "winget.exe");
        if (File.Exists(local))
        {
            return local;
        }
        return OllamaSetup.FindInPathList(Environment.GetEnvironmentVariable("PATH"), "winget.exe", File.Exists);
    }

    /// <summary>
    /// Avvia il servizio locale di Ollama (senza installare nulla) e attende che
    /// risponda: su PC più lenti l'avvio può richiedere più di dieci secondi.
    /// </summary>
    private async Task StartOllamaAsync(string? ollamaExecutable)
    {
        try
        {
            var directory = ollamaExecutable is null ? null : Path.GetDirectoryName(ollamaExecutable);
            var application = directory is null
                ? null
                : Path.Combine(directory, OllamaSetup.ServerExecutableNames[0]);
            if (application is not null && File.Exists(application))
            {
                Process.Start(new ProcessStartInfo(application) { UseShellExecute = true });
            }
            else if (ollamaExecutable is not null)
            {
                Process.Start(new ProcessStartInfo(ollamaExecutable)
                {
                    Arguments = "serve",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            else
            {
                return;
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return;
        }

        for (var attempt = 0; attempt < 15; attempt++)
        {
            await Task.Delay(1000);
            if (await _ai.IsAvailableAsync())
            {
                return;
            }
        }
    }

    /// <summary>
    /// Modello da usare: la scelta fatta in fase di installazione (file scritto
    /// dall'installer), poi quella salvata dall'utente, infine il predefinito.
    /// </summary>
    private string ResolvePreferredModel()
    {
        var fromInstaller = ReadInstallerModelMarker();
        var stored = _database.GetSetting(AiModelSetting);
        return OllamaSetup.ResolvePreferredModel(fromInstaller, stored) ?? LocalAiAssistant.DefaultModel;
    }

    /// <summary>Legge il modello scelto durante l'installazione dell'app.</summary>
    private static string? ReadInstallerModelMarker()
    {
        try
        {
            var marker = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeGioia",
                "PresenzeXmlConverter",
                OllamaSetup.ModelMarkerFileName);
            return File.Exists(marker) ? File.ReadAllText(marker) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Dimensione approssimativa dei modelli proposti dall'installer.</summary>
    private static string DescribeModelSize(string model) => model switch
    {
        "qwen2.5:0.5b" => "Circa 400 MB.",
        "qwen2.5:1.5b" => "Circa 1 GB.",
        "qwen2.5:3b" => "Circa 2 GB.",
        _ => string.Empty
    };

    /// <summary>
    /// Evita di avviare un download da oltre un gigabyte su un disco quasi pieno.
    /// In caso di dubbio (unità non leggibile) non blocca nulla.
    /// </summary>
    private static bool HasEnoughDiskSpace()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            if (string.IsNullOrEmpty(root))
            {
                return true;
            }
            var drive = new DriveInfo(root);
            return !drive.IsReady || drive.AvailableFreeSpace >= OllamaSetup.MinimumFreeBytes;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>
    /// Download di un file con avanzamento reale (stream + Content-Length).
    /// Il file di destinazione viene ricreato a ogni tentativo, così un download
    /// interrotto non lascia un installer troncato.
    /// </summary>
    private static async Task DownloadFileAsync(
        string url,
        string destination,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await DownloadClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var file = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);

        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            if (total > 0)
            {
                progress?.Report((int)Math.Min(100, received * 100 / total));
            }
        }
        await file.FlushAsync(cancellationToken);
    }

    private async void UpdateAiStatusAsync()
    {
        if (!await _ai.IsAvailableAsync())
        {
            SetAiStatus("AI locale non configurata (opzionale).", false);
            return;
        }

        try
        {
            SetAiStatus(
                await _ai.HasModelAsync()
                    ? $"AI locale pronta ({_ai.ActiveModel})."
                    : "Motore disponibile; modello locale da scaricare.",
                true);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            SetAiStatus("AI locale non disponibile.", false);
            SetStatus($"Verifica AI: {exception.Message}", true);
        }
    }

    private void PopulatePreview(IReadOnlyList<DailyMovement> movements)
    {
        _isPopulatingPreview = true;
        try
        {
            _movementsGrid.Rows.Clear();
            foreach (var movement in movements)
            {
                _movementsGrid.Rows.Add(
                    movement.EmployeeName,
                    movement.EmployeeCode,
                    movement.Date.ToString("dd/MM/yyyy"),
                    movement.Policy,
                    movement.ReasonCode,
                    movement.Hours,
                    movement.Minutes,
                    movement.SourceRow == 0 ? string.Empty : movement.SourceRow.ToString());
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
            _xmlPreview.Text = _xml;
            ApplyXmlSyntaxHighlighting();
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

    private void SetAiStatus(string text, bool isReady)
    {
        _aiStatusLabel.Text = text;
        _aiStatusLabel.ForeColor = isReady ? Theme.SuccessInk : Theme.MutedInk;
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
