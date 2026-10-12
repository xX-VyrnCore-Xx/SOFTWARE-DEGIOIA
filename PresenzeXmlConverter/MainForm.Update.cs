using System.Diagnostics;

namespace PresenzeXmlConverter;

/// <summary>
/// Aggiornamenti tramite le Release GitHub (repository privata): controllo
/// all'avvio (silenzioso), proposta di aggiornamento con le novità, download
/// dell'installer con verifica del checksum e avvio dell'installazione.
/// Dopo il riavvio viene mostrato che cosa è cambiato.
/// </summary>
internal sealed partial class MainForm
{
    private const string AutoUpdateSetting = "auto-update-check";
    private const string SkippedVersionSetting = "skipped-update-version";
    private const string PendingUpdateVersionSetting = "pending-update-version";
    private const string PendingUpdateNotesSetting = "pending-update-notes";
    private const string LastRunVersionSetting = "last-run-version";
    private const string UpdateEndpointSetting = "update-endpoint";

    private readonly System.Windows.Forms.Timer _updateCheckDelay = new() { Interval = 4000 };
    private bool _updateBusy;

    private static Version RunningVersion =>
        typeof(MainForm).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);

    private void InitializeUpdates()
    {
        // Controllo all'avvio volutamente ritardato: non rallenta l'apertura della
        // finestra e non tocca nulla se l'utente sta già lavorando.
        _updateCheckDelay.Tick += async (_, _) =>
        {
            _updateCheckDelay.Stop();
            if (IsDisposed || !Visible)
            {
                return;
            }
            // Prima le novità dell'eventuale aggiornamento appena installato,
            // poi la ricerca di una versione più recente.
            await ShowWhatsNewIfNeededAsync();
            if (!IsDisposed && Visible)
            {
                await CheckForUpdatesAsync(userInitiated: false);
            }
        };
        _updateCheckDelay.Start();
    }

    private void AddUpdateCommands(ContextMenuStrip menu)
    {
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(
            "Cerca aggiornamenti…",
            null,
            async (_, _) => await CheckForUpdatesAsync(userInitiated: true));
        menu.Items.Add(
            "Novità di questa versione",
            null,
            (_, _) => ShowVersionNotes());
    }

    private bool AutoUpdateEnabled()
    {
        var value = _database.GetSetting(AutoUpdateSetting);
        return value is null || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    private void ShowVersionNotes()
    {
        var stored = _database.GetSetting(PendingUpdateNotesSetting);
        var notes = string.IsNullOrWhiteSpace(stored)
            ? "Per questa versione non ci sono note salvate in locale. " +
              "Le novità complete sono descritte nella Release su GitHub."
            : stored!;
        using var dialog = new ReleaseNotesDialog(
            $"Novità di PresenzeFlow {RunningVersion.ToString(3)}",
            "Riepilogo delle modifiche introdotte in questa versione.",
            notes,
            showUpdateButton: false);
        dialog.ShowDialog(this);
    }

    private async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (_updateBusy)
        {
            return;
        }
        if (!userInitiated && !AutoUpdateEnabled())
        {
            return;
        }

        _updateBusy = true;
        try
        {
            var result = await UpdateService.CheckAsync(
                RunningVersion,
                _database.GetSetting(UpdateEndpointSetting));
            if (!result.Succeeded)
            {
                if (userInitiated)
                {
                    ShowInformation("Aggiornamenti", result.Error ?? "Controllo non riuscito.");
                }
                return;
            }
            if (result.Update is null)
            {
                if (userInitiated)
                {
                    ShowInformation(
                        "Aggiornamenti",
                        $"PresenzeFlow è aggiornato (versione {RunningVersion.ToString(3)}).");
                }
                return;
            }

            var update = result.Update;
            // Un aggiornamento rimandato dall'utente non viene riproposto a ogni avvio,
            // ma resta disponibile dal menu "Cerca aggiornamenti…".
            if (!userInitiated &&
                string.Equals(
                    _database.GetSetting(SkippedVersionSetting),
                    update.Version.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            using var dialog = new ReleaseNotesDialog(
                $"Nuova versione {update.Version.ToString(3)} disponibile",
                $"La versione installata è la {RunningVersion.ToString(3)}. " +
                "Aggiorna ora per ricevere le novità: il download è verificato con checksum SHA-256.",
                update.Notes,
                showUpdateButton: true);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                _database.SaveSetting(SkippedVersionSetting, update.Version.ToString());
                SetStatus("●   Aggiornamento rimandato. Puoi riprenderlo dal menu dell'icona in tray.", false);
                return;
            }

            await DownloadAndInstallAsync(update);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            System.ComponentModel.Win32Exception or
            InvalidOperationException)
        {
            if (userInitiated)
            {
                ShowError("Aggiornamento non riuscito", exception);
            }
        }
        finally
        {
            _updateBusy = false;
        }
    }

    private async Task DownloadAndInstallAsync(UpdateInfo update)
    {
        var destination = Path.Combine(
            Path.GetTempPath(),
            $"PresenzeFlow-Setup-{update.Version.ToString(3)}.exe");
        using var progress = new UpdateProgressDialog(update.Version.ToString(3));
        progress.Show(this);
        try
        {
            await UpdateService.DownloadAsync(
                update,
                destination,
                new Progress<int>(percent => progress.SetProgress(percent, $"Scaricamento in corso… {percent}%")),
                progress.Token);
            progress.SetProgress(100);

            progress.SetStatus("Verifica del file scaricato…");
            var expectedChecksum = await UpdateService.TryGetChecksumAsync(update);
            if (expectedChecksum is not null && !UpdateService.VerifySha256(destination, expectedChecksum))
            {
                TryDelete(destination);
                throw new InvalidDataException(
                    "Il file scaricato non corrisponde al checksum della Release: aggiornamento annullato.");
            }

            // Le note vengono salvate prima del riavvio: dopo l'aggiornamento la
            // finestra "Novità" le mostra senza dover usare la rete.
            _database.SaveSetting(PendingUpdateVersionSetting, update.Version.ToString());
            _database.SaveSetting(PendingUpdateNotesSetting, update.Notes);

            progress.SetStatus("Avvio dell'installer…");
            StartInstaller(destination);
            progress.Close();
            SetStatus("●   Aggiornamento avviato: PresenzeFlow si chiude per completarlo.", false);
            _forceClose = true;
            Close();
        }
        catch (OperationCanceledException)
        {
            progress.Close();
            TryDelete(destination);
            SetStatus("●   Aggiornamento annullato: nessun file è stato installato.", false);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            HttpRequestException or
            TaskCanceledException or
            System.ComponentModel.Win32Exception or
            InvalidOperationException)
        {
            // L'utente ha accettato l'aggiornamento: l'errore va sempre mostrato,
            // anche quando il controllo era partito in automatico.
            progress.Close();
            TryDelete(destination);
            SetStatus("●   Aggiornamento non riuscito.", true);
            ShowError("Aggiornamento non riuscito", exception);
        }
        catch
        {
            progress.Close();
            throw;
        }
    }

    /// <summary>
    /// Se il software è appena stato aggiornato mostra che cosa è cambiato,
    /// usando le note salvate al momento del download (nessuna rete necessaria).
    /// </summary>
    private async Task ShowWhatsNewIfNeededAsync()
    {
        var current = RunningVersion;
        var previousText = _database.GetSetting(LastRunVersionSetting);
        _database.SaveSetting(LastRunVersionSetting, current.ToString());

        var pendingVersion = UpdateService.ParseVersion(_database.GetSetting(PendingUpdateVersionSetting));
        var pendingNotes = _database.GetSetting(PendingUpdateNotesSetting);
        if (pendingVersion is not null && current >= pendingVersion)
        {
            _database.SaveSetting(PendingUpdateVersionSetting, string.Empty);
            _database.SaveSetting(PendingUpdateNotesSetting, string.Empty);
            if (!string.IsNullOrWhiteSpace(pendingNotes))
            {
                ShowNotes(current, pendingNotes!);
                return;
            }
        }

        if (previousText is null)
        {
            // Primo avvio: non c'è nulla da raccontare.
            return;
        }
        var previous = UpdateService.ParseVersion(previousText);
        if (previous is null || current <= previous)
        {
            return;
        }

        // Aggiornamento fatto fuori dall'app: si prova a leggere le note della
        // Release della versione corrente; se non è possibile, messaggio generico.
        var fetched = await TryFetchNotesForCurrentVersionAsync(current);
        ShowNotes(
            current,
            fetched ?? $"PresenzeFlow è stato aggiornato alla versione {current.ToString(3)}. " +
                       "Le novità complete sono descritte nella Release su GitHub.");
    }

    private static Task<string?> TryFetchNotesForCurrentVersionAsync(Version version) =>
        UpdateService.TryGetReleaseNotesAsync(version);

    private void ShowNotes(Version version, string notes)
    {
        using var dialog = new ReleaseNotesDialog(
            $"PresenzeFlow aggiornato alla {version.ToString(3)}",
            "Ecco che cosa è cambiato in questa versione.",
            notes,
            showUpdateButton: false);
        dialog.ShowDialog(this);
        SetStatus($"●   Aggiornamento completato: versione {version.ToString(3)}.", false);
    }

    private static void StartInstaller(string installerPath)
    {
        var start = new ProcessStartInfo(installerPath)
        {
            UseShellExecute = true,
            Arguments = "/SILENT /NORESTART /CLOSEAPPLICATIONS"
        };
        if (Process.Start(start) is null)
        {
            throw new InvalidOperationException("Windows non ha avviato l'installer di aggiornamento.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void ShowInformation(string title, string message) =>
        MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
}
