using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter;

/// <summary>
/// Database sempre disponibile: apertura che si ripara da sola, backup
/// automatici e comandi per vedere e copiare i dati. La cartella dei dati è
/// anche quella che l'installer, alla disinstallazione, chiede se conservare o
/// eliminare.
/// </summary>
internal sealed partial class MainForm
{
    /// <summary>Che cosa è stato fatto per riaprire il database all'avvio (null = nulla di speciale).</summary>
    private static string? _databaseStartupNote;

    /// <summary>
    /// Apre il database con riparazione automatica. È chiamato dall'inizializzatore
    /// del campo: l'app deve partire anche se il file è danneggiato o la cartella
    /// dei dati non è scrivibile.
    /// </summary>
    private static LocalDatabase OpenDatabase()
    {
        var opened = LocalDatabase.OpenWithRecovery();
        _databaseStartupNote = opened.Note;
        return opened.Database;
    }

    /// <summary>
    /// Dopo il primo disegno: avvisa se il database è stato riparato e, se
    /// l'ultimo backup è vecchio, ne crea uno nuovo senza bloccare la finestra.
    /// </summary>
    private void StartDatabaseMaintenance()
    {
        if (_databaseStartupNote is not null)
        {
            var note = _databaseStartupNote;
            _databaseStartupNote = null;
            SetStatus($"●   {note}", true);
            MessageBox.Show(this, note, "Database locale", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        RefreshRailStatus();
        NotifyCertificateExpiry();
        _ = Task.Run(() =>
        {
            try
            {
                if (_database.BackupIfDue(TimeSpan.FromHours(20)) is not null && !IsDisposed)
                {
                    BeginInvoke(RefreshRailStatus);
                }
                // Copia dei codici e delle aziende pronta per il cambio PC (solo se sono cambiati).
                CodesAutoBackup.Run(_database, DateTime.Now);
            }
            catch (Exception exception) when (
                exception is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // Il backup è un di più: l'app funziona comunque (InvalidOperation: finestra già chiusa).
            }
        });
    }

    /// <summary>Una volta al giorno ricorda che il certificato di firma sta per scadere (o è scaduto).</summary>
    private void NotifyCertificateExpiry()
    {
        var notice = ExecutableSignature.ExpiryNotice(ExecutableSignature.CurrentCertificateExpiry(), DateTime.Now);
        if (notice is null)
        {
            return;
        }
        var today = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            if (_database.GetSetting("cert-expiry-notice-day") == today)
            {
                return;
            }
            _database.SaveSetting("cert-expiry-notice-day", today);
        }
        catch (SqliteException)
        {
            return;
        }
        Notify(ToastKind.Warning, "Certificato di firma", notice);
    }

    /// <summary>Aggiorna lo stato dei dati mostrato in fondo alla barra laterale.</summary>
    private void RefreshRailStatus()
    {
        try
        {
            var health = _database.GetHealth();
            var now = DateTime.UtcNow;
            _railDataLabel.Text = health.IsTemporary ? "●  Database temporaneo" : "●  Database collegato";
            _railDataLabel.ForeColor = health.IsTemporary ? Theme.WarningInk : Theme.SuccessInk;
            _railBackupLabel.Text = health.DescribeBackupsShort(now);
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            _railDataLabel.Text = "●  Database non leggibile";
            _railDataLabel.ForeColor = Theme.DangerInk;
            _railBackupLabel.Text = "controlla la cartella dei dati";
        }
    }

    private void BackupDatabaseNow()
    {
        try
        {
            var path = _database.CreateBackup("manuale");
            var health = _database.GetHealth().Describe(DateTime.UtcNow);
            RefreshRailStatus();
            SetStatus($"●   Backup creato: {Path.GetFileName(path)}", false);
            Audit(ActivityKind.Backup, "Backup dei dati creato", Path.GetFileName(path));
            ShowInformation(
                "Backup dei dati",
                $"Backup creato in:{Environment.NewLine}{path}{Environment.NewLine}{Environment.NewLine}{health}");
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            ShowError("Backup non riuscito", exception);
        }
    }

    private void OpenDataFolder()
    {
        var folder = Path.GetDirectoryName(_database.DatabasePath) ?? LocalDatabase.DataDirectory;
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            SetStatus($"Impossibile aprire la cartella dei dati: {exception.Message}", true);
        }
    }

