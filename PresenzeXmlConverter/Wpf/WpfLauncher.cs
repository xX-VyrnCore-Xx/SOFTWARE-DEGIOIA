using System.Windows;

namespace PresenzeXmlConverter.Wpf;

/// <summary>
/// Avvio della nuova interfaccia. Qualunque errore prima che la finestra sia comparsa (librerie grafiche assenti,
/// stile non valido, ...) viene scritto nel registro degli errori e restituisce <c>false</c>: chi chiama apre allora
/// chi chiama mostra il messaggio d'errore con il percorso del registro.
/// </summary>
internal static class WpfLauncher
{
    public static bool TryRun(SingleInstance instance, Action<Exception> log, bool backgroundStart = false)
    {
        var shown = false;
        TrayController? tray = null;
        try
        {
            var app = WpfApp.EnsureApplication();
            var service = new ConversionService(LocalDatabase.OpenWithRecovery().Database);
            var window = new ShellWindow(service) { ManageLifecycle = true };
            window.ContentRendered += (_, _) => shown = true;
            tray = TryCreateTray(window, service.Database, log);
            if (tray is not null)
            {
                window.AttachTray(tray);
            }
            var updateUi = new WpfUpdateUi(window, () => window.Dispatcher.BeginInvoke(() =>
            {
                window.RestoreFromTray();
                window.Navigate(ShellWindow.SettingsSection);
                window.ShowSettingsTab(1);
            }));
            var updates = new UpdateController(
                service.Database,
                window.Notifier,
                updateUi,
                () => service.HasPreview && service.FindPreviousDelivery() is null);
            updateUi.InstallProgress += updates.ReportProgress;
            window.AttachUpdates(updates);
            updates.Start();
            instance.OnActivationRequested(() => window.Dispatcher.BeginInvoke(window.RestoreFromTray));
            // Chiusura della sessione di Windows (spegnimento, disconnessione): niente tray, il programma esce.
            app.SessionEnding += (_, _) => window.ForceCloseOnSessionEnd();
            // L'app finisce quando la finestra principale si chiude davvero (non quando va nel tray): lo decide lei con Shutdown().
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (backgroundStart && tray is not null)
            {
                // Avvio con Windows: resta nel tray senza aprire la finestra (gli aggiornamenti arrivano lo stesso).
                tray.Visible = true;
                shown = true;
                app.Run();
            }
            else
            {
                app.Run(window);
            }
            return true;
        }
        catch (Exception exception)
        {
            log(exception);
            try
            {
                tray?.Dispose();
            }
            catch (Exception)
            {
                // Pulizia di cortesia.
            }
            // Un errore dopo che la finestra era aperta non riapre un'altra interfaccia sotto gli occhi dell'utente.
            return shown;
        }
    }

    private static TrayController? TryCreateTray(Window window, LocalDatabase database, Action<Exception> log)
    {
        try
        {
            var path = Environment.ProcessPath ?? string.Empty;
            var icon = (path.Length > 0 ? System.Drawing.Icon.ExtractAssociatedIcon(path) : null) ?? System.Drawing.SystemIcons.Application;
            var version = typeof(WpfLauncher).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
            var dpi = (int)System.Windows.Media.VisualTreeHelper.GetDpi(window).PixelsPerInchX;
            return new TrayController(database, $"Vyrn Presence {version}", icon, dpi);
        }
        catch (Exception exception)
        {
            // Senza tray il programma funziona lo stesso: solo senza avvisi dal tray.
            log(exception);
            return null;
        }
    }
}
