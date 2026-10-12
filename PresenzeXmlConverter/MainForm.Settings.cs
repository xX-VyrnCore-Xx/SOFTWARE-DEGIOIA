using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter;

/// <summary>
/// Impostazioni, scelta del foglio e scorciatoie da tastiera: le tre cose che
/// permettono di correggere la conversione senza rifare tutto da capo.
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>Apre le impostazioni e applica quello che l'utente ha salvato.</summary>
    private void OpenSettings()
    {
        try
        {
            using var dialog = new SettingsDialog(
                _generationOptions,
                _inspection?.PeriodStart?.Year ?? DateTime.Today.Year,
                AutoUpdateEnabled(),
                _database.GetSetting(AiModelSetting),
                $"Modello in uso: {_ai.ActiveModel}. {_aiStatusLabel.Text}");
            var result = dialog.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                ApplySettings(dialog);
                return;
            }
            // Anche con "Annulla" un token verificato e salvato resta attivo:
            // l'utente lo ha già confermato con "Verifica e salva token".
            if (dialog.TokenSaved)
            {
                SetStatus("●   Token aggiornato: controllo gli aggiornamenti adesso.", false);
                _ = CheckForUpdatesAsync(userInitiated: true);
            }
        }
        catch (SqliteException exception)
        {
            ShowError("Impossibile leggere le impostazioni", exception);
        }
    }

    private void ApplySettings(SettingsDialog dialog)
    {
        try
        {
            var previous = _generationOptions;
            var referenceYear = _inspection?.PeriodStart?.Year ?? DateTime.Today.Year;
            _generationOptions = dialog.Options with { ExtraHolidays = dialog.Options.ExtraHolidays ?? [] };
            GenerationSettings.Save(_database, _generationOptions, referenceYear);
            _database.SaveSetting(AutoUpdateSetting, dialog.AutoUpdateCheck ? "true" : "false");

            var storedModel = _database.GetSetting(AiModelSetting);
            if (!string.Equals(dialog.PreferredModel, storedModel, StringComparison.Ordinal))
            {
                _database.SaveSetting(AiModelSetting, dialog.PreferredModel ?? string.Empty);
                _ai.PreferredModel = dialog.PreferredModel ?? LocalAiAssistant.DefaultModel;
                UpdateAiStatusAsync();
            }

            if (!GenerationSettings.AreEquivalent(previous, _generationOptions))
            {
                InvalidatePreview();
                SetStatus(
                    _requests.Count > 0
                        ? "Impostazioni di conversione salvate: rigenera l’anteprima per applicarle."
                        : "Impostazioni di conversione salvate.",
                    _requests.Count > 0);
            }
            else
            {
                SetStatus("●   Impostazioni salvate", false);
            }

            if (dialog.TokenSaved)
            {
                SetStatus("●   Token GitHub attivo: controllo subito gli aggiornamenti.", false);
                _ = CheckForUpdatesAsync(userInitiated: true);
            }
        }
        catch (SqliteException exception)
        {
            ShowError("Impossibile salvare le impostazioni", exception);
        }
    }

    /// <summary>Elenco delle scorciatoie: la finestra non ha un menu, quindi va raccontato.</summary>
    private void ShowShortcuts()
    {
        const string text = """
            Ctrl+O   Apri un file Excel
            Ctrl+G   Genera anteprima e XML
            Ctrl+S   Salva l'XML su file
            Ctrl+F   Cerca nei movimenti
            Ctrl+1   Scheda Movimenti
            Ctrl+2   Scheda Anteprima XML
            Ctrl+3   Scheda Controlli
            Ctrl+,   Impostazioni
            F1       Questo elenco
            """;
        MessageBox.Show(this, text, "Scorciatoie da tastiera", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.O:
                _ = ImportWorkbookAsync();
                return true;
            case Keys.Control | Keys.G when _generateButton.Enabled:
                GeneratePreview();
                return true;
            case Keys.Control | Keys.S when _exportButton.Enabled:
                ExportXml();
                return true;
            case Keys.Control | Keys.F:
                _previewTabs.SelectedIndex = 0;
                _previewSearch.FocusInput();
                return true;
            case Keys.Control | Keys.D1:
                _previewTabs.SelectedIndex = 0;
                return true;
            case Keys.Control | Keys.D2:
                _previewTabs.SelectedIndex = 1;
                return true;
            case Keys.Control | Keys.D3:
                _previewTabs.SelectedIndex = ReviewTabIndex;
                return true;
            case Keys.Control | Keys.Oemcomma:
                OpenSettings();
                return true;
            case Keys.F1:
                ShowShortcuts();
                return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    /// <summary>Legge i nomi dei fogli del file appena importato (senza mai bloccare l'importazione).</summary>
    private void LoadSheetNames(string path)
    {
        try
        {
            _sheetNames = AttendanceWorkbookReader.ListSheetNames(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or
            ArgumentException)
        {
            _sheetNames = Array.Empty<string>();
        }
        UpdateSheetButton();
    }

    private void UpdateSheetButton()
    {
        var hasFile = _inspection is not null && _sourcePath is not null;
        _sheetButton.Enabled = !_isBusy && hasFile && _sheetNames.Count > 1;
        _sheetButton.Text = !hasFile
            ? "Foglio: nessun file importato"
            : _sheetNames.Count > 1
                ? $"Foglio: {_inspection!.SheetName}   ▾   ({_sheetNames.Count} fogli)"
                : $"Foglio: {_inspection!.SheetName}";
    }

    private void ShowSheetMenu()
    {
        if (_sourcePath is null || _sheetNames.Count == 0)
        {
            return;
        }
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        foreach (var name in _sheetNames)
        {
            var item = new ToolStripMenuItem(name)
            {
                Checked = string.Equals(name, _inspection?.SheetName, StringComparison.OrdinalIgnoreCase)
            };
            var captured = name;
            item.Click += async (_, _) => await SwitchSheetAsync(captured);
            menu.Items.Add(item);
        }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Individua il foglio migliore", null, async (_, _) => await SwitchSheetAsync(null));
        menu.Show(_sheetButton, new Point(0, _sheetButton.Height));
    }

    /// <summary>
    /// Rilegge il file usando un foglio scelto a mano: le colonne già mappate
    /// vengono riproposte, i controlli e l'anteprima partono da zero.
    /// </summary>
    private async Task SwitchSheetAsync(string? sheetName)
    {
        if (_sourcePath is null)
        {
            return;
        }

        try
        {
            SetBusy(true, sheetName is null
                ? "Analisi dei fogli del file in corso…"
                : $"Lettura del foglio “{sheetName}” in corso…");
            WorkbookInspection inspection;
            try
            {
                inspection = await Task.Run(() => AttendanceWorkbookReader.Inspect(_sourcePath, sheetName));
            }
            finally
            {
                SetBusy(false);
            }

            var columns = inspection.SuggestedColumns ?? _columns;
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
            _inspection = inspection;
            _columns = columns;
            SetSourceProblems(read.Problems);
            ClearGeneratedPreview();
            ResetReview();
            UpdateSheetButton();
            _previewTitleLabel.Text = "File importato · pronto per la conversione";
            _previewSubtitleLabel.Text =
                $"Foglio letto: {inspection.SheetName}. Controlla i codici e genera l’anteprima dei movimenti.";
            UpdateWorkflowSummary();
            UpdateButtons();
            SetStatus(
                $"●   Foglio “{inspection.SheetName}”: {_requests.Count:N0} richieste lette" +
                (read.Problems.Count > 0 ? $", {read.Problems.Count:N0} righe da verificare." : "."),
                read.Problems.Count > 0);
        }
        catch (Exception exception) when (IsExpectedInputError(exception))
        {
            ShowError("Lettura del foglio non riuscita", exception);
        }
    }
}
