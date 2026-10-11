using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter;

internal sealed partial class MainForm : Form
{
    private readonly LocalDatabase _database = new();
    private readonly LocalAiAssistant _ai = new();
    private readonly TextBox _companyCode = new();
    private readonly Label _fileLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _aiStatusLabel = new();
    private readonly Button _generateButton = new();
    private readonly Button _exportButton = new();
    private readonly DataGridView _movementsGrid = new();
    private readonly RichTextBox _xmlPreview = new();
    private readonly TabControl _previewTabs = new();
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
    private bool _designScaled;
    private Size _designSize;
    private Size _designMinimumSize;
    private readonly System.Windows.Forms.Timer _xmlRefreshDebounce = new() { Interval = 350 };

    public MainForm()
    {
        Text = "PresenzeFlow";
        StartPosition = FormStartPosition.CenterScreen;
        // Le dimensioni sono espresse a 96 DPI: con lo scaling DPI tutte le posizioni
        // e i caratteri crescono insieme, così niente testo più grande della sua scatola.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(244, 247, 251);
        _applicationIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        Icon = _applicationIcon;

        BuildLayout();
        // Dimensioni di progetto a 96 DPI: servono per riscalare la finestra
        // una sola volta, senza moltiplicare due volte ciò che l'AutoScale ha già toccato.
        _designSize = Size;
        _designMinimumSize = MinimumSize;
        _companyCode.Text = _database.GetSetting("company-code") ?? string.Empty;
        _lastExportDirectory = _database.GetSetting("last-export-dir");
        _xmlRefreshDebounce.Tick += (_, _) =>
        {
            _xmlRefreshDebounce.Stop();
            RefreshXmlFromPreview();
        };
        SetupTrayIcon();
        UpdateButtons();
        UpdateAiStatusAsync();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Il font cresce con il DPI (i punti sono relativi), ma le posizioni e le
        // righe assolute dei layout no: senza riscalare la geometria il contenuto
        // esce dalle scatole. Qui scaliamo TUTTA la geometria di un fattore solo,
        // poi riportiamo la finestra dentro lo schermo.
        var factor = DeviceDpi / 96f;
        if (factor > 1.01f && !_designScaled)
        {
            _designScaled = true;
            Size = new Size((int)(_designSize.Width * factor), (int)(_designSize.Height * factor));
            MinimumSize = new Size(
                (int)(_designMinimumSize.Width * factor),
                (int)(_designMinimumSize.Height * factor));
            ScaleLayoutStyles(this, factor);
        }
        FitToScreen();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        FitToScreen();
    }

    internal static void ScaleLayoutStyles(Control root, float factor)
    {
        if (root is TableLayoutPanel table)
        {
            for (var index = 0; index < table.RowStyles.Count; index++)
            {
                var row = table.RowStyles[index];
                if (row.SizeType == SizeType.Absolute)
                {
                    table.RowStyles[index] = new RowStyle(SizeType.Absolute, row.Height * factor);
                }
            }
            for (var index = 0; index < table.ColumnStyles.Count; index++)
            {
                var column = table.ColumnStyles[index];
                if (column.SizeType == SizeType.Absolute)
                {
                    table.ColumnStyles[index] = new ColumnStyle(SizeType.Absolute, column.Width * factor);
                }
            }
        }
        else if (root is DataGridView grid)
        {
            grid.ColumnHeadersHeight = (int)(grid.ColumnHeadersHeight * factor);
            grid.RowHeadersWidth = (int)(grid.RowHeadersWidth * factor);
            grid.RowTemplate.Height = (int)(grid.RowTemplate.Height * factor);
        }
        else if (root is TabControl tabs)
        {
            tabs.ItemSize = new Size((int)(tabs.ItemSize.Width * factor), (int)(tabs.ItemSize.Height * factor));
            tabs.Padding = new Point((int)(tabs.Padding.X * factor), (int)(tabs.Padding.Y * factor));
        }

        foreach (Control child in root.Controls)
        {
            // I controlli ancorati/messi in dock vengono ridisegnati dal genitore:
            // scalare qui servono solo quelli con posizione libera (titolo, badge,
            // passi numerati, etichette delle metriche...).
            if (child.Dock == DockStyle.None)
            {
                child.Location = new Point((int)(child.Location.X * factor), (int)(child.Location.Y * factor));
                child.Size = new Size((int)(child.Size.Width * factor), (int)(child.Size.Height * factor));
            }
            if (child.MinimumSize != Size.Empty)
            {
                child.MinimumSize = new Size(
                    (int)(child.MinimumSize.Width * factor),
                    (int)(child.MinimumSize.Height * factor));
            }
            ScaleLayoutStyles(child, factor);
        }
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

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.OnFormClosing(e);
    }

