using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Data.Sqlite;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using Window = System.Windows.Window;

namespace PresenzeXmlConverter.Wpf;

/// <summary>
/// Il pulsante «Trasferisci ▾» di Gestione codici e di Aziende: esporta codici e aziende in un file da portare su un altro PC,
/// li importa da un file (unendoli a quelli presenti, dopo una copia di sicurezza) e apre la cartella delle copie automatiche.
/// </summary>
internal sealed class TransferButton
{
    private readonly ConversionService _conversion;
    private readonly Func<Window?> _owner;
    private readonly Action<string> _status;
    private readonly Action _changed;
    private readonly ContextMenu _menu = new() { HasDropShadow = true, Placement = PlacementMode.Bottom };

    public TransferButton(ConversionService conversion, Func<Window?> owner, Action<string> status, Action changed)
    {
        _conversion = conversion;
        _owner = owner;
        _status = status;
        _changed = changed;
        Button = new Button { Content = "Trasferisci  ▾" };
        System.Windows.Automation.AutomationProperties.SetName(Button, "Trasferisci codici e aziende");
        Button.ToolTip = "Esporta in un file i codici, le ore dei dipendenti e le aziende per usarli su un altro PC, oppure importali da un file.";
        Button.Click += (_, _) => Open();
        _menu.PlacementTarget = Button;
    }

    public Button Button { get; }

    /// <summary>Le voci del menù (per i test).</summary>
    internal IReadOnlyList<MenuEntry> Entries() =>
    [
        new MenuEntry("Esporta codici e aziende…", Glyph.Upload, Export),
        new MenuEntry("Importa codici e aziende…", Glyph.Download, Import),
        MenuEntry.Separator,
        new MenuEntry("Apri la cartella delle copie automatiche", Glyph.Folder, OpenAutoBackupFolder)
    ];

    private void Open()
    {
        UiMenus.Fill(_menu, Entries());
        _menu.IsOpen = true;
    }

    internal void Export()
    {
        try
        {
            var database = _conversion.Database;
            if (CodesBackup.Read(database, DateTime.UtcNow).Count == 0)
            {
                MessageBox.Show(_owner(), "Non c'è ancora nulla da esportare: inserisci prima codici o aziende.", "Esporta codici e aziende", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Esporta codici e aziende",
                Filter = "Codici e aziende (*.json)|*.json",
                AddExtension = true,
                DefaultExt = "json",
                FileName = CodesBackup.SuggestFileName(DateTime.Now),
                OverwritePrompt = true
            };
            dialog.ClientGuid = FileDialogs.ClientId;
            var last = database.GetSetting("last-export-dir");
            if (!string.IsNullOrEmpty(last) && Directory.Exists(last))
            {
                dialog.InitialDirectory = last;
            }
            if (dialog.ShowDialog(_owner()) != true)
            {
                return;
            }
            var described = CodesTransfer.Export(database, dialog.FileName, DateTime.UtcNow);
            _status($"Codici e aziende esportati: {Path.GetFileName(dialog.FileName)} ({described}).");
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(_owner(), exception.Message, "Esportazione non riuscita", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    internal void Import()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importa codici e aziende",
                Filter = "Codici e aziende (*.json)|*.json|Tutti i file (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            dialog.ClientGuid = FileDialogs.ClientId;
            var autoFolder = CodesAutoBackup.FolderFor(_conversion.Database);
            if (CodesAutoBackup.List(autoFolder).Count > 0)
            {
                dialog.InitialDirectory = autoFolder;
            }
            if (dialog.ShowDialog(_owner()) != true)
            {
                return;
            }
            if (!CodesTransfer.TryRead(dialog.FileName, out var backup, out var problem))
            {
                MessageBox.Show(_owner(), problem, "Importa codici e aziende", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var answer = MessageBox.Show(
                _owner(),
                $"Il file contiene {backup.Describe()}." +
                $"{Environment.NewLine}{Environment.NewLine}I nuovi vengono aggiunti e quelli con un codice diverso aggiornati; " +
                $"ciò che nel file non c'è resta com'è. Prima si fa una copia di sicurezza dei dati." +
                $"{Environment.NewLine}{Environment.NewLine}Importare?",
                "Importa codici e aziende",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }
            var report = CodesTransfer.Import(_conversion.Database, backup);
            _conversion.InvalidateGenerated();
            _changed();
            _status($"Codici e aziende importati: {report.Describe()}");
            if (report.Skipped.Count > 0)
            {
                MessageBox.Show(
                    _owner(),
                    $"{report.Describe()}{Environment.NewLine}{Environment.NewLine}Scartati:{Environment.NewLine}" +
                    string.Join(Environment.NewLine, report.Skipped.Take(8)) +
                    (report.Skipped.Count > 8 ? $"{Environment.NewLine}… e altri {report.Skipped.Count - 8:N0}." : string.Empty),
                    "Importazione terminata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(_owner(), exception.Message, "Importazione non riuscita", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenAutoBackupFolder()
    {
        try
        {
            var folder = CodesAutoBackup.FolderFor(_conversion.Database);
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _status("Non riesco ad aprire la cartella delle copie automatiche.");
        }
    }
}
