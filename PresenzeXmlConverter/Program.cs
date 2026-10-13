using System.Text;

namespace PresenzeXmlConverter;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Identità Windows: la barra delle applicazioni, le notifiche del tray e
        // i collegamenti bloccati usano lo stesso nome del prodotto. Il mutex
        // tiene traccia dell'esecuzione e permette all'installer di riconoscere
        // l'app aperta durante un aggiornamento. Entrambi vivono quanto il
        // processo: il mutex viene tenuto aperto per tutta Main.
        AppIdentity.Apply();
        using var setupMutex = AppIdentity.HoldSetupMutex();

        // Un solo PresenzeFlow per utente: un secondo avvio mostra la finestra già aperta.
        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            return;
        }

        // Nessun errore deve restare silenzioso: se l'avvio o un evento dell'interfaccia
        // falliscono, l'utente vede un messaggio leggibile invece di un'app che "non parte".
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) => ShowFatalError(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            ShowFatalError(args.ExceptionObject as Exception
                ?? new Exception("Errore sconosciuto durante l'avvio."));

        try
        {
            var window = new MainForm();
            instance.OnActivationRequested(window.ActivateFromAnotherInstance);
            Application.Run(window);
        }
        catch (Exception exception)
        {
            ShowFatalError(exception);
        }
    }

    private static void ShowFatalError(Exception exception)
    {
        var details = FullMessage(exception);
        TryWriteErrorLog(details);

        MessageBox.Show(
            details + Environment.NewLine + Environment.NewLine +
            "Il dettaglio è stato salvato in:" + Environment.NewLine +
            ErrorLogPath,
            "PresenzeFlow non può partire",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static string FullMessage(Exception exception)
    {
        var builder = new StringBuilder();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            builder.AppendLine(current.Message);
        }
        return builder.ToString().Trim();
    }

    private static string ErrorLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeGioia",
        "PresenzeXmlConverter",
        "error.log");

    private static void TryWriteErrorLog(string details)
    {
        try
        {
            var path = ErrorLogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(
                path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]{Environment.NewLine}{details}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // La scrittura del log non deve mai mascherare il messaggio d'errore originario.
        }
    }
}
