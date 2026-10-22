using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PresenzeXmlConverter.Wpf;

using TextBlock = System.Windows.Controls.TextBlock;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Cursors = System.Windows.Input.Cursors;
using Orientation = System.Windows.Controls.Orientation;
using Brush = System.Windows.Media.Brush;
using Ellipse = System.Windows.Shapes.Ellipse;
using Point = System.Windows.Point;

/// <summary>Che cosa mostra l'icona degli aggiornamenti.</summary>
internal enum UpdateChipState
{
    UpToDate,
    Checking,
    Available,
    Downloading,
    Problem
}

/// <summary>
/// L'icona degli aggiornamenti in alto a destra: la versione installata, lo stato in tempo reale (aggiornato, controllo in corso,
/// nuova versione, download con percentuale) e un pulsante che cerca subito o, se c'è una versione nuova, la installa.
/// </summary>
internal sealed class UpdateChip : Border
{
    private readonly Ellipse _dot = new() { Width = 9, Height = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
    private readonly TextBlock _version;
    private readonly TextBlock _status = Ui.Text(string.Empty, 12.5, ink: "Brush.Muted");
    private readonly Border _button = new() { Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Cursor = Cursors.Hand, Margin = new Thickness(10, 0, 0, 0) };
    private readonly Border _bar = new() { Height = 3, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Border _barFill = new() { Height = 3, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly RotateTransform _spin = new();
    private FrameworkElement _icon;
    private UpdateChipState _state = UpdateChipState.UpToDate;
    private int _percent;

    public UpdateChip(string version)
    {
        _version = Ui.Text($"v{version}", 13, FontWeights.SemiBold);
        Background = Ui.Brush("Brush.Surface");
        BorderBrush = Ui.Brush("Brush.Divider");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(12);
        Padding = new Thickness(12, 4, 4, 4);
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Center;
        System.Windows.Automation.AutomationProperties.SetName(this, "Aggiornamenti");

        _bar.Background = Ui.Brush("Brush.Sunken");
        _bar.Child = _barFill;
        _barFill.Background = Ui.Brush("Brush.Brand");
        _bar.SizeChanged += (_, _) => ApplyBar();

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var line = new StackPanel { Orientation = Orientation.Horizontal };
        line.Children.Add(_version);
        line.Children.Add(Ui.Text("·", 13, ink: "Brush.Faint").WithMargin(new Thickness(8, 0, 8, 0)));
        line.Children.Add(_status);
        texts.Children.Add(line);
        texts.Children.Add(_bar);

        _icon = Icons.Create(Glyph.Refresh, Ui.Brush("Brush.InkSecondary"), 16, 1.8);
        _icon.RenderTransformOrigin = new Point(0.5, 0.5);
        _icon.RenderTransform = _spin;
        _button.Child = _icon;
        _button.Background = System.Windows.Media.Brushes.Transparent;
        _button.MouseEnter += (_, _) => Highlight(true);
        _button.MouseLeave += (_, _) => Highlight(false);
        _button.MouseLeftButtonUp += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
        System.Windows.Automation.AutomationProperties.SetName(_button, "Cerca aggiornamenti");

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_dot);
        row.Children.Add(texts);
        row.Children.Add(_button);
        Child = row;
        Show(UpdateChipState.UpToDate, "Aggiornato", 0);
    }

    /// <summary>Il pulsante dell'icona: cerca un aggiornamento o, se ce n'è uno pronto, lo installa.</summary>
    public event EventHandler? Clicked;

    /// <summary>Stato, testo e percentuale mostrati (per i test).</summary>
    internal (UpdateChipState State, string Text, int Percent, bool ButtonEnabled) Snapshot => (_state, _status.Text, _percent, _button.IsHitTestVisible);

    public void Show(UpdateChipState state, string text, int percent)
    {
        _state = state;
        _percent = percent;
        _status.Text = text;
        var dot = state switch
        {
            UpdateChipState.Available => "Brush.Brand",
            UpdateChipState.Downloading => "Brush.Brand",
            UpdateChipState.Checking => "Brush.Muted",
            UpdateChipState.Problem => "Brush.Warning",
            _ => "Brush.Success"
        };
        _dot.Fill = Ui.Brush(dot);
        _status.Foreground = Ui.Brush(state switch
        {
            UpdateChipState.Available or UpdateChipState.Downloading => "Brush.BrandInk",
            UpdateChipState.Problem => "Brush.WarningInk",
            _ => "Brush.Muted"
        });
        _status.FontWeight = state is UpdateChipState.Available or UpdateChipState.Downloading ? FontWeights.SemiBold : FontWeights.Normal;
        _bar.Visibility = state == UpdateChipState.Downloading ? Visibility.Visible : Visibility.Collapsed;
        ApplyBar();

        var busy = state is UpdateChipState.Checking or UpdateChipState.Downloading;
        _button.IsHitTestVisible = !busy;
        _button.Opacity = busy ? 0.55 : 1;
        _button.Cursor = busy ? Cursors.Arrow : Cursors.Hand;
        _button.ToolTip = state switch
        {
            UpdateChipState.Available => "Scarica e installa la nuova versione",
            UpdateChipState.Checking => "Controllo in corso…",
            UpdateChipState.Downloading => "Download in corso…",
            _ => "Cerca aggiornamenti"
        };
        var install = state == UpdateChipState.Available;
        SetIcon(install ? Glyph.Download : Glyph.Refresh, install ? "Brush.Brand" : "Brush.InkSecondary");
        Spin(state == UpdateChipState.Checking);
    }

    private void SetIcon(Glyph glyph, string ink)
    {
        _icon = Icons.Create(glyph, Ui.Brush(ink), 16, 1.8);
        _icon.RenderTransformOrigin = new Point(0.5, 0.5);
        _icon.RenderTransform = _spin;
        _button.Child = _icon;
    }

    private void Highlight(bool on) =>
        _button.Background = on && _button.IsHitTestVisible ? Ui.Brush("Brush.BrandSoft") : System.Windows.Media.Brushes.Transparent;

    private void Spin(bool on)
    {
        if (on && UiMotion.Enabled)
        {
            _spin.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = RepeatBehavior.Forever });
        }
        else
        {
            _spin.BeginAnimation(RotateTransform.AngleProperty, null);
            _spin.Angle = 0;
        }
    }

    private void ApplyBar() => _barFill.Width = Math.Max(0, _bar.ActualWidth * Math.Clamp(_percent, 0, 100) / 100.0);
}
