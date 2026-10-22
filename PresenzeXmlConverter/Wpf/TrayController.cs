using System.Drawing;
using System.Windows.Forms;
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

/// <summary>
/// L'icona della nuova interfaccia nell'area di notifica: menu con le stesse voci di quella classica, fumetti di Windows
/// (cliccandoli si apre la finestra e si esegue l'azione dell'avviso) e suggerimento che dice se c'è un aggiornamento pronto.
/// Le scelte del menu si scrivono nello stesso database della finestra classica.
/// </summary>
internal sealed class TrayController : IDisposable
{
    private readonly LocalDatabase _database;
    private readonly string _caption;
    private readonly NotifyIcon _icon = new();
    private readonly ToolStripMenuItem _updateItem;
    private readonly ToolStripMenuItem _notificationsItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly DispatcherTimer _cleanup = new() { Interval = TimeSpan.FromSeconds(20) };
    private Action? _balloonAction;
    private bool _disposed;

    public TrayController(LocalDatabase database, string caption, Icon icon, int dpi)
    {
        _database = database;
        _caption = caption;
        _icon.Icon = icon;
        _icon.Text = Truncate(caption);

        var menu = ThemedMenus.Create(icons: true);
        var open = ThemedMenus.Add(menu, "Apri Vyrn Presence", Glyph.Dashboard, (_, _) => OpenRequested?.Invoke(), dpi, Theme.Brand);
        open.Font = new Font(menu.Font, FontStyle.Bold);
        _updateItem = ThemedMenus.Add(menu, "Installa aggiornamento", Glyph.Download, (_, _) => InstallUpdateRequested?.Invoke(), dpi, Theme.Success);
        _updateItem.Visible = false;
        _updateItem.Font = new Font(menu.Font, FontStyle.Bold);
        menu.Items.Add(new ToolStripSeparator());
        ThemedMenus.Add(menu, "Cerca aggiornamenti…", Glyph.Refresh, (_, _) => CheckUpdatesRequested?.Invoke(), dpi);
        ThemedMenus.Add(menu, "Impostazioni…", Glyph.Gear, (_, _) => SettingsRequested?.Invoke(), dpi);
        menu.Items.Add(new ToolStripSeparator());
        _notificationsItem = ThemedMenus.Add(menu, NotificationsText(), Glyph.Alert, (_, _) => ToggleNotifications(), dpi);
        _startupItem = ThemedMenus.Add(menu, StartupText(), Glyph.Clock, (_, _) => ToggleStartup(), dpi);
        ThemedMenus.Add(menu, "Alla chiusura chiedi di nuovo", Glyph.Refresh, (_, _) =>
        {
            _database.SaveSetting(CloseBehaviorPolicy.Setting, string.Empty);
            Message?.Invoke("Alla chiusura della finestra il programma tornerà a chiedere come comportarsi.");
        }, dpi);
        ThemedMenus.Add(menu, "Torna all'interfaccia classica", Glyph.Dashboard, (_, _) => ClassicRequested?.Invoke(), dpi);
        menu.Items.Add(new ToolStripSeparator());
        ThemedMenus.Add(menu, "Esci definitivamente", Glyph.Arrow, (_, _) => ExitRequested?.Invoke(), dpi, Theme.DangerInk);
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        // Un clic sul fumetto riporta la finestra davanti e fa ciò che l'avviso proponeva.
        _icon.BalloonTipClicked += (_, _) =>
        {
            var action = _balloonAction;
            _balloonAction = null;
            OpenRequested?.Invoke();
            action?.Invoke();
        };
        _icon.BalloonTipClosed += (_, _) => _balloonAction = null;
        _cleanup.Tick += (_, _) =>
        {
            _cleanup.Stop();
            // Con la finestra aperta l'icona serviva solo alla notifica: non resta nel tray.
            if (!_disposed && KeepVisible?.Invoke() != true)
            {
                _icon.Visible = false;
            }
        };
    }

    public event Action? OpenRequested;

    public event Action? ExitRequested;

    public event Action? CheckUpdatesRequested;

    public event Action? InstallUpdateRequested;

    public event Action? SettingsRequested;

    public event Action? ClassicRequested;