    private void SetupTrayIcon()
    {
        _trayIcon.Icon = _applicationIcon;
        _trayIcon.Text = "PresenzeFlow";
        var menu = new ContextMenuStrip();
        menu.Items.Add("Apri PresenzeFlow", null, (_, _) => RestoreFromTray());
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
        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            SetBusy(true, "Lettura del file Excel in corso…");
            WorkbookInspection inspection;
            try
            {
                inspection = await Task.Run(() => AttendanceWorkbookReader.Inspect(picker.FileName));
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

            _requests = inspection.ReadRequests(columns);
            _inspection = inspection;
            _sourcePath = picker.FileName;
            _fileLabel.Text = Path.GetFileName(picker.FileName);
            ClearGeneratedPreview();
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
        if (!TryValidateCode(_companyCode.Text, 6, "codice azienda", out var companyCode))
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
                        _inspection.PeriodEnd);
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
            _previewTabs.SelectedIndex = 0;
            _hasWarnings = result.Warnings.Count > 0;
            UpdatePreviewSummary(result.Movements);
            UpdateButtons();
            NotifyConversionCompleted(result.Movements.Count);
            if (result.Warnings.Count > 0)
            {
                var warningText = string.Join(Environment.NewLine, result.Warnings.Take(12));
                if (result.Warnings.Count > 12)
                {
                    warningText += $"{Environment.NewLine}… e altre {result.Warnings.Count - 12} segnalazioni.";
                }
                SetStatus($"{result.Warnings.Count} segnalazioni: controlla l’elenco prima dell’esportazione.", true);
                MessageBox.Show(
                    this,
                    warningText,
                    "Controlli sulla conversione",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            else
            {
                SetStatus("Anteprima pronta. Controlla i movimenti e il contenuto XML prima di scaricare.", false);
            }
            UpdateWorkflowSummary();
        }
        catch (Exception exception) when (IsExpectedInputError(exception))
        {
            ShowError("Generazione XML non riuscita", exception);
        }
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
            var companyCode = _companyCode.Text.Trim();
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
            if (!await _ai.IsAvailableAsync())
            {
                var answer = MessageBox.Show(
                    this,
                    "Ollama non è installato o non è in esecuzione. Vuoi scaricare e avviare l’installer ufficiale? " +
                    "Dopo l’installazione riapri questa configurazione per scaricare il modello. " +
                    "Il servizio AI verrà usato solo in locale.",
                    "Configurazione AI locale",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                {
                    SetAiStatus("Download installer Ollama in corso (oltre 1 GB)…", false);
                    await DownloadAndRunOllamaInstallerAsync();
                }
                return;
            }

            if (await _ai.HasModelAsync())
            {
                SetAiStatus($"AI locale pronta ({_ai.ActiveModel}).", true);
                MessageBox.Show(
                    this,
                    $"Il modello locale {_ai.ActiveModel} è pronto. L’AI può proporre colonne e abbinamenti basati su mapping già configurati; i codici ufficiali restano sotto il tuo controllo.",
                    "AI locale configurata",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var download = MessageBox.Show(
                this,
                $"Scaricare il modello locale {LocalAiAssistant.DefaultModel}? È adatto a colonne e giustificativi e richiede poche risorse. Il download può richiedere alcuni minuti; le richieste Excel non vengono inviate a alcun server.",
                "Scarica modello AI locale",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (download != DialogResult.Yes)
            {
                return;
            }

            SetAiStatus("Download del modello in corso…", false);
            await _ai.DownloadModelAsync();
            SetAiStatus($"AI locale pronta ({_ai.ActiveModel}).", true);
            MessageBox.Show(this, "Modello locale installato correttamente.", "Configurazione completata", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    private static async Task DownloadAndRunOllamaInstallerAsync()
    {
        const string installerUrl = "https://ollama.com/download/OllamaSetup.exe";
        var setupDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeGioia",
            "PresenzeXmlConverter",
            "setup");
        Directory.CreateDirectory(setupDirectory);
        var installerPath = Path.Combine(setupDirectory, "OllamaSetup.exe");

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(60) };
        using var response = await client.GetAsync(installerUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using (var file = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await response.Content.CopyToAsync(file);
        }

        using var process = Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });
        if (process is null)
        {
            throw new InvalidOperationException("Windows non ha avviato l’installer ufficiale di Ollama.");
        }
        await process.WaitForExitAsync();
        File.Delete(installerPath);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"L’installer Ollama è terminato con codice {process.ExitCode}.");
        }
        MessageBox.Show(
            "Installazione di Ollama completata. Riapri “Configura AI locale” per scaricare il modello.",
            "Installazione AI locale",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
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
            var companyCode = _companyCode.Text.Trim();
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
            _hasWarnings = false;
            UpdatePreviewSummary(movements);
            UpdateButtons();
            SetStatus("Anteprima modificata: XML aggiornato. Verifica nuovamente prima di scaricare.", false);
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
        var value = _companyCode.Text.Trim();
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
        if (_previewTabs.TabPages.Count > 0)
        {
            _previewTabs.TabPages[0].Text = "Movimenti  ·  0";
        }
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
        _statusLabel.ForeColor = isWarning ? Color.FromArgb(170, 95, 20) : Color.FromArgb(88, 100, 116);
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
        _aiStatusLabel.ForeColor = isReady ? Color.FromArgb(29, 120, 76) : Color.FromArgb(115, 105, 90);
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
            _companyCode.Focus();
            return false;
        }
        return true;
    }

    private static Button CreateButton(string text, Color color, bool primary = false)
    {
        var button = new Button { Text = text };
        StyleButton(button, color, primary);
        return button;
    }

    private static void StyleButton(Button button, Color color, bool primary)
    {
        button.AutoSize = true;
        button.MinimumSize = new Size(110, 34);
        button.Padding = new Padding(12, 0, 12, 0);
        button.Margin = new Padding(0);
        button.BackColor = color;
        button.ForeColor = primary ? Color.White : Color.FromArgb(55, 65, 81);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = primary
            ? ControlPaint.Light(color, 0.15f)
            : Color.FromArgb(226, 232, 240);
        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(color, 0.1f);
        button.Font = new Font("Segoe UI", 9.7f, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
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

    private void ShowError(string title, Exception exception)
    {
        SetStatus(exception.Message, true);
        MessageBox.Show(this, exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
