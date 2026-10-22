using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;
using Brush = System.Windows.Media.Brush;
using Orientation = System.Windows.Controls.Orientation;

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
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Application = System.Windows.Application;

/// <summary>
/// Finestra principale della nuova interfaccia: a sinistra la barra laterale con il marchio e le sezioni,
/// a destra la pagina scelta. Il cambio di pagina è una dissolvenza con un piccolo scorrimento.
/// </summary>
internal static class ShellAlign
{
    public static T AlignTop<T>(this T element)
        where T : FrameworkElement
    {
        element.VerticalAlignment = VerticalAlignment.Top;
        return element;
    }
}

internal sealed class ShellWindow : Window
{
    private static readonly (string Title, Glyph Glyph)[] Sections =
    [
        ("Dashboard", Glyph.Dashboard),
        ("Conversione", Glyph.Transfer),
        ("Aziende", Glyph.Building),
        ("Codici", Glyph.Users),
        ("Storico", Glyph.Clock),
        ("Registro", Glyph.List),
        ("Impostazioni", Glyph.Gear)
    ];

    private NavRail _nav = new();
    private ContentControl _host = new() { Focusable = false };
    private readonly Grid _layers = new();
    private readonly string _version = typeof(ShellWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    private UpdateChip _chip = null!;
    private readonly UIElement?[] _pages = new UIElement?[Sections.Length];

    private readonly ConversionService _service;
    private ConversionView? _conversion;

    private readonly Func<IConversionPrompts>? _prompts;

    public ShellWindow(ConversionService service, Func<IConversionPrompts>? prompts = null)
    {
        _service = service;
        _prompts = prompts;
        Title = $"Vyrn Presence {_version}";
        MinWidth = 1000;
        MinHeight = 680;
        Width = 1280;
        Height = 800;
        FontFamily = Ui.Font;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TaskbarItemInfo = new System.Windows.Shell.TaskbarItemInfo();

        BuildShell();
        Content = _layers;
        Notifier = new WpfNotifier(this, _toasts, service.Database);

        Loaded += (_, _) =>
        {
            _nav.Select(0);
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
        };
        Closed += (_, _) => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
    }

    /// <summary>Costruisce (o ricostruisce, dopo un cambio di tema) la barra laterale, la barra in alto con l'icona degli aggiornamenti e l'area delle pagine.</summary>
    private void BuildShell()
    {
        Background = Ui.Brush("Brush.Canvas");
        Foreground = Ui.Brush("Brush.Ink");
        _nav = new NavRail();
        _host = new ContentControl { Focusable = false };
        Array.Clear(_pages);

        foreach (var (title, glyph) in Sections)
        {
            _nav.AddItem(title, glyph);
        }
        _nav.SelectionChanged += (_, _) => ShowPage(_nav.SelectedIndex);

        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(22, 26, 18, 22) };
        brand.Children.Add(new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(10),
            Background = Ui.Brush("Brush.Brand"),
            Child = Icons.Create(Glyph.Check, System.Windows.Media.Brushes.White, 18, 2.2)
        });
        var names = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(Ui.Text("Vyrn Presence", 15, FontWeights.SemiBold));
        names.Children.Add(Ui.Text("Presenze per Zucchetti", 12, ink: "Brush.Muted"));
        brand.Children.Add(names);

        var sidebar = new Grid { Background = Ui.Brush("Brush.Sidebar") };
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebar.Children.Add(brand.At(0, 0));
        sidebar.Children.Add(_nav.WithMargin(new Thickness(12, 0, 12, 0)).At(1, 0));

        var bottom = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        bottom.Children.Add(BuildUpdateBanner());
        sidebar.Children.Add(bottom.At(2, 0));

        _chip = new UpdateChip(_version);
        _chip.Clicked += async (_, _) => await OnChipClickedAsync();
        var area = new Grid();
        area.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        area.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        area.Children.Add(_chip.WithMargin(new Thickness(28, 16, 28, 0)).At(0, 0));
        area.Children.Add(_host.WithMargin(new Thickness(28, 12, 28, 0)).At(1, 0));