    /// <summary>Un messaggio breve per chi guarda la finestra (le scelte del menu lo usano per confermare).</summary>
    public event Action<string>? Message;

    /// <summary>Dice se l'icona deve restare nel tray (con la finestra nascosta sì, con la finestra aperta no).</summary>
    public Func<bool>? KeepVisible { get; set; }

    public bool Visible
    {
        get => _icon.Visible;
        set => _icon.Visible = value;
    }

    internal NotifyIcon Icon => _icon;

    private string NotificationsText() =>
        NotificationSettings.NativeEnabled(_database) ? "Notifiche di Windows · attive" : "Notifiche di Windows · spente";

    private static string StartupText() => StartupRegistration.IsEnabled() ? "Avvia con Windows · sì" : "Avvia con Windows · no";

    private void ToggleNotifications()
    {
        var enable = !NotificationSettings.NativeEnabled(_database);
        _database.SaveSetting(SettingsService.NativeNotificationsSetting, enable ? "true" : "false");
        _notificationsItem.Text = NotificationsText();
        Message?.Invoke(enable ? "Notifiche di Windows attivate." : "Notifiche di Windows disattivate.");
    }

    private void ToggleStartup()
    {
        var enable = !StartupRegistration.IsEnabled();
        if (!StartupRegistration.Set(enable))
        {
            Message?.Invoke("Windows non ha permesso di cambiare l'avvio automatico.");
            return;
        }
        _startupItem.Text = StartupText();
        Message?.Invoke(enable
            ? "Si avvia con Windows nel tray: gli aggiornamenti arrivano anche a programma chiuso."
            : "Avvio con Windows disattivato.");
        try
        {
            _database.AddActivity(ActivityKind.Settings, enable ? "Avvio con Windows attivato" : "Avvio con Windows disattivato");
        }
        catch (Exception exception) when (exception is Microsoft.Data.Sqlite.SqliteException or IOException)
        {
            // Il registro non è l'operazione.
        }
    }

    /// <summary>Ricorda l'aggiornamento pronto: la voce in cima al menu e il suggerimento dell'icona lo dicono anche a finestra chiusa.</summary>
    public void SetPendingUpdate(Version? version)
    {
        _updateItem.Visible = version is not null;
        _updateItem.Text = version is null ? "Installa aggiornamento" : $"Installa l'aggiornamento {version.ToString(3)}…";
        _icon.Text = Truncate(UpdateNotice.TrayTooltip(_caption, version));
    }

    /// <summary>Il testo della voce di aggiornamento (per i test).</summary>
    internal (bool Visible, string Text) UpdateItem => (_updateItem.Available, _updateItem.Text);

    /// <summary>
    /// Un fumetto di Windows. L'icona deve essere registrata prima di poterlo mostrare: se è appena apparsa si attende un istante,
    /// altrimenti la prima notifica andrebbe persa. «force» è la prova delle Impostazioni (si vede anche con le notifiche spente).
    /// </summary>
    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info, Action? onClick = null, bool force = false, int milliseconds = 6000)
    {
        if (_disposed || (!force && !NotificationSettings.NativeEnabled(_database)))
        {
            return;
        }
        _balloonAction = onClick;
        try
        {
            var registered = _icon.Visible;
            _icon.Visible = true;

            void Show()
            {
                if (_disposed)
                {
                    return;
                }
                _icon.ShowBalloonTip(milliseconds, title, text, icon);
                _cleanup.Stop();
                _cleanup.Interval = TimeSpan.FromMilliseconds(milliseconds + 15_000);
                _cleanup.Start();
            }

            if (registered)
            {
                Show();
                return;
            }
            var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            delay.Tick += (_, _) =>
            {
                delay.Stop();
                Show();
            };
            delay.Start();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or System.ComponentModel.Win32Exception)
        {
            // Una notifica mancata non deve mai disturbare il lavoro.
        }
    }

    // Il suggerimento dell'icona non può superare 127 caratteri.
    private static string Truncate(string text) => text.Length <= 120 ? text : text[..119] + "…";

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _cleanup.Stop();
        try
        {
            _icon.Visible = false;
            _icon.Dispose();
        }
        catch (Exception)
        {
            // Pulizia di cortesia alla chiusura.
        }
    }
}
