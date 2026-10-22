using System.Drawing;
using System.Runtime.InteropServices;
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
/// L'icona dell'applicazione nell'area di notifica: menu con testata (nome, versione e stato degli aggiornamenti), accesso rapido
/// a nuova conversione e storico, impostazioni e interruttori; fumetti di Windows (cliccandoli si apre la finestra e si esegue
/// l'azione dell'avviso); un pallino verde sull'icona e il suggerimento dicono che c'è un aggiornamento pronto.
/// </summary>
internal sealed class TrayController : IDisposable
{
    private readonly LocalDatabase _database;
    private readonly string _caption;
    private readonly NotifyIcon _icon = new();
    private readonly ToolStripMenuItem _updateItem;
    private readonly ToolStripMenuItem _statusItem;
    private readonly Icon _baseIcon;
    private readonly int _dpi;
    private Icon? _badgedIcon;
    private readonly ToolStripMenuItem _notificationsItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly DispatcherTimer _cleanup = new() { Interval = TimeSpan.FromSeconds(20) };
    private Action? _balloonAction;
    private bool _disposed;

    public TrayController(LocalDatabase database, string caption, Icon icon, int dpi)
    {
        _database = database;
        _caption = caption;
        _baseIcon = icon;
        _dpi = dpi;
        _icon.Icon = icon;
        _icon.Text = Truncate(caption);

        var menu = ThemedMenus.Create(icons: true);
        // Testata: nome e versione, e sotto lo stato degli aggiornamenti (si legge senza aprire nulla).
        var header = new ToolStripMenuItem(caption) { Enabled = false, Font = new Font(menu.Font, FontStyle.Bold) };
        menu.Items.Add(header);
        _statusItem = new ToolStripMenuItem(StatusText(null)) { Enabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        var open = ThemedMenus.Add(menu, "Apri Vyrn Presence", Glyph.Dashboard, (_, _) => OpenRequested?.Invoke(), dpi, Theme.Brand);
        open.Font = new Font(menu.Font, FontStyle.Bold);
        ThemedMenus.Add(menu, "Nuova conversione…", Glyph.Transfer, (_, _) => NewConversionRequested?.Invoke(), dpi);
        ThemedMenus.Add(menu, "Storico esportazioni", Glyph.Clock, (_, _) => HistoryRequested?.Invoke(), dpi);
        menu.Items.Add(new ToolStripSeparator());
        _updateItem = ThemedMenus.Add(menu, "Installa aggiornamento", Glyph.Download, (_, _) => InstallUpdateRequested?.Invoke(), dpi, Theme.Success);
        _updateItem.Visible = false;
        _updateItem.Font = new Font(menu.Font, FontStyle.Bold);
        ThemedMenus.Add(menu, "Cerca aggiornamenti…", Glyph.Refresh, (_, _) => CheckUpdatesRequested?.Invoke(), dpi);
        menu.Items.Add(new ToolStripSeparator());
        ThemedMenus.Add(menu, "Impostazioni…", Glyph.Gear, (_, _) => SettingsRequested?.Invoke(), dpi);
        _notificationsItem = ThemedMenus.Add(menu, NotificationsText(), Glyph.Alert, (_, _) => ToggleNotifications(), dpi);
        _startupItem = ThemedMenus.Add(menu, StartupText(), Glyph.Clock, (_, _) => ToggleStartup(), dpi);
        ThemedMenus.Add(menu, "Alla chiusura chiedi di nuovo", Glyph.Refresh, (_, _) =>
        {
            _database.SaveSetting(CloseBehaviorPolicy.Setting, string.Empty);
            Message?.Invoke("Alla chiusura della finestra il programma tornerà a chiedere come comportarsi.");
        }, dpi);
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

    public event Action? NewConversionRequested;

    public event Action? HistoryRequested;

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
        _statusItem.Text = StatusText(version);
        _icon.Text = Truncate(UpdateNotice.TrayTooltip(_caption, version));
        try
        {
            if (version is null)
            {
                _icon.Icon = _baseIcon;
            }
            else
            {
                _badgedIcon ??= TrayIconFactory.WithBadge(_baseIcon, System.Drawing.Color.FromArgb(52, 199, 89), _dpi);
                _icon.Icon = _badgedIcon;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or ExternalException or InvalidOperationException)
        {
            // Senza il pallino l'icona resta quella di sempre: l'avviso c'è comunque nel menu e nel suggerimento.
            _icon.Icon = _baseIcon;
        }
    }

    /// <summary>La riga di stato sotto il nome, nel menu.</summary>
    internal static string StatusText(Version? pending) =>
        pending is null ? "Nessun aggiornamento in sospeso" : $"Aggiornamento {pending.ToString(3)} pronto da installare";

    /// <summary>La riga di stato mostrata (per i test).</summary>
    internal string StatusLine => _statusItem.Text ?? string.Empty;

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
            _badgedIcon?.Dispose();
        }
        catch (Exception)
        {
            // Pulizia di cortesia alla chiusura.
        }
    }
}
