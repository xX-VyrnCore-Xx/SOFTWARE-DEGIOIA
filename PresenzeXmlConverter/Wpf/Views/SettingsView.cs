using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

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
using PasswordBox = System.Windows.Controls.PasswordBox;
using CheckBox = System.Windows.Controls.CheckBox;
using ScrollViewer = System.Windows.Controls.ScrollViewer;

/// <summary>
/// La sezione «Impostazioni»: cinque schede (Conversione, Aggiornamenti, Formato XML, Notifiche, Generale) con gli stessi
/// valori della finestra classica, controllati e salvati da <see cref="SettingsService"/>. Il token degli aggiornamenti si
/// verifica e si salva subito, senza aspettare «Salva impostazioni».
/// </summary>
internal sealed class SettingsView : Grid, IRefreshablePage
{
    private static readonly string[] TabTitles = ["Conversione", "Aggiornamenti", "Formato XML", "Notifiche", "Generale"];

    private readonly SettingsService _service;
    private readonly Func<Window?> _owner;
    private readonly PillBar _tabs = new(TabTitles);
    private readonly ContentControl _page = new() { Focusable = false };
    private readonly UIElement[] _pages;

    private readonly TextBox _hours = new() { Tag = "es. 8  oppure  7,5", MaxLength = 5 };
    private readonly CheckBox _saturdays = Toggle("Il sabato è sempre lavorativo (le ore vengono distribuite anche di sabato)");
    private readonly CheckBox _autoDetect = Toggle("Rileva in automatico sabati e ore dalle date (consigliato)");
    private readonly CheckBox _fillPresence = Toggle("Registra la presenza (01) nei giorni lavorativi senza assenze");
    private readonly TextBox _holidays = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 110, VerticalContentAlignment = VerticalAlignment.Top, Padding = new Thickness(12, 8, 12, 8) };

    private readonly CheckBox _autoUpdate = Toggle("Cerca aggiornamenti automaticamente all'avvio e durante l'uso");
    private readonly CheckBox _startWithWindows = Toggle("Avvia con Windows nel tray: gli aggiornamenti arrivano subito, anche a programma chiuso");
    private readonly PasswordBox _token = new() { MaxLength = 400 };
    private readonly TextBlock _tokenStatus = Ui.Text(string.Empty, 13, ink: "Brush.Muted", wrap: true);
    private readonly Button _verifyToken = new() { Content = "Verifica e salva token" };
    private readonly Button _clearToken = new() { Content = "Rimuovi token" };

    private readonly TextBox _xmlRoot = new() { Tag = "es. Dipendenti", MaxLength = 64 };
    private readonly TextBox _xmlNamespace = new() { Tag = "es. http://www.esempio.it/schema", MaxLength = 300 };
    private readonly CheckBox _xmlPerNode = Toggle("Ogni movimento è un nodo «Movimenti» a sé (senza il nodo «Movimento» dentro)");
    private readonly TextBlock _xmlStatus = Ui.Text("Con un file XML che Zucchetti accetta (anche di un altro programma) la forma si copia senza indovinare.", 13, ink: "Brush.Muted", wrap: true);

    private readonly CheckBox _inApp = Toggle("Mostra gli avvisi dentro la finestra");
    private readonly CheckBox _system = Toggle("Mostra le notifiche di Windows quando il programma è in background");

    private readonly PillBar _close = new(["Chiedi ogni volta", "Resta in background (tray)", "Esci dal programma"]);
    private readonly PillBar _theme = new(["Chiaro", "Scuro", "Come Windows", "Vetro"]);
    private readonly TextBlock _general = Ui.Text(string.Empty, 13, ink: "Brush.Muted", wrap: true);
    private readonly TextBlock _backupStatus = Ui.Text(string.Empty, 13, ink: "Brush.Muted", wrap: true);

    private readonly TextBlock _message = Ui.Text(string.Empty, 13.5, FontWeights.SemiBold, "Brush.Muted", wrap: true);
    private readonly Button _save = new() { Content = "Salva impostazioni" };

    private static readonly string?[] CloseTags = [string.Empty, "tray", "exit"];
    private static readonly ThemeMode[] ThemeModes = [ThemeMode.Light, ThemeMode.Dark, ThemeMode.System, ThemeMode.Glass];

    public SettingsView(ConversionService conversion, Func<Window?> owner)
    {
        _service = new SettingsService(conversion);
        _owner = owner;
        _pages = [BuildConversion(), BuildUpdates(), BuildXml(), BuildNotifications(), BuildGeneral()];
        _save.Style = Ui.Style("Primary");
        _verifyToken.Style = Ui.Style("Primary");

        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Children.Add(new PageScaffold("Impostazioni", "Regole di conversione, aggiornamenti, formato dell'XML, notifiche e dati.", BuildBody()));

        _tabs.SelectionChanged += (_, _) => ShowTab(_tabs.SelectedIndex);
        _save.Click += (_, _) => Save();
        _verifyToken.Click += async (_, _) => await VerifyTokenAsync();
        _clearToken.Click += (_, _) => ClearToken();
        ShowTab(0);
    }

    public void OnShown() => Fill(_service.Load());

    /// <summary>Apre la scheda indicata (0 = Conversione … 4 = Generale).</summary>
    public void ShowTab(int index)
    {
        if (index < 0 || index >= _pages.Length)
        {
            return;
        }
        _tabs.Select(index, raise: false);
        _page.Content = _pages[index];
    }

    private UIElement BuildBody()
    {
        var card = new Border
        {
            Background = Ui.Brush("Brush.Surface"),
            BorderBrush = Ui.Brush("Brush.Divider"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _page
            }
        };
        var footer = new Grid { Margin = new Thickness(0, 14, 0, 18) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.Children.Add(_save.At(0, 0));
        _message.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(_message.WithMargin(new Thickness(14, 0, 0, 0)).At(0, 1));
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.Children.Add(_tabs.At(0, 0));
        body.Children.Add(card.At(1, 0));
        body.Children.Add(footer.At(2, 0));
        return body;
    }

    // ---- Schede ----------------------------------------------------------------------------------------------

    private UIElement BuildConversion() => Column(
        Hint("Come vengono distribuite le ore della richiesta sui giorni lavorativi."),
        Label($"Ore di una giornata standard (in {_service.ReferenceYear} la durata della richiesta viene divisa sui giorni lavorativi)"),
        _hours,
        _saturdays,
        _autoDetect,
        _fillPresence,
        Label("Chiusure aziendali aggiuntive (oltre alle festività nazionali): una data per riga o separate da virgola — es. 02/06, 24/12"),
        _holidays,
        Note("Le festività nazionali e le domeniche sono sempre escluse. Un giorno di chiusura qui indicato non riceve ore."));

    private UIElement BuildUpdates()
    {
        _tokenStatus.Text = _service.TokenStatus();
        return Column(
            Hint("Le Release del repository privato si leggono con un token di sola lettura, già incluso nelle versioni ufficiali."),
            _autoUpdate,
            _startWithWindows,
            Label("Token personale (facoltativo, se quello incluso è scaduto). Fine-grained, permesso Contents → Read-only."),
            _token,
            Buttons(_verifyToken, _clearToken),
            _tokenStatus.WithMargin(new Thickness(0, 6, 0, 0)),
            Note("Il token serve solo a leggere le Release. Se uno non vale, l'app prova gli altri (personale, file, incluso)."),
            Note(ExecutableSignature.DescribeCurrentExecutable()));
    }

    private UIElement BuildXml()
    {
        var detect = new Button { Content = "Leggi da un XML di esempio…" };
        detect.Click += (_, _) => DetectFormat();
        return Column(
            Hint("Impostazioni avanzate: servono solo se Zucchetti respinge il file con «cvc-elt.1: impossibile trovare la dichiarazione dell'elemento…»."),
            Label("Nome dell'elemento radice del file XML (la prima riga dopo <?xml …?>)"),
            _xmlRoot,
            Label("Spazio dei nomi (xmlns) dell'elemento radice: lascia vuoto se il file non ne ha"),
            _xmlNamespace,
            _xmlPerNode.WithMargin(new Thickness(0, 10, 0, 0)),
            Buttons(detect),
            _xmlStatus.WithMargin(new Thickness(0, 6, 0, 0)));
    }

    private UIElement BuildNotifications()
    {
        var windows = new Button { Content = "Impostazioni di Windows" };
        windows.Click += (_, _) => OpenWindowsNotificationSettings();
        return Column(
            Hint("Avvisi per conversione completata, file salvato e aggiornamenti. Quelli di Windows arrivano solo quando la finestra non è in primo piano."),
            _inApp,
            _system,
            Buttons(windows),
            Note("Se le notifiche di Windows non compaiono, controlla Assistente focus e le notifiche di «Vyrn Presence» nelle impostazioni di Windows."));
    }

    private UIElement BuildGeneral()
    {
        var openData = new Button { Content = "Apri la cartella dei dati" };
        openData.Click += (_, _) => OpenFolder(_service.BuildGeneralInfo().DataFolder);
        var backup = new Button { Content = "Backup dei dati ora", Style = Ui.Style("Primary") };
        backup.Click += (_, _) => BackupNow();
        var codes = new Button { Content = "Copie dei codici" };
        codes.Click += (_, _) => OpenFolder(_service.BuildGeneralInfo().CodesBackupFolder);
        return Column(
            Hint("Cosa fa il programma quando chiudi la finestra, dove tiene i dati e come sono protetti."),
            Label("Quando chiudi la finestra con la X (in background nel tray ricevi gli avvisi degli aggiornamenti)"),
            _close,
            Label("Aspetto dell'app (la scelta si applica al prossimo avvio: chiudi e riapri il programma)"),
            _theme,
            Label("Dati: tutto resta sul tuo computer, nella cartella dei dati del tuo profilo Windows."),
            _general,
            Buttons(openData, backup, codes),
            _backupStatus.WithMargin(new Thickness(0, 6, 0, 0)),
            Note("Il token degli aggiornamenti è cifrato con il tuo utente Windows. " + ExecutableSignature.DescribeCurrentExecutable()));
    }

    // ---- Costruzione -----------------------------------------------------------------------------------------

    private static CheckBox Toggle(string text) => new() { Content = text, Style = Ui.Style("Toggle") };

    private static StackPanel Column(params UIElement[] children)
    {
        var column = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
        foreach (var child in children)
        {
            column.Children.Add(child);
            if (child is TextBox or PasswordBox)
            {
                ((FrameworkElement)child).Margin = new Thickness(0, 4, 0, 6);
            }
        }
        return column;
    }

    private static TextBlock Hint(string text) => Ui.Text(text, 13.5, ink: "Brush.Muted", wrap: true).WithMargin(new Thickness(0, 0, 0, 6));

    private static TextBlock Label(string text) => Ui.Text(text, 13.5, FontWeights.SemiBold, "Brush.InkSecondary", wrap: true).WithMargin(new Thickness(0, 14, 0, 0));

    private static TextBlock Note(string text) => Ui.Text(text, 12.5, ink: "Brush.Faint", wrap: true).WithMargin(new Thickness(0, 16, 0, 0));

    private static WrapPanel Buttons(params Button[] buttons)
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        foreach (var button in buttons)
        {
            button.Margin = new Thickness(0, 0, 8, 6);
            panel.Children.Add(button);
        }
        return panel;
    }

    // ---- Valori ----------------------------------------------------------------------------------------------

    private void Fill(SettingsModel model)
    {
        _hours.Text = model.HoursText;
        _saturdays.IsChecked = model.IncludeSaturdays;
        _autoDetect.IsChecked = model.AutoDetect;
        _fillPresence.IsChecked = model.FillPresence;
        _holidays.Text = model.HolidaysText;
        _autoUpdate.IsChecked = model.AutoUpdate;
        _startWithWindows.IsChecked = model.StartWithWindows;
        _token.Password = _service.CurrentToken();
        _xmlRoot.Text = model.XmlRoot;
        _xmlNamespace.Text = model.XmlNamespace;
        _xmlPerNode.IsChecked = model.XmlPerNode;
        _inApp.IsChecked = model.InAppNotifications;
        _system.IsChecked = model.SystemNotifications;
        _close.Select(Math.Max(0, Array.IndexOf(CloseTags, model.CloseBehavior)), raise: false);
        _theme.Select(Math.Max(0, Array.IndexOf(ThemeModes, model.Theme)), raise: false);
        var info = _service.BuildGeneralInfo();
        _general.Text = info.Describe();
        _message.Text = string.Empty;
    }

    private SettingsModel Read() => new(
        _hours.Text,
        _saturdays.IsChecked == true,
        _autoDetect.IsChecked == true,
        _fillPresence.IsChecked == true,
        _holidays.Text,
        _autoUpdate.IsChecked == true,
        _startWithWindows.IsChecked == true,
        _inApp.IsChecked == true,
        _system.IsChecked == true,
        CloseTags[Math.Clamp(_close.SelectedIndex, 0, CloseTags.Length - 1)] ?? string.Empty,
        ThemeModes[Math.Clamp(_theme.SelectedIndex, 0, ThemeModes.Length - 1)],
        _xmlRoot.Text,
        _xmlNamespace.Text,
        _xmlPerNode.IsChecked == true);

    /// <summary>Le scelte della pagina, per i test (con la stessa forma che si salva).</summary>
    internal SettingsModel Current => Read();

    private void Save()
    {
        var result = _service.Save(Read());
        _message.Foreground = Ui.Brush(result.Succeeded ? "Brush.SuccessInk" : "Brush.DangerInk");
        _message.Text = result.Error ?? result.Message;
        if (result.Succeeded && _service.TokenSaved)
        {
            _message.Text += " Token attivo: gli aggiornamenti si controllano subito.";
        }
    }

    private async Task VerifyTokenAsync()
    {
        _verifyToken.IsEnabled = false;
        SetTokenStatus("Verifica in corso su GitHub…", ok: null);
        try
        {
            var (ok, message) = await _service.VerifyTokenAsync(_token.Password);
            SetTokenStatus(message, ok);
            if (ok)
            {
                _token.Password = _service.CurrentToken();
            }
        }
        finally
        {
            _verifyToken.IsEnabled = true;
        }
    }

    private void ClearToken()
    {
        var (ok, message) = _service.ClearToken();
        if (ok)
        {
            _token.Password = string.Empty;
        }
        SetTokenStatus(message, ok);
    }

    private void SetTokenStatus(string text, bool? ok)
    {
        _tokenStatus.Text = text;
        _tokenStatus.Foreground = Ui.Brush(ok switch { true => "Brush.SuccessInk", false => "Brush.DangerInk", _ => "Brush.Muted" });
    }

    private void DetectFormat()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Scegli un file XML accettato da Zucchetti",
            Filter = "File XML (*.xml)|*.xml|Tutti i file (*.*)|*.*",
            CheckFileExists = true
        };
        dialog.ClientGuid = FileDialogs.ClientId;
        if (dialog.ShowDialog(_owner()) != true)
        {
            return;
        }
        if (!SettingsService.TryDetectFormat(dialog.FileName, out var detected, out var message))
        {
            _xmlStatus.Foreground = Ui.Brush("Brush.DangerInk");
            _xmlStatus.Text = message;
            return;
        }
        _xmlRoot.Text = detected.RootElement;
        _xmlNamespace.Text = detected.Namespace;
        _xmlPerNode.IsChecked = detected.MovementPerNode;
        _xmlStatus.Foreground = Ui.Brush("Brush.SuccessInk");
        _xmlStatus.Text = message + " Premi «Salva impostazioni» per usarla.";
    }

    private void BackupNow()
    {
        try
        {
            _backupStatus.Foreground = Ui.Brush("Brush.SuccessInk");
            _backupStatus.Text = _service.BackupNow();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _backupStatus.Foreground = Ui.Brush("Brush.DangerInk");
            _backupStatus.Text = $"Backup non riuscito: {exception.Message}";
        }
    }

    private static void OpenWindowsNotificationSettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:notifications") { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Pagina non disponibile: si apre a mano dalle impostazioni di Windows.
        }
    }

    private static void OpenFolder(string folder)
    {
        if (folder.Length == 0)
        {
            return;
        }
        try
        {
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            // Cartella non apribile: il percorso resta scritto nella scheda.
        }
    }
}
