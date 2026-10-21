using System.Windows;

namespace PresenzeXmlConverter.Wpf;

/// <summary>
/// Avvio della nuova interfaccia. Qualunque errore prima che la finestra sia comparsa (librerie grafiche assenti,
/// stile non valido, ...) viene scritto nel registro degli errori e restituisce <c>false</c>: chi chiama apre allora
/// l'interfaccia classica, così il programma parte sempre.
/// </summary>
internal static class WpfLauncher
{
    public static bool TryRun(SingleInstance instance, Action<Exception> log)
    {
        var shown = false;
        try
        {
            var app = WpfApp.EnsureApplication();
            var service = new ConversionService(LocalDatabase.OpenWithRecovery().Database);
            var window = new ShellWindow(service);
            window.ContentRendered += (_, _) => shown = true;
            instance.OnActivationRequested(() => window.Dispatcher.BeginInvoke(() =>
            {
                if (window.WindowState == WindowState.Minimized)
                {
                    window.WindowState = WindowState.Normal;
                }
                window.Activate();
            }));
            // L'app finisce con la finestra principale (nei test resta aperta: un'Application si crea una volta sola).
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(window);
            return true;
        }
        catch (Exception exception)
        {
            log(exception);
            // Un errore dopo che la finestra era aperta non riapre un'altra interfaccia sotto gli occhi dell'utente.
            return shown;
        }
    }
}
