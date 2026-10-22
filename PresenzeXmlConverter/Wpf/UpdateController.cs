using System.Windows;
using System.Windows.Threading;

namespace PresenzeXmlConverter.Wpf;

using TextBlock = System.Windows.Controls.TextBlock;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Cursors = System.Windows.Input.Cursors;
using DragDropEffects = System.Windows.DragDropEffects;
using Button = System.Windows.Controls.Button;
using Control = System.Windows.Controls.Control;
using Label = System.Windows.Controls.Label;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using Window = System.Windows.Window;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Orientation = System.Windows.Controls.Orientation;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Brush = System.Windows.Media.Brush;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using DataGrid = System.Windows.Controls.DataGrid;
using Binding = System.Windows.Data.Binding;

/// <summary>Le finestre degli aggiornamenti (le mostra la nuova interfaccia; i test le sostituiscono).</summary>
internal interface IUpdateUi
{
    /// <summary>Nuova versione: le novità e «Aggiorna ora» / «Più tardi». true = aggiornare.</summary>
    bool ShowUpdateAvailable(UpdateInfo update, Version running);

    /// <summary>C'è un'anteprima non ancora esportata: aggiornare lo stesso?</summary>
    bool ConfirmUnsavedPreview();

    void ShowInformation(string title, string message);

    void ShowError(string title, Exception exception);

    /// <summary>Scarica, verifica e avvia l'installer mostrando l'avanzamento. Lancia l'errore se non riesce; <see cref="OperationCanceledException"/> se l'utente annulla.</summary>
    Task RunInstallAsync(UpdateInfo update, LocalDatabase database);

    /// <summary>Le novità della versione appena installata.</summary>
    void ShowWhatsNew(Version version, string notes);

    /// <summary>Manca il token e la repository è privata: si porta l'utente alle Impostazioni → Aggiornamenti.</summary>
    void PromptForToken();
}

/// <summary>L'avanzamento di un aggiornamento in corso: il passo, la percentuale del download e il dettaglio (velocità, tempo che resta).</summary>
internal sealed record UpdateProgressInfo(UpdateStep Step, int Percent, string Detail);

/// <summary>
/// Il ciclo degli aggiornamenti della nuova interfaccia, come nella classica: controllo all'avvio (ritardato), ogni dieci minuti anche
/// con la finestra nel tray, al ritorno della rete e alla fine della sospensione, e quando si torna alla finestra dopo un po'.
/// Un aggiornamento trovato si annuncia con un avviso discreto (mai una finestra a sorpresa), dal tray e dalla barra laterale si installa.
/// </summary>
internal sealed class UpdateController
{
    private readonly LocalDatabase _database;
    private readonly WpfNotifier _notifier;
    private readonly IUpdateUi _ui;
    private readonly Func<Version, string?, Task<UpdateCheckResult>> _check;
    private readonly Func<bool> _hasUnexportedPreview;
    private readonly DispatcherTimer _startupDelay = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly DispatcherTimer _periodic = new() { Interval = UpdateCadence.PollInterval };
    private readonly DispatcherTimer _retry = new() { Interval = UpdateCadence.RetryInterval };
    private readonly DispatcherTimer _settle = new() { Interval = UpdateCadence.SettleDelay };
    private DateTime? _lastCheckUtc;
    private bool _busy;
    private bool _startupCheckDone;
    private bool _firstRun;
    private bool _stopped;

    public UpdateController(
        LocalDatabase database,
        WpfNotifier notifier,
        IUpdateUi ui,
        Func<bool> hasUnexportedPreview,
        Func<Version, string?, Task<UpdateCheckResult>>? check = null,
        Version? running = null)
    {
        _database = database;
        _notifier = notifier;
        _ui = ui;
        _hasUnexportedPreview = hasUnexportedPreview;
        _check = check ?? ((version, endpoint) => UpdateService.CheckAsync(version, endpoint));
        Running = running ?? UpdateWorkflow.RunningVersion;
        StatusText = UpdateService.ResolveToken() is null
            ? "Aggiornamenti · attivi"
            : $"Aggiornamenti · collegato ({UpdateService.DescribeTokenSource()})";
    }

    public Version Running { get; }

    /// <summary>Aggiornamento trovato e non ancora installato (null = nessuno).</summary>
    public UpdateInfo? Pending { get; private set; }

    /// <summary>Lo stato dei controlli in una riga («Aggiornamenti · aggiornato alla 1.65.0 · 10:45»).</summary>
    public string StatusText { get; private set; }

    public bool StatusIsWarning { get; private set; }

    public bool IsChecking => _busy;

    /// <summary>L'aggiornamento in corso di scaricamento e installazione (null = nessuno).</summary>
    public UpdateProgressInfo? Progress { get; private set; }

