using System.Runtime.InteropServices;

namespace PresenzeXmlConverter;

/// <summary>
/// Identità Windows dell'applicazione. Sono due valori che Windows usa per
/// riconoscere il programma e che prima erano impliciti e diversi dal nome
/// dell'eseguibile:
///
/// * l'<b>AppUserModelID</b> raggruppa la finestra, le notifiche e il
///   collegamento bloccato nella barra delle applicazioni sotto un'unica
///   identità (senza, una notifica inviata dal tray non è attribuita all'app);
/// * il <b>mutex</b> tiene traccia dell'esecuzione in corso ed è lo stesso nome
///   dichiarato in <c>[Setup] AppMutex</c> dell'installer, così un aggiornamento
///   sa che l'app è aperta e la chiude in modo ordinato invece di fallire sui
///   file in uso.
///
/// Tenendoli qui, applicazione e installer non possono più divergere.
/// </summary>
internal static class AppIdentity
{
    /// <summary>Identificatore stabile usato dalla barra delle applicazioni e dalle notifiche.</summary>
    public const string AppUserModelId = "DeGioia.PresenzeFlow";

    /// <summary>
    /// Nome del mutex dell'esecuzione, ripetuto in <c>Installer.iss</c>:
    /// <c>#define AppMutex "DeGioia.PresenzeFlow.Setup"</c>.
    /// </summary>
    public const string MutexName = "DeGioia.PresenzeFlow.Setup";

    /// <summary>
    /// Comunica a Windows l'AppUserModelID del processo. Serve prima che venga
    /// creata la finestra: dopo, la barra delle applicazioni avrebbe già
    /// assegnato al programma un'identità diversa (quella dell'eseguibile).
    /// Fuori da Windows o se shell32 non risponde non cambia nulla.
    /// </summary>
    public static void Apply()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        try
        {
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        }
        catch (Exception)
        {
            // Identità di default: l'app si avvia lo stesso.
        }
    }

    /// <summary>
    /// Tiene aperto il mutex per tutta la durata del processo. Il valore di
    /// ritorno va conservato: rilasciarlo segnala a Windows che l'app è chiusa.
    /// Se il sistema non permette di crearlo (caso raro) l'app funziona come
    /// prima: non è una funzione da cui dipende l'avvio.
    /// </summary>
    public static IDisposable? HoldSetupMutex()
    {
        try
        {
            return new Mutex(initiallyOwned: false, MutexName);
        }
        catch (Exception)
        {
            return null;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
