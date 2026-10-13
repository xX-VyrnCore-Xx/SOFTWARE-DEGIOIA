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
        _ = Task.Run(() =>
        {
            try
            {
                if (_database.BackupIfDue(TimeSpan.FromHours(20)) is not null && !IsDisposed)
                {
                    BeginInvoke(RefreshRailStatus);
                }
            }
            catch (Exception exception) when (
                exception is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // Il backup è un di più: l'app funziona comunque (InvalidOperation: finestra già chiusa).
            }
        });
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

    private void ShowDatabaseStatus()
    {
        try
        {
            ShowInformation(
                "Stato del database",
                $"{_database.GetHealth().Describe(DateTime.UtcNow)}{Environment.NewLine}{Environment.NewLine}" +
                $"File: {_database.DatabasePath}{Environment.NewLine}Backup: {_database.BackupDirectory}");
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            ShowError("Stato del database non leggibile", exception);
        }
    }

    private void AddDataCommands(ContextMenuStrip menu)
    {
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Stato del database…", null, (_, _) => ShowDatabaseStatus());
        menu.Items.Add("Backup dei dati ora", null, (_, _) => BackupDatabaseNow());
        menu.Items.Add("Apri cartella dei dati", null, (_, _) => OpenDataFolder());
    }
}
