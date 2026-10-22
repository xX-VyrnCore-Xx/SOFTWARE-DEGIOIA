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
        ("Registro", Glyph.List)
    ];

    private readonly NavRail _nav = new();
    private readonly ContentControl _host = new() { Focusable = false };
    private readonly UIElement?[] _pages = new UIElement?[Sections.Length];

    private readonly ConversionService _service;
    private ConversionView? _conversion;

    private readonly Func<IConversionPrompts>? _prompts;

    public ShellWindow(ConversionService service, Func<IConversionPrompts>? prompts = null)
    {
        _service = service;
        _prompts = prompts;
        var version = typeof(ShellWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        Title = $"Vyrn Presence {version}";
        MinWidth = 1000;
        MinHeight = 680;
        Width = 1280;
        Height = 800;
        FontFamily = Ui.Font;
        Background = Ui.Brush("Brush.Canvas");
        Foreground = Ui.Brush("Brush.Ink");
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

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
        names.Children.Add(Ui.Text($"Versione {version}", 12, ink: "Brush.Muted"));
        brand.Children.Add(names);

        var sidebar = new Grid { Background = Ui.Brush("Brush.Sidebar") };
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebar.Children.Add(brand.At(0, 0));
        sidebar.Children.Add(_nav.WithMargin(new Thickness(12, 0, 12, 0)).At(1, 0));

        var classic = new Button { Content = "Torna all'interfaccia classica", Style = Ui.Style("Ghost"), Margin = new Thickness(12, 8, 12, 18) };
        classic.Click += (_, _) =>
        {
            UiMode.Save(UiKind.Classic);
            MessageBox.Show(this, "Alla prossima apertura Vyrn Presence userà l'interfaccia classica.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
        };
        sidebar.Children.Add(classic.At(2, 0));

        var root = Ui.Columns(Ui.Px(232), Ui.Star());
        root.Children.Add(sidebar.At(0, 0));
        root.Children.Add(new Border
        {
            BorderBrush = Ui.Brush("Brush.Divider"),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = _host.WithMargin(new Thickness(28, 28, 28, 0))
        }.At(0, 1));
        Content = root;

        Loaded += (_, _) => _nav.Select(0);
    }

    /// <summary>Apre la schermata di conversione e importa il foglio (se il percorso è nullo ne chiede uno).</summary>
    public async Task ImportAsync(string? path)
    {
        Navigate(1);
        var view = _conversion ??= new ConversionView(_service, () => this, _prompts);
        await view.ImportAsync(path);
    }

    internal ConversionView? Conversion => _conversion;

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
        0 => new DashboardView(Navigate, path => _ = ImportAsync(path)),
        1 => _conversion ??= new ConversionView(_service, () => this, _prompts),
        2 => new CompaniesView(_service, () => this),
        3 => new CodesView(_service, () => this),
        4 => new HistoryView(_service, () => this),
        5 => new ActivityView(_service, () => this),
        _ => new PageScaffold(
            Sections[index].Title,
            "Questa sezione arriva nella prossima versione dell'interfaccia: intanto usa l'interfaccia classica.",
            Ui.Card(Ui.Text("In arrivo", 16, FontWeights.SemiBold), new Thickness(28)).WithMargin(new Thickness(0, 0, 0, 24)).AlignTop())
    };
}
