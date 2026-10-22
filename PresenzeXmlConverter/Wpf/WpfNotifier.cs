using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Forms;

namespace PresenzeXmlConverter.Wpf;

using Window = System.Windows.Window;

/// <summary>
/// Gli avvisi della nuova interfaccia: sceglie i canali con <see cref="NotificationPolicy"/> (le stesse regole di quella classica)
/// e li usa: cartellino nella finestra, lampeggio nella barra delle applicazioni, fumetto di Windows dal tray.
/// </summary>
internal sealed class WpfNotifier
{
    private readonly Window _window;
    private readonly ToastLayer _toasts;

    public WpfNotifier(Window window, ToastLayer toasts, LocalDatabase database)
    {
        _window = window;
        _toasts = toasts;
        Database = database;
    }

    public LocalDatabase Database { get; }

    public TrayController? Tray { get; set; }

    /// <summary>Per i test: forza lo stato della finestra (null = lo stato vero).</summary>
    internal (bool Visible, bool Minimized, bool InUse)? WindowStateOverride { get; set; }

    /// <summary>L'utente sta guardando la finestra? Una finestra dietro le altre o ridotta a icona non conta.</summary>
    public bool IsWindowInUse => WindowStateOverride?.InUse ?? (_window.IsVisible && _window.WindowState != WindowState.Minimized && _window.IsActive);

    /// <summary>I canali che userebbe ora un avviso (per i test).</summary>
    internal NotificationPlan Plan(bool systemNotification) => NotificationPolicy.Decide(
        WindowStateOverride?.Visible ?? _window.IsVisible,
        WindowStateOverride?.Minimized ?? _window.WindowState == WindowState.Minimized,
        IsWindowInUse,
        NotificationSettings.InAppEnabled(Database),
        NotificationSettings.NativeEnabled(Database),
        systemNotification);

    /// <summary>
    /// Avvisa l'utente di un evento. Con la finestra in uso basta il cartellino; altrimenti (ridotta a icona, dietro altre finestre,
    /// nascosta nel tray) si aggiungono il lampeggio nella barra e il fumetto di Windows, se l'avviso lo chiede e l'utente li vuole.
    /// </summary>
    public void Notify(ToastKind kind, string title, string message, Action? onClick = null, bool systemNotification = false)
    {
        var plan = Plan(systemNotification);
        if (plan.InApp)
        {
            _toasts.Show(kind, title, message, onClick);
        }
        if (plan.Flash)
        {
            var handle = new WindowInteropHelper(_window).Handle;
            TaskbarFlash.Flash(handle);
        }
        if (plan.Balloon)
        {
            Tray?.ShowBalloon(
                title,
                message,
                kind switch
                {
                    ToastKind.Warning => ToolTipIcon.Warning,
                    ToastKind.Error => ToolTipIcon.Error,
                    _ => ToolTipIcon.Info
                },
                onClick);
        }
    }

    /// <summary>«Prova le notifiche» delle Impostazioni: mostra subito i canali scelti, così l'utente vede quali funzionano.</summary>
    public void Test(bool inApp, bool system)
    {
        var plan = NotificationPolicy.Test(inApp, system);
        if (plan.InApp)
        {
            _toasts.Show(ToastKind.Success, "Notifiche attive", "Così compaiono gli avvisi di Vyrn Presence dentro la finestra.");
        }
        if (plan.Balloon)
        {
            Tray?.ShowBalloon(
                "Prova notifica di Windows",
                "Se vedi questo messaggio, le notifiche di sistema funzionano.",
                force: true);
        }
    }
}
