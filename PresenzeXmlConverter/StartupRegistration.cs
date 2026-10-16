using Microsoft.Win32;

namespace PresenzeXmlConverter;

/// <summary>Registra o toglie l'avvio con Windows nel registro dell'utente (nessun privilegio di amministratore).</summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>L'avvio con Windows è attivo (qualunque riga registrata con il nostro nome).</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(StartupLaunch.RegistryValueName) is string value && value.Length > 0;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>Attiva o disattiva. False se Windows ha rifiutato la scrittura.</summary>
    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null)
            {
                return false;
            }
            if (enabled)
            {
                key.SetValue(StartupLaunch.RegistryValueName, StartupLaunch.Command(Application.ExecutablePath), RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(StartupLaunch.RegistryValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>Se l'avvio è attivo ma punta altrove (programma spostato o reinstallato), lo riallinea al percorso attuale.</summary>
    public static void KeepCurrent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            if (key?.GetValue(StartupLaunch.RegistryValueName) is string value &&
                !StartupLaunch.IsCurrent(value, Application.ExecutablePath))
            {
                Set(true);
            }
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Nessuna correzione possibile: l'avvio resta com'è.
        }
    }
}
