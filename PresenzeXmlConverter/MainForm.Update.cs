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
    /// <summary>
    /// Controllo periodico: il collegamento alle Release resta attivo anche con
    /// il programma aperto per ore, non solo all'avvio.
    /// </summary>
    private readonly System.Windows.Forms.Timer _updatePeriodic = new() { Interval = 60 * 60 * 1000 };
    private readonly Label _updateStatusLabel = new();
    private bool _updateBusy;
    private bool _isFirstRun;
    private bool _updateTokenPromptShown;

    private static Version RunningVersion =>
        typeof(MainForm).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);

    private void InitializeUpdates()
    {
        // Primo avvio: se la repo è privata e il token non c'è, conviene chiederlo
        // subito una volta sola, invece di lasciare gli aggiornamenti spenti.
        _isFirstRun = _database.GetSetting(LastRunVersionSetting) is null;
        RefreshUpdateStatus(
            UpdateService.ResolveToken() is null
                ? "Aggiornamenti · token GitHub da configurare"
                : $"Aggiornamenti · collegato ({UpdateService.DescribeTokenSource()})",
            UpdateService.ResolveToken() is null);

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

        _updatePeriodic.Tick += async (_, _) =>
        {
            if (IsDisposed || !Visible || _updateBusy || !AutoUpdateEnabled())
            {
                return;
            }
            await CheckForUpdatesAsync(userInitiated: false);
        };
        _updatePeriodic.Start();
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
        menu.Items.Add("Impostazioni…", null, (_, _) => OpenSettings());
        menu.Items.Add("Scorciatoie da tastiera", null, (_, _) => ShowShortcuts());
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
                var error = result.Error ?? "Controllo non riuscito.";
                RefreshUpdateStatus($"Aggiornamenti · {SummarizeError(error)}", true);
                if (userInitiated)
                {
                    ShowInformation("Aggiornamenti", error);
                }
                // Token mancante o non valido: si propone una volta sola la
                // configurazione, senza continuare a interrompere il lavoro.
                if (NeedsTokenAttention(error) && (_isFirstRun || userInitiated))
                {
                    PromptForUpdateToken();
                }
                return;
            }
            if (result.Update is null)
            {
                RefreshUpdateStatus(
                    $"Aggiornamenti · aggiornato alla {RunningVersion.ToString(3)} · {DateTime.Now:HH:mm}",
                    false);
                if (userInitiated)
                {
                    ShowInformation(
                        "Aggiornamenti",
                        $"PresenzeFlow è aggiornato (versione {RunningVersion.ToString(3)}).");
                }
                return;
            }

            var update = result.Update;
            RefreshUpdateStatus($"Aggiornamenti · nuova versione {update.Version.ToString(3)}", true);
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

    /// <summary>
    /// Stato del collegamento alle Release nella barra inferiore: dice sempre se
    /// gli aggiornamenti sono attivi e da dove arriva il token.
    /// </summary>
    private void RefreshUpdateStatus(string text, bool isWarning)
    {
        _updateStatusLabel.Text = text;
        _updateStatusLabel.ForeColor = isWarning ? Theme.WarningInk : Theme.MutedInk;
    }

    /// <summary>Errore in una riga sola, adatto alla barra di stato.</summary>
    private static string SummarizeError(string error)
    {
        var firstLine = error.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? error;
        firstLine = firstLine.Trim();
        return firstLine.Length <= 80 ? firstLine : firstLine[..77] + "…";
    }

    /// <summary>True quando il controllo è fallito per un problema di token, non di rete.</summary>
    private static bool NeedsTokenAttention(string error) =>
        error.Contains("privato", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("token", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Apre le impostazioni sul punto giusto quando manca il token: la repo è
    /// privata e senza token l'app non può leggere le Release.
    /// </summary>
    private void PromptForUpdateToken()
    {
        if (_updateTokenPromptShown || IsDisposed)
        {
            return;
        }
        _updateTokenPromptShown = true;
        SetStatus(
            "La repository delle Release è privata: incolla il token GitHub in Impostazioni → Aggiornamenti e premi “Verifica e salva token”.",
            true);
        OpenSettings();
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