        var root = Ui.Columns(Ui.Px(232), Ui.Star());
        root.Children.Add(sidebar.At(0, 0));
        root.Children.Add(new Border
        {
            BorderBrush = Ui.Brush("Brush.Divider"),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = area
        }.At(0, 1));
        _layers.Children.Clear();
        _layers.Children.Add(root);
        _layers.Children.Add(_toasts);
        RefreshUpdateUi();
    }

    /// <summary>
    /// Applica subito il tema scelto nelle Impostazioni (nessun riavvio): ricarica la tavolozza, ricostruisce la finestra
    /// e riporta l'utente dov'era, con il lavoro in corso (file importato, anteprima) intatto.
    /// </summary>
    internal void ApplyTheme()
    {
        var section = Math.Max(0, _nav.SelectedIndex);
        var settingsTab = (_pages[SettingsSection] as SettingsView)?.CurrentTab ?? 0;
        var previous = _conversion;
        ThemePreference.ApplyAtStartup();
        var merged = Application.Current.Resources.MergedDictionaries;
        var old = merged.FirstOrDefault(dictionary => dictionary.Contains("Brush.Ink"));
        var fresh = UiStyles.Create(UiPalette.Current);
        if (old is null)
        {
            merged.Add(fresh);
        }
        else
        {
            merged[merged.IndexOf(old)] = fresh;
        }
        _conversion = null;
        BuildShell();
        if (previous is not null)
        {
            _conversion = new ConversionView(_service, () => this, _prompts, Notifier);
            _conversion.RestoreFrom(previous);
        }
        _nav.Select(section);
        if (section == SettingsSection)
        {
            ShowSettingsTab(settingsTab);
        }
    }

    private void OnSystemPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category != Microsoft.Win32.UserPreferenceCategory.General)
        {
            return;
        }
        // Con «Come Windows» il tema segue il sistema anche a programma aperto.
        Dispatcher.BeginInvoke(() =>
        {
            if (ThemePreference.Load() == ThemeMode.System && ThemePreference.SystemIsDark() != Theme.IsDark)
            {
                ApplyTheme();
            }
        });
    }

    private async Task OnChipClickedAsync()
    {
        if (Updates is not { } updates)
        {
            return;
        }
        if (updates.Pending is not null)
        {
            await updates.InstallPendingAsync();
            return;
        }
        await updates.CheckAsync(userInitiated: true);
    }

    /// <summary>Aggiorna in tempo reale l'icona in alto a destra, il riquadro della barra laterale e l'avanzamento nella barra delle applicazioni.</summary>
    private void RefreshUpdateUi()
    {
        ShowPendingUpdate();
        var info = TaskbarItemInfo;
        if (Updates is not { } updates)
        {
            _chip.Show(UpdateChipState.UpToDate, "Aggiornato", 0);
            return;
        }
        if (updates.Progress is { } progress)
        {
            var downloading = progress.Step == UpdateStep.Download;
            _chip.Show(
                UpdateChipState.Downloading,
                downloading ? $"Download {progress.Percent}%" : UpdateSteps.Label(progress.Step),
                downloading ? progress.Percent : 100);
            if (info is not null)
            {
                info.ProgressState = downloading ? System.Windows.Shell.TaskbarItemProgressState.Normal : System.Windows.Shell.TaskbarItemProgressState.Indeterminate;
                info.ProgressValue = progress.Percent / 100.0;
            }
            return;
        }
        if (info is not null)
        {
            info.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
        }
        if (updates.IsChecking)
        {
            _chip.Show(UpdateChipState.Checking, "Controllo in corso…", 0);
        }
        else if (updates.Pending is { } pending)
        {
            _chip.Show(UpdateChipState.Available, $"Nuova versione {pending.Version.ToString(3)}", 0);
        }
        else if (updates.StatusIsWarning)
        {
            _chip.Show(UpdateChipState.Problem, "Controllo non riuscito", 0);
        }
        else
        {
            _chip.Show(UpdateChipState.UpToDate, "Aggiornato", 0);
        }
    }

    /// <summary>Lo stato dell'icona degli aggiornamenti (per i test).</summary>
    internal (UpdateChipState State, string Text, int Percent, bool ButtonEnabled) ChipSnapshot => _chip.Snapshot;

    private Border _updateBanner = new() { Visibility = Visibility.Collapsed };
    private TextBlock _updateBannerText = Ui.Text(string.Empty, 13, FontWeights.SemiBold, "Brush.BrandInk", wrap: true);

    /// <summary>Il ciclo degli aggiornamenti (null nei test e quando l'avvio non l'ha creato).</summary>
    internal UpdateController? Updates { get; private set; }

    /// <summary>Un riquadro in fondo alla barra laterale: dice che c'è una versione nuova e la installa con un clic.</summary>
    private Border BuildUpdateBanner()
    {
        _updateBanner = new Border { Visibility = Visibility.Collapsed };
        _updateBannerText = Ui.Text(string.Empty, 13, FontWeights.SemiBold, "Brush.BrandInk", wrap: true);
        var button = new Button { Content = "Aggiorna", Style = Ui.Style("Primary"), Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += async (_, _) =>
        {
            if (Updates is { } updates)
            {
                await updates.InstallPendingAsync();
            }
        };
        var inner = new StackPanel();
        inner.Children.Add(_updateBannerText);
        inner.Children.Add(button);
        _updateBanner.Background = Ui.Brush("Brush.BrandSoft");
        _updateBanner.CornerRadius = new CornerRadius(12);
        _updateBanner.Padding = new Thickness(14, 12, 14, 12);
        _updateBanner.Margin = new Thickness(12, 0, 12, 4);
        _updateBanner.Child = inner;
        return _updateBanner;
    }

    /// <summary>Dà alla finestra il ciclo degli aggiornamenti: banner nella barra laterale, comandi del tray, controllo al ritorno alla finestra.</summary>
    internal void AttachUpdates(UpdateController updates)
    {
        Updates = updates;
        updates.PendingChanged += RefreshUpdateUi;
        updates.ProgressChanged += () => Dispatcher.BeginInvoke(RefreshUpdateUi);
        updates.StatusChanged += () =>
        {
            RefreshUpdateUi();
            if (_pages[0] is DashboardView dashboard)
            {
                dashboard.ShowUpdates(updates.StatusText, updates.StatusIsWarning);
            }
        };
        updates.ExitRequested += () => Dispatcher.BeginInvoke(ExitApplication);
        if (Tray is { } tray)
        {
            tray.CheckUpdatesRequested += () => Dispatcher.BeginInvoke(async () =>
            {
                RestoreFromTray();
                await updates.CheckAsync(userInitiated: true);
            });
            tray.InstallUpdateRequested += () => Dispatcher.BeginInvoke(async () =>
            {
                RestoreFromTray();
                await updates.InstallPendingAsync();
            });
        }
        // Tornando alla finestra dopo un po' si controlla subito se c'è una versione nuova.
        Activated += (_, _) => updates.RequestCheck(staleOnly: true);
        RefreshUpdateUi();
    }

    /// <summary>Apre la scheda indicata delle Impostazioni (1 = Aggiornamenti).</summary>
    internal void ShowSettingsTab(int tab)
    {
        Navigate(SettingsSection);
        (PageAt(SettingsSection) as SettingsView)?.ShowTab(tab);
    }

    private DashboardView NewDashboard()
    {
        var dashboard = new DashboardView(Navigate, path => _ = ImportAsync(path));
        if (Updates is { } updates)
        {
            dashboard.ShowUpdates(updates.StatusText, updates.StatusIsWarning);
        }
        return dashboard;
    }

    private void ShowPendingUpdate()
    {
        var pending = Updates?.Pending;
        _updateBanner.Visibility = pending is null ? Visibility.Collapsed : Visibility.Visible;
        _updateBannerText.Text = pending is null ? string.Empty : $"Aggiornamento {pending.Version.ToString(3)} disponibile";
    }

    /// <summary>Il testo e la visibilità del riquadro di aggiornamento (per i test).</summary>
    internal (bool Visible, string Text) UpdateBannerState => (_updateBanner.Visibility == Visibility.Visible, _updateBannerText.Text);

    private readonly ToastLayer _toasts = new();
    private bool _forceClose;
    private bool _closedForReal;

    /// <summary>Gli avvisi dell'applicazione: cartellino nella finestra, lampeggio nella barra e fumetto di Windows.</summary>
    internal WpfNotifier Notifier { get; }

    /// <summary>L'icona nel tray (null nei test e quando l'avvio non l'ha creata).</summary>
    internal TrayController? Tray { get; private set; }

    /// <summary>
    /// Con il ciclo di vita attivo la X della finestra chiede (o ricorda) se restare nel tray o uscire, e la chiusura vera
    /// fa la manutenzione dei dati e ferma l'applicazione. I test lasciano questa opzione spenta.
    /// </summary>
    internal bool ManageLifecycle { get; set; }

    /// <summary>Dà alla finestra la sua icona nel tray e ne collega i comandi.</summary>
    internal void AttachTray(TrayController tray)
    {
        Tray = tray;
        Notifier.Tray = tray;
        tray.KeepVisible = () => !IsVisible;
        tray.OpenRequested += () => Dispatcher.BeginInvoke(RestoreFromTray);
        tray.ExitRequested += () => Dispatcher.BeginInvoke(ExitApplication);
        tray.SettingsRequested += () => Dispatcher.BeginInvoke(() =>
        {
            RestoreFromTray();
            Navigate(SettingsSection);
        });
        tray.NewConversionRequested += () => Dispatcher.BeginInvoke(() =>
        {
            RestoreFromTray();
            Navigate(ConversionSection);
        });
        tray.HistoryRequested += () => Dispatcher.BeginInvoke(() =>
        {
            RestoreFromTray();
            Navigate(HistorySection);
        });
        tray.Message += text => Dispatcher.BeginInvoke(() => Notifier.Notify(ToastKind.Info, "Vyrn Presence", text));
    }

    /// <summary>I cartellini visibili (per i test).</summary>
    internal IReadOnlyList<(ToastKind Kind, string Title, string Message)> VisibleToasts => _toasts.Visible;

    /// <summary>Chiude i cartellini (per i test).</summary>
    internal void ClearToasts() => _toasts.Clear();

    /// <summary>La sezione della conversione (per il tray).</summary>
    internal static int ConversionSection => 1;

    /// <summary>La sezione dello Storico (per il tray).</summary>
    internal static int HistorySection => 4;

    /// <summary>La sezione delle Impostazioni (l'ultima della barra laterale).</summary>
    internal static int SettingsSection => Sections.Length - 1;

    /// <summary>Riporta la finestra davanti (dal tray, da ridotta a icona o da un secondo avvio del programma).</summary>
    internal void RestoreFromTray()
    {
        if (!IsVisible)
        {
            Show();
        }
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Topmost = true;
        Topmost = false;
        if (Tray is { } tray && tray.Visible)
        {
            tray.Visible = false;
        }
    }

    /// <summary>Nasconde la finestra e lascia il programma in background con l'icona nel tray.</summary>
    internal void MinimizeToTray()
    {
        if (Tray is null)
        {
            WindowState = WindowState.Minimized;
            return;
        }
        Hide();
        Tray.Visible = true;
        Tray.ShowBalloon(
            "Vyrn Presence in background",
            "Il programma resta attivo in tray: riceverai una notifica quando una conversione termina.",
            milliseconds: 4000);
    }

    /// <summary>Chiude il programma per davvero (non nel tray).</summary>
    internal void ExitApplication()
    {
        _forceClose = true;
        try
        {
            Close();
        }
        finally
        {
            if (!_closedForReal && ManageLifecycle)
            {
                FinishClosing();
            }
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (ManageLifecycle && !_forceClose)
        {
            var action = CloseBehaviorPolicy.Decide(ReadCloseSetting(), userClosing: true, forceClose: false);
            if (action == CloseAction.Ask)
            {
                var choice = CloseChoiceWindow.Ask(this, out var remember);
                if (choice is null)
                {
                    e.Cancel = true;
                    return;
                }
                action = choice.Value;
                var remembered = CloseBehaviorPolicy.ToSetting(action, remember);
                if (remembered.Length > 0)
                {
                    try
                    {
                        Notifier.Database.SaveSetting(CloseBehaviorPolicy.Setting, remembered);
                    }
                    catch (Microsoft.Data.Sqlite.SqliteException)
                    {
                        // La scelta vale per questa volta.
                    }
                }
            }
            if (action == CloseAction.MinimizeToTray)
            {
                e.Cancel = true;
                MinimizeToTray();
                return;
            }
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (ManageLifecycle)
        {
            FinishClosing();
        }
    }

    private string? ReadCloseSetting()
    {
        try
        {
            return Notifier.Database.GetSetting(CloseBehaviorPolicy.Setting);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            return null;
        }
    }

    /// <summary>Chiusura vera: operazioni «di cortesia» (copie di sicurezza, icona del tray) che non devono mai bloccare l'uscita.</summary>
    private void FinishClosing()
    {
        if (_closedForReal)
        {
            return;
        }
        _closedForReal = true;
        Updates?.Stop();
        try
        {
            _toasts.Clear();
            Notifier.Database.MaintainOnExit();
            CodesAutoBackup.Run(Notifier.Database, DateTime.Now);
        }
        catch (Exception)
        {
            // Salvataggio best-effort: niente deve impedire la chiusura.
        }
        try
        {
            Tray?.Dispose();
        }
        catch (Exception)
        {
        }
        Application.Current?.Shutdown();
    }

    /// <summary>Un'app che si chiude con la sessione di Windows (spegnimento, disconnessione) non deve restare nel tray.</summary>
    internal void ForceCloseOnSessionEnd() => _forceClose = true;

    /// <summary>Apre la schermata di conversione e importa il foglio (se il percorso è nullo ne chiede uno).</summary>
    public async Task ImportAsync(string? path)
    {
        Navigate(1);
        var view = _conversion ??= new ConversionView(_service, () => this, _prompts, Notifier);
        await view.ImportAsync(path);
    }

    internal ConversionView? Conversion => _conversion;

    /// <summary>La pagina già creata di una sezione (per i test).</summary>
    internal UIElement? PageAt(int index) => _pages[index];

    public int SectionCount => Sections.Length;

    public int CurrentSection => _nav.SelectedIndex;

    public string SectionTitle(int index) => Sections[index].Title;

    /// <summary>Mostra una sezione, creandone la pagina alla prima visita.</summary>
    public void Navigate(int index)
    {
        if (index == _nav.SelectedIndex)
        {
            ShowPage(index);
            return;
        }
        _nav.Select(index);
    }

    private void ShowPage(int index)
    {
        if (index < 0 || index >= Sections.Length)
        {
            return;
        }
        _pages[index] ??= CreatePage(index);
        _host.Content = _pages[index];
        (_pages[index] as IRefreshablePage)?.OnShown();
        if (_pages[index] is FrameworkElement element)
        {
            UiMotion.FadeIn(element);
        }
    }

    private UIElement CreatePage(int index) => index switch
    {
        0 => NewDashboard(),
        1 => _conversion ??= new ConversionView(_service, () => this, _prompts, Notifier),
        2 => new CompaniesView(_service, () => this),
        3 => new CodesView(_service, () => this),
        4 => new HistoryView(_service, () => this),
        5 => new ActivityView(_service, () => this),
        6 => new SettingsView(_service, () => this, () => Notifier, () => Updates),
        _ => new PageScaffold(
            Sections[index].Title,
            "Questa sezione arriva nella prossima versione.",
            Ui.Card(Ui.Text("In arrivo", 16, FontWeights.SemiBold), new Thickness(28)).WithMargin(new Thickness(0, 0, 0, 24)).AlignTop())
    };
}