    /// <summary>Cambia l'avanzamento (download, controllo, installazione) o finisce.</summary>
    public event Action? ProgressChanged;

    /// <summary>Riceve l'avanzamento dalla finestra dell'installazione, così il resto dell'interfaccia lo segue in tempo reale.</summary>
    internal void ReportProgress(UpdateStep step, int percent, string detail)
    {
        Progress = new UpdateProgressInfo(step, percent, detail);
        ProgressChanged?.Invoke();
    }

    public event Action? PendingChanged;

    public event Action? StatusChanged;

    /// <summary>Chiamato quando l'installer è partito: il programma deve chiudersi per lasciarlo lavorare.</summary>
    public event Action? ExitRequested;

    /// <summary>Per i test: ricorda cosa è successo all'ultimo controllo.</summary>
    internal UpdateAnnouncement? LastAnnouncement { get; private set; }

    public void Start()
    {
        // Primo avvio: se la repo è privata e il token non c'è, conviene chiederlo subito una volta sola.
        _firstRun = SafeSetting(UpdateWorkflow.LastRunVersionSetting) is null;
        _startupDelay.Tick += async (_, _) =>
        {
            _startupDelay.Stop();
            if (_stopped)
            {
                return;
            }
            // Prima le novità dell'eventuale aggiornamento appena installato (solo a finestra visibile), poi la ricerca.
            if (_notifier.IsWindowInUse)
            {
                await ShowWhatsNewIfNeededAsync();
            }
            await CheckAsync(userInitiated: false);
        };
        _startupDelay.Start();
        _periodic.Tick += async (_, _) =>
        {
            // Anche con la finestra nascosta in tray: il programma può restare aperto per giorni.
            if (_stopped || _busy || !AutoUpdateEnabled())
            {
                return;
            }
            await CheckAsync(userInitiated: false);
        };
        _periodic.Start();
        _retry.Tick += async (_, _) =>
        {
            _retry.Stop();
            if (_stopped || _busy || !AutoUpdateEnabled())
            {
                return;
            }
            await CheckAsync(userInitiated: false);
        };
        _settle.Tick += (_, _) =>
        {
            _settle.Stop();
            RequestCheck(staleOnly: false);
        };
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    public void Stop()
    {
        _stopped = true;
        _startupDelay.Stop();
        _periodic.Stop();
        _retry.Stop();
        _settle.Stop();
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
    }

    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode == Microsoft.Win32.PowerModes.Resume)
        {
            ScheduleSettledCheck();
        }
    }

    private void OnNetworkAvailabilityChanged(object? sender, System.Net.NetworkInformation.NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable)
        {
            ScheduleSettledCheck();
        }
    }

    // Dopo la sospensione o il ritorno della rete si lascia un istante a Windows per ristabilire il collegamento.
    private void ScheduleSettledCheck() => _settle.Dispatcher.BeginInvoke(() =>
    {
        _settle.Stop();
        _settle.Start();
    });

    /// <summary>Cerca un aggiornamento se è ora di farlo: mai a ridosso del precedente, mai con il controllo spento o già in corso.</summary>
    public void RequestCheck(bool staleOnly)
    {
        if (_stopped || _busy || !AutoUpdateEnabled() || !UpdateCadence.ShouldCheck(_lastCheckUtc, DateTime.UtcNow, staleOnly))
        {
            return;
        }
        _ = CheckAsync(userInitiated: false);
    }

    private bool AutoUpdateEnabled()
    {
        var value = SafeSetting(UpdateWorkflow.AutoUpdateSetting);
        return value is null || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    private string? SafeSetting(string key)
    {
        try
        {
            return _database.GetSetting(key);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            return null;
        }
    }

    private void SetStatus(string text, bool warning)
    {
        StatusText = text;
        StatusIsWarning = warning;
        StatusChanged?.Invoke();
    }

    private void SetPending(UpdateInfo? update)
    {
        Pending = update;
        _notifier.Tray?.SetPendingUpdate(update?.Version);
        PendingChanged?.Invoke();
    }

    /// <summary>Cerca una versione più recente. Con <paramref name="userInitiated"/> dice sempre il risultato (anche «sei aggiornato»).</summary>
    public async Task CheckAsync(bool userInitiated)
    {
        if (_busy)
        {
            return;
        }
        if (!userInitiated && !AutoUpdateEnabled())
        {
            return;
        }
        _busy = true;
        _lastCheckUtc = DateTime.UtcNow;
        StatusChanged?.Invoke();
        try
        {
            var result = await _check(Running, SafeSetting(UpdateWorkflow.UpdateEndpointSetting));
            _retry.Stop();
            if (!result.Succeeded)
            {
                if (result.IsTransient && !userInitiated)
                {
                    _retry.Start();
                }
                var error = result.Error ?? "Controllo non riuscito.";
                SetStatus($"Aggiornamenti · {UpdateWorkflow.SummarizeError(error)}", warning: true);
                if (userInitiated)
                {
                    _ui.ShowInformation("Aggiornamenti", error);
                }
                // Token mancante o non valido: si propone una volta sola la configurazione, senza interrompere il lavoro.
                if (UpdateWorkflow.NeedsTokenAttention(error) && (_firstRun || userInitiated))
                {
                    _ui.PromptForToken();
                }
                return;
            }
            if (result.Update is null)
            {
                SetPending(null);
                SetStatus($"Aggiornamenti · aggiornato alla {Running.ToString(3)} · {DateTime.Now:HH:mm}", warning: false);
                if (userInitiated)
                {
                    _ui.ShowInformation("Aggiornamenti", $"Vyrn Presence è aggiornato (versione {Running.ToString(3)}).");
                }
                return;
            }

            var update = result.Update;
            SetPending(update);
            SetStatus($"Aggiornamenti · nuova versione {update.Version.ToString(3)}", warning: true);
            var announcement = UpdateWorkflow.Announce(
                update, userInitiated, SafeSetting(UpdateWorkflow.SkippedVersionSetting), _notifier.IsWindowInUse, _startupCheckDone);
            LastAnnouncement = announcement;
            if (announcement == UpdateAnnouncement.Silent)
            {
                return;
            }
            if (announcement == UpdateAnnouncement.Notify)
            {
                NotifyAvailable(update);
                return;
            }
            if (!_ui.ShowUpdateAvailable(update, Running))
            {
                _database.SaveSetting(UpdateWorkflow.SkippedVersionSetting, update.Version.ToString());
                SetStatus("Aggiornamento rimandato: lo riprendi dal menu dell'icona nel tray o dalla barra laterale.", warning: false);
                return;
            }
            if (!_hasUnexportedPreview() || _ui.ConfirmUnsavedPreview())
            {
                await InstallAsync(update);
                return;
            }
            SetStatus("Aggiornamento rimandato: salva prima il file XML, poi riprendilo dal menu.", warning: false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or
            System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            if (userInitiated)
            {
                _ui.ShowError("Aggiornamento non riuscito", exception);
            }
        }
        finally
        {
            _busy = false;
            if (!userInitiated)
            {
                _startupCheckDone = true;
            }
            StatusChanged?.Invoke();
        }
    }

    /// <summary>Un avviso al giorno per la stessa versione (subito per una versione nuova): il controllo gira ogni dieci minuti.</summary>
    private void NotifyAvailable(UpdateInfo update)
    {
        var today = DateTime.Now;
        if (!UpdateNotice.ShouldNotify(SafeSetting(UpdateWorkflow.UpdateNoticeSetting), update.Version, today))
        {
            return;
        }
        try
        {
            _database.SaveSetting(UpdateWorkflow.UpdateNoticeSetting, UpdateNotice.Record(update.Version, today));
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // Senza memoria dell'ultimo avviso si avvisa comunque.
        }
        _notifier.Notify(
            ToastKind.Info,
            $"Aggiornamento {update.Version.ToString(3)} disponibile",
            UpdateNotice.BalloonText(Running, update.Version, update.Notes),
            () => _ = CheckAsync(userInitiated: true),
            systemNotification: true);
    }

    /// <summary>Installa un aggiornamento: se l'installer parte il programma si chiude, altrimenti l'errore si mostra.</summary>
    public async Task InstallAsync(UpdateInfo update)
    {
        try
        {
            ReportProgress(UpdateStep.Download, 0, "Connessione a GitHub…");
            await _ui.RunInstallAsync(update, _database);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Aggiornamento annullato: nessun file è stato installato.", warning: false);
            return;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or HttpRequestException or
            TaskCanceledException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // L'utente ha accettato l'aggiornamento: l'errore va sempre mostrato, anche se il controllo era automatico.
            SetStatus("Aggiornamento non riuscito.", warning: true);
            _ui.ShowError("Aggiornamento non riuscito", exception);
            return;
        }
        finally
        {
            Progress = null;
            ProgressChanged?.Invoke();
        }
        SetStatus("Aggiornamento avviato: Vyrn Presence si chiude per completarlo.", warning: false);
        ExitRequested?.Invoke();
    }

    /// <summary>L'azione di «Installa l'aggiornamento…»: la finestra con le novità e poi l'installazione.</summary>
    public Task InstallPendingAsync() => CheckAsync(userInitiated: true);

    /// <summary>Se il software è appena stato aggiornato mostra che cosa è cambiato (le note salvate prima del riavvio, senza rete).</summary>
    public async Task ShowWhatsNewIfNeededAsync()
    {
        var current = Running;
        var lastRun = SafeSetting(UpdateWorkflow.LastRunVersionSetting);
        try
        {
            _database.SaveSetting(UpdateWorkflow.LastRunVersionSetting, current.ToString());
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            return;
        }
        var plan = UpdateWorkflow.PlanWhatsNew(
            current,
            lastRun,
            SafeSetting(UpdateWorkflow.PendingUpdateVersionSetting),
            SafeSetting(UpdateWorkflow.PendingUpdateNotesSetting));
        if (plan.ClearPending)
        {
            _database.SaveSetting(UpdateWorkflow.PendingUpdateVersionSetting, string.Empty);
            _database.SaveSetting(UpdateWorkflow.PendingUpdateNotesSetting, string.Empty);
        }
        switch (plan.Kind)
        {
            case WhatsNewKind.StoredNotes:
                _ui.ShowWhatsNew(current, plan.Notes!);
                SetStatus($"Aggiornamento completato: versione {current.ToString(3)}.", warning: false);
                break;
            case WhatsNewKind.FetchedNotes:
                // Aggiornamento fatto fuori dall'app: si prova a leggere le note della Release; altrimenti un messaggio generico.
                var fetched = await UpdateService.TryGetReleaseNotesAsync(current);
                _ui.ShowWhatsNew(current, fetched ?? UpdateWorkflow.GenericNotes(current));
                SetStatus($"Aggiornamento completato: versione {current.ToString(3)}.", warning: false);
                break;
        }
    }
}

/// <summary>Le finestre degli aggiornamenti vere: note, avanzamento, conferme e avvisi sopra la finestra principale.</summary>
internal sealed class WpfUpdateUi : IUpdateUi
{
    private readonly Window _owner;
    private readonly Action _openUpdateSettings;

    /// <summary>L'avanzamento dell'installazione, in tempo reale (passo, percentuale, dettaglio).</summary>
    public event Action<UpdateStep, int, string>? InstallProgress;

    public WpfUpdateUi(Window owner, Action openUpdateSettings)
    {
        _owner = owner;
        _openUpdateSettings = openUpdateSettings;
    }

    public bool ShowUpdateAvailable(UpdateInfo update, Version running)
    {
        var window = ReleaseNotesWindow.Create(
            $"Nuova versione {update.Version.ToString(3)} disponibile",
            $"La versione installata è la {running.ToString(3)}. Aggiorna ora per ricevere le novità: il download è verificato con checksum SHA-256.",
            update.Notes,
            showUpdateButton: true);
        return window.ShowOn(_owner);
    }

    public bool ConfirmUnsavedPreview() =>
        MessageBox.Show(
            _owner,
            "Hai un'anteprima che non è ancora stata esportata in XML. L'aggiornamento chiude il programma e " +
            "dovresti rifare l'importazione." + Environment.NewLine + Environment.NewLine +
            "Vuoi aggiornare lo stesso? Scegli «No» per salvare prima il file: l'aggiornamento resta disponibile dal menu.",
            "Aggiornamento",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;

    public void ShowInformation(string title, string message) =>
        MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string title, Exception exception) =>
        MessageBox.Show(_owner, exception.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public async Task RunInstallAsync(UpdateInfo update, LocalDatabase database)
    {
        var progress = new UpdateProgressWindow(
            update.Version.ToString(3),
            (message, current, installer) => MessageBox.Show(
                _owner,
                message + Environment.NewLine + Environment.NewLine +
                $"Firmatario attuale: {current}" + Environment.NewLine +
                $"Firmatario dell'installer: {installer}" + Environment.NewLine + Environment.NewLine +
                "Vuoi installare lo stesso?",
                "Aggiornamento firmato da un altro certificato",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) == MessageBoxResult.Yes);
        if (_owner.IsLoaded)
        {
            progress.Owner = _owner;
        }
        progress.Reported += (step, percent, detail) => InstallProgress?.Invoke(step, percent, detail);
        progress.Show();
        try
        {
            await UpdateInstaller.InstallAsync(update, database, progress);
        }
        finally
        {
            progress.Close();
        }
    }

    public void ShowWhatsNew(Version version, string notes) =>
        ReleaseNotesWindow.Create(
            $"Vyrn Presence aggiornato alla {version.ToString(3)}",
            "Ecco che cosa è cambiato in questa versione.",
            notes,
            showUpdateButton: false).ShowOn(_owner);

    public void PromptForToken() => _openUpdateSettings();
}
