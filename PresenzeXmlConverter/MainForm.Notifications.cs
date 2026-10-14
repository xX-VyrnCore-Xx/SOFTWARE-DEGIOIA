using System.Diagnostics;

namespace PresenzeXmlConverter;

/// <summary>
/// Notifiche dell'applicazione. Tre canali, dal più discreto al più insistente:
/// il cartellino dentro la finestra (sempre, se la finestra è visibile), il lampeggio nella barra
/// delle applicazioni e il fumetto di Windows dal tray (solo quando la finestra non è in primo piano).
/// Le due scelte dell'utente stanno in Impostazioni → Notifiche.
/// </summary>
internal sealed partial class MainForm
{
    private const string NativeNotificationsSetting = "notifications";
    private const string InAppNotificationsSetting = "in-app-notifications";

    private ToastCenter? _toasts;

    private bool NativeNotificationsEnabled() =>
        !string.Equals(_database.GetSetting(NativeNotificationsSetting), "false", StringComparison.OrdinalIgnoreCase);

    private bool InAppNotificationsEnabled() =>
        !string.Equals(_database.GetSetting(InAppNotificationsSetting), "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Avvisa l'utente di un evento. Con la finestra in uso basta il cartellino interno; altrimenti
    /// (ridotta a icona, dietro altre finestre, nascosta nel tray) si aggiungono il lampeggio nella barra
    /// e il fumetto di Windows, se attivi.
    /// </summary>
    private void Notify(ToastKind kind, string title, string message, Action? onClick = null, bool systemNotification = false)
    {
        if (IsDisposed)
        {
            return;
        }
        if (Visible && WindowState != FormWindowState.Minimized && InAppNotificationsEnabled())
        {
            (_toasts ??= new ToastCenter(this)).Show(kind, title, message, onClick);
        }
        if (!systemNotification || IsWindowInUse)
        {
            return;
        }
        TaskbarFlash.Flash(this);
        ShowTrayNotification(
            title,
            message,
            icon: kind switch
            {
                ToastKind.Warning => ToolTipIcon.Warning,
                ToastKind.Error => ToolTipIcon.Error,
                _ => ToolTipIcon.Info
            });
    }

    /// <summary>Riempie il calendario con i movimenti correnti e il loro periodo.</summary>
    private void RefreshCalendar(IReadOnlyList<DailyMovement> movements) =>
        _calendarView.SetData(
            movements,
            _generationOptions,
            SafeProfiles(() => _database.GetEmployeeProfiles()),
            _inspection?.PeriodStart,
            _inspection?.PeriodEnd);

    /// <summary>
    /// Dopo la conversione ricorda che il calendario ha giorni da controllare: la barra in alto lo dice
    /// finché l'anteprima è aperta, il cartellino lo dice subito e porta alla scheda.
    /// </summary>
    private void NotifyCalendarAlerts()
    {
        var checks = _calendarView.CheckDayCount;
        var notices = _calendarView.NoticeDayCount;
        if (checks + notices == 0)
        {
            return;
        }
        var parts = new List<string>();
        if (checks > 0)
        {
            parts.Add(checks == 1 ? "1 giorno con ore oltre la giornata" : $"{checks} giorni con ore oltre la giornata");
        }
        if (notices > 0)
        {
            parts.Add(notices == 1 ? "1 giorno insolito" : $"{notices} giorni insoliti");
        }
        Notify(
            checks > 0 ? ToastKind.Warning : ToastKind.Info,
            "Da controllare nel calendario",
            string.Join(" e ", parts) + ". Clic per aprire il calendario.",
            () => _previewTabs.SelectedIndex = ReviewTabIndex + 1);
    }

    /// <summary>
    /// «Prova le notifiche» delle Impostazioni: mostra tutto subito, senza badare a dov'è la finestra,
    /// così l'utente vede quali canali funzionano sul suo computer.
    /// </summary>
    private void SendTestNotification(bool inApp, bool system)
    {
        if (inApp)
        {
            (_toasts ??= new ToastCenter(this)).Show(
                ToastKind.Success,
                "Notifiche attive",
                "Così compaiono gli avvisi di Vyrn Presence dentro la finestra.");
        }
        if (system)
        {
            ShowTrayNotification(
                "Prova notifica di Windows",
                "Se vedi questo messaggio, le notifiche di sistema funzionano.",
                force: true);
        }
    }

    /// <summary>Apre Esplora risorse con il file selezionato (o la cartella, se il file non c'è più).</summary>
    private static void RevealInExplorer(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                return;
            }
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Solo una comodità: il percorso resta nella barra di stato.
        }
    }
}
