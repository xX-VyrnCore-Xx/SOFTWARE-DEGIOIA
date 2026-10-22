namespace PresenzeXmlConverter;

/// <summary>Quali canali usare per un avviso: il cartellino nella finestra, il lampeggio nella barra, il fumetto di Windows.</summary>
internal readonly record struct NotificationPlan(bool InApp, bool Flash, bool Balloon)
{
    public static NotificationPlan None { get; } = new(false, false, false);
}

/// <summary>
/// Le regole delle notifiche, senza finestre (la finestra classica e la nuova le usano uguali): con la finestra in uso basta
/// il cartellino; altrimenti, se l'avviso lo chiede, si aggiungono il lampeggio nella barra e il fumetto di Windows (se attivo).
/// </summary>
internal static class NotificationPolicy
{
    public static NotificationPlan Decide(
        bool windowVisible,
        bool windowMinimized,
        bool windowInUse,
        bool inAppEnabled,
        bool nativeEnabled,
        bool systemRequested)
    {
        var inApp = windowVisible && !windowMinimized && inAppEnabled;
        if (!systemRequested || windowInUse)
        {
            return new NotificationPlan(inApp, false, false);
        }
        return new NotificationPlan(inApp, true, nativeEnabled);
    }

    /// <summary>«Prova le notifiche»: tutto subito, senza badare a dov'è la finestra né alle scelte (la prova serve a vedere cosa funziona).</summary>
    public static NotificationPlan Test(bool inApp, bool system) => new(inApp, false, system);
}

/// <summary>Cosa fa la X della finestra (impostazione «close-behavior»: vuoto = chiede, «tray», «exit»).</summary>
internal enum CloseAction
{
    Ask,
    MinimizeToTray,
    Exit
}

internal static class CloseBehaviorPolicy
{
    public const string Setting = "close-behavior";

    /// <summary>Cosa fare alla chiusura: la scelta ricordata, oppure chiedere. Una chiusura non dell'utente (o forzata) esce sempre.</summary>
    public static CloseAction Decide(string? remembered, bool userClosing, bool forceClose)
    {
        if (!userClosing || forceClose)
        {
            return CloseAction.Exit;
        }
        return remembered switch
        {
            "tray" => CloseAction.MinimizeToTray,
            "exit" => CloseAction.Exit,
            _ => CloseAction.Ask
        };
    }

    /// <summary>Il valore da ricordare per una scelta («tray» o «exit»); vuoto se non va ricordata.</summary>
    public static string ToSetting(CloseAction chosen, bool remember) =>
        remember ? chosen switch { CloseAction.MinimizeToTray => "tray", CloseAction.Exit => "exit", _ => string.Empty } : string.Empty;
}

/// <summary>Le due scelte dell'utente sulle notifiche (Impostazioni → Notifiche): cartellini nella finestra e fumetti di Windows.</summary>
internal static class NotificationSettings
{
    public static bool NativeEnabled(LocalDatabase database) => Enabled(database, SettingsService.NativeNotificationsSetting);

    public static bool InAppEnabled(LocalDatabase database) => Enabled(database, SettingsService.InAppNotificationsSetting);

    private static bool Enabled(LocalDatabase database, string key)
    {
        try
        {
            return !string.Equals(database.GetSetting(key), "false", StringComparison.OrdinalIgnoreCase);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // Senza memoria della scelta si avvisa comunque.
            return true;
        }
    }
}