    /// <summary>Apre la cartella con le copie automatiche di codici e aziende (si crea se manca).</summary>
    private void OpenCodesBackupFolder()
    {
        var folder = CodesAutoBackup.FolderFor(_database);
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            SetStatus($"Impossibile aprire la cartella delle copie: {exception.Message}", true);
        }
    }

    private void ShowDatabaseStatus()
    {
        try
        {
            ShowInformation(
                "Stato del database",
                $"{_database.GetHealth().Describe(DateTime.UtcNow)}{Environment.NewLine}{Environment.NewLine}" +
                $"Controllo dei backup: {_database.VerifyBackups().Describe()}{Environment.NewLine}{Environment.NewLine}" +
                $"File: {_database.DatabasePath}{Environment.NewLine}Backup: {_database.BackupDirectory}");
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            ShowError("Stato del database non leggibile", exception);
        }
    }

    /// <summary>Salva codici, aziende e profili in un file da portare su un altro PC (o da tenere come copia).</summary>
    private void ExportCodesBackup()
    {
        try
        {
            var backup = CodesBackup.Read(_database, DateTime.UtcNow);
            if (backup.Count == 0)
            {
                ShowInformation("Esporta codici e aziende", "Non c'è ancora nulla da esportare: inserisci prima codici o aziende.");
                return;
            }
            using var picker = new SaveFileDialog
            {
                Title = "Esporta codici e aziende",
                Filter = "Codici e aziende (*.json)|*.json",
                AddExtension = true,
                DefaultExt = "json",
                FileName = CodesBackup.SuggestFileName(DateTime.Now),
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
            File.WriteAllText(picker.FileName, backup.ToJson(), new System.Text.UTF8Encoding(false));
            SetStatus($"●   Codici e aziende esportati: {Path.GetFileName(picker.FileName)}", false);
            Audit(ActivityKind.Backup, "Codici e aziende esportati", $"{backup.Describe()} · {Path.GetFileName(picker.FileName)}");
            Notify(
                ToastKind.Success,
                "Codici e aziende esportati",
                $"{backup.Describe()} · clic per aprire la cartella.",
                () => RevealInExplorer(picker.FileName));
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            ShowError("Esportazione non riuscita", exception);
        }
    }

    /// <summary>
    /// Importa un file di codici e aziende (anche da un altro PC). Unisce: il file aggiunge e aggiorna, ciò che non
    /// contiene resta. Prima di scrivere si fa una copia di sicurezza del database.
    /// </summary>
    private void ImportCodesBackup()
    {
        try
        {
            using var picker = new OpenFileDialog
            {
                Title = "Importa codici e aziende",
                Filter = "Codici e aziende (*.json)|*.json|Tutti i file (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            // Si parte dalla cartella delle copie automatiche, se ce n'è già qualcuna.
            var autoFolder = CodesAutoBackup.FolderFor(_database);
            if (CodesAutoBackup.List(autoFolder).Count > 0)
            {
                picker.InitialDirectory = autoFolder;
            }
            if (picker.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            if (new FileInfo(picker.FileName).Length > CodesBackup.MaximumFileBytes)
            {
                ShowInformation("Importa codici e aziende", "Il file è troppo grande per essere un elenco di codici.");
                return;
            }
            if (!CodesBackup.TryParse(File.ReadAllText(picker.FileName), out var backup, out var problem))
            {
                ShowInformation("Importa codici e aziende", problem);
                return;
            }
            if (backup.Count == 0)
            {
                ShowInformation("Importa codici e aziende", "Il file non contiene nessun codice né azienda.");
                return;
            }
            var unsaved = _codesLoaded && _mappingPanel.HasUnsavedChanges
                ? $"{Environment.NewLine}{Environment.NewLine}Attenzione: nella pagina dei codici hai modifiche non salvate, che l'importazione sostituisce."
                : string.Empty;
            if (MessageBox.Show(
                    this,
                    $"Il file contiene {backup.Describe()}." +
                    $"{Environment.NewLine}{Environment.NewLine}I nuovi vengono aggiunti e quelli con un codice diverso aggiornati; " +
                    $"ciò che nel file non c'è resta com'è. Prima si fa una copia di sicurezza dei dati.{unsaved}" +
                    $"{Environment.NewLine}{Environment.NewLine}Importare?",
                    "Importa codici e aziende",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            _database.CreateBackup("prima-di-importare");
            var report = backup.ApplyTo(_database);
            _codesLoaded = false;
            LoadCompaniesQuietly();
            RefreshCompanies();
            EnsureCodesLoaded();
            RefreshRailStatus();
            InvalidatePreview();
            SetStatus($"●   Codici e aziende importati: {report.Describe()}", report.Skipped.Count > 0);
            Audit(ActivityKind.Backup, "Codici e aziende importati", report.Describe());
            var detail = report.Skipped.Count == 0
                ? report.Describe()
                : $"{report.Describe()}{Environment.NewLine}{Environment.NewLine}Scartati:{Environment.NewLine}" +
                  string.Join(Environment.NewLine, report.Skipped.Take(8)) +
                  (report.Skipped.Count > 8 ? $"{Environment.NewLine}… e altri {report.Skipped.Count - 8:N0}." : string.Empty);
            ShowInformation("Importazione terminata", detail);
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            ShowError("Importazione non riuscita", exception);
        }
    }

    // Il menu è uno solo e resta vivo: eliminarlo alla chiusura (evento Closed) lo distruggeva PRIMA che
    // Windows Forms eseguisse il clic della voce scelta, e il clic finiva su un controllo già distrutto
    // («Cannot access a disposed object… ContextMenuStrip»).
    private ContextMenuStrip? _transferMenu;

    /// <summary>Menu «Trasferisci»: esporta o importa codici e aziende per cambiare PC.</summary>
    private void ShowTransferMenu(Control anchor)
    {
        if (_transferMenu is null)
        {
            _transferMenu = ThemedMenus.Create();
            _transferMenu.Items.Add("Esporta codici e aziende…", null, (_, _) => ExportCodesBackup());
            _transferMenu.Items.Add("Importa codici e aziende…", null, (_, _) => ImportCodesBackup());
            _transferMenu.Items.Add(new ToolStripSeparator());
            _transferMenu.Items.Add("Apri la cartella delle copie automatiche", null, (_, _) => OpenCodesBackupFolder());
        }
        _transferMenu.Show(anchor, new Point(0, anchor.Height));
    }

    /// <summary>Pulsante «Trasferisci ▾», lo stesso in Gestione codici e in Aziende.</summary>
    private Button CreateTransferButton(Func<Button> create)
    {
        var transfer = create();
        transfer.Text = "Trasferisci  ▾";
        transfer.Click += (_, _) => ShowTransferMenu(transfer);
        Describe(
            transfer,
            "Trasferisci codici e aziende",
            "Esporta in un file i codici, le ore dei dipendenti e le aziende per usarli su un altro PC, oppure importali da un file.");
        return transfer;
    }

    private void AddDataCommands(ContextMenuStrip menu)
    {
        var dpi = DeviceDpi;
        menu.Items.Add(new ToolStripSeparator());
        ThemedMenus.Add(menu, "Esporta codici e aziende…", Glyph.Upload, (_, _) => ExportCodesBackup(), dpi);
        ThemedMenus.Add(menu, "Importa codici e aziende…", Glyph.Download, (_, _) => ImportCodesBackup(), dpi);
        ThemedMenus.Add(menu, "Stato del database…", Glyph.Database, (_, _) => ShowDatabaseStatus(), dpi);
        ThemedMenus.Add(menu, "Backup dei dati ora", Glyph.Save, (_, _) => BackupDatabaseNow(), dpi);
        ThemedMenus.Add(menu, "Apri cartella dei dati", Glyph.Folder, (_, _) => OpenDataFolder(), dpi);
        ThemedMenus.Add(menu, "Apri cartella delle copie dei codici", Glyph.Folder, (_, _) => OpenCodesBackupFolder(), dpi);
    }
}
