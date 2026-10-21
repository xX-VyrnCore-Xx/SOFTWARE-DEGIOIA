using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Orientation = System.Windows.Controls.Orientation;
using Ellipse = System.Windows.Shapes.Ellipse;
using ShapeRectangle = System.Windows.Shapes.Rectangle;

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

/// <summary>Tono di una riga di stato: colora l'icona e il testo del valore.</summary>
internal enum UiTone
{
    Neutral,
    Good,
    Warning,
    Bad
}

/// <summary>
/// Riga di stato del sistema: icona in un riquadro colorato, il nome sopra e lo stato sotto (nel colore
/// del suo tono). Lo stato ha tutta la larghezza che resta e va a capo con i puntini solo se non basta.
/// </summary>
internal sealed class StatusRow : Grid
{
    private readonly Border _chip;
    private readonly FrameworkElement _icon;
    private readonly TextBlock _value;

    public StatusRow(string title, Glyph glyph)
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _icon = Icons.Create(glyph, Ui.Brush("Brush.Muted"), 18, 1.6);
        _icon.HorizontalAlignment = HorizontalAlignment.Center;
        _icon.VerticalAlignment = VerticalAlignment.Center;
        _chip = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(11),
            Background = Ui.Brush("Brush.Canvas"),
            Child = _icon,
            VerticalAlignment = VerticalAlignment.Center
        };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        text.Children.Add(Ui.Text(title, 14, FontWeights.SemiBold));
        _value = Ui.Text(string.Empty, 13, ink: "Brush.Muted", trimming: TextTrimming.CharacterEllipsis);
        _value.Margin = new Thickness(0, 1, 0, 0);
        text.Children.Add(_value);
        Children.Add(_chip.At(0, 0));
        Children.Add(text.At(0, 1));
        System.Windows.Automation.AutomationProperties.SetName(this, title);
    }

    public void SetState(string value, UiTone tone)
    {
        _value.Text = value;
        var (soft, ink) = tone switch
        {
            UiTone.Good => ("Brush.SuccessSoft", "Brush.SuccessInk"),
            UiTone.Warning => ("Brush.WarningSoft", "Brush.WarningInk"),
            UiTone.Bad => ("Brush.DangerSoft", "Brush.DangerInk"),
            _ => ("Brush.Canvas", "Brush.Muted")
        };
        _chip.Background = Ui.Brush(soft);
        _value.Foreground = Ui.Brush(ink);
        ((FrameworkElement)_icon).Tag = ink;
        SetIconBrush(_icon, Ui.Brush(ink));
        System.Windows.Automation.AutomationProperties.SetHelpText(this, value);
    }

    private static void SetIconBrush(DependencyObject root, Brush brush)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is System.Windows.Shapes.Shape shape)
            {
                if (shape.Stroke is not null)
                {
                    shape.Stroke = brush;
                }
                if (shape.Fill is not null)
                {
                    shape.Fill = brush;
                }
            }
            SetIconBrush(child, brush);
        }
    }
}

/// <summary>
/// Indicatore dei tre passi del lavoro (Importa, Verifica, Esporta): cerchi collegati da una linea. I passi
/// fatti diventano verdi con la spunta, quello in corso è blu; il passaggio da uno stato all'altro è animato.
/// </summary>
internal sealed class StepperBar : Grid
{
    private static readonly string[] Names = ["Importa", "Verifica", "Esporta"];
    private readonly List<(Border Dot, TextBlock Number, FrameworkElement Check, TextBlock Label, ShapeRectangle? Line)> _steps = new();
    private int _current;

    public StepperBar()
    {
        for (var index = 0; index < Names.Length; index++)
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (index < Names.Length - 1)
            {
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
        }
        var column = 0;
        for (var index = 0; index < Names.Length; index++)
        {
            var number = Ui.Text((index + 1).ToString(), 12.5, FontWeights.SemiBold, "Brush.Muted");
            number.HorizontalAlignment = HorizontalAlignment.Center;
            number.VerticalAlignment = VerticalAlignment.Center;
            var check = Icons.Create(Glyph.Check, System.Windows.Media.Brushes.White, 14, 2.2);
            check.HorizontalAlignment = HorizontalAlignment.Center;
            check.VerticalAlignment = VerticalAlignment.Center;
            check.Visibility = Visibility.Collapsed;
            var dot = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = Ui.Mutable("Brush.Sunken"),
                Child = new Grid { Children = { number, check } }
            };
            var label = Ui.Text(Names[index], 13.5, FontWeights.Medium, "Brush.Muted");
            label.Margin = new Thickness(8, 0, 0, 0);
            label.VerticalAlignment = VerticalAlignment.Center;
            var step = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            step.Children.Add(dot);
            step.Children.Add(label);
            Children.Add(step.At(0, column++));
            ShapeRectangle? line = null;
            if (index < Names.Length - 1)
            {
                line = new ShapeRectangle
                {
                    Height = 2,
                    RadiusX = 1,
                    RadiusY = 1,
                    Fill = Ui.Mutable("Brush.Divider"),
                    Margin = new Thickness(12, 0, 12, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Children.Add(line.At(0, column++));
            }
            _steps.Add((dot, number, check, label, line));
        }
        Current = 0;
        System.Windows.Automation.AutomationProperties.SetName(this, "Avanzamento del lavoro");
    }

    /// <summary>Passo in corso: 0 Importa, 1 Verifica, 2 Esporta, 3 tutto completato.</summary>
    public int Current
    {
        get => _current;
        set
        {
            _current = Math.Clamp(value, 0, Names.Length);
            for (var index = 0; index < _steps.Count; index++)
            {
                var (dot, number, check, label, line) = _steps[index];
                var done = index < _current;
                var active = index == _current;
                var fill = done ? "Brush.Success" : active ? "Brush.Brand" : "Brush.Sunken";
                Recolor((SolidColorBrush)dot.Background, Ui.Color(fill));
                number.Foreground = active ? System.Windows.Media.Brushes.White : Ui.Brush("Brush.Muted");
                number.Visibility = done ? Visibility.Collapsed : Visibility.Visible;
                check.Visibility = done ? Visibility.Visible : Visibility.Collapsed;
                label.Foreground = Ui.Brush(done ? "Brush.SuccessInk" : active ? "Brush.BrandInk" : "Brush.Muted");
                label.FontWeight = active ? FontWeights.SemiBold : FontWeights.Medium;
                if (line is not null)
                {
                    Recolor((SolidColorBrush)line.Fill, Ui.Color(done ? "Brush.Success" : "Brush.Divider"));
                }
            }
            System.Windows.Automation.AutomationProperties.SetHelpText(
                this,
                _current >= Names.Length ? "Tutti i passi completati" : $"Passo in corso: {Names[_current]}");
        }
    }

    private static void Recolor(SolidColorBrush brush, System.Windows.Media.Color target)
    {
        if (!UiMotion.Enabled)
        {
            brush.Color = target;
            return;
        }
        brush.BeginAnimation(
            SolidColorBrush.ColorProperty,
            new System.Windows.Media.Animation.ColorAnimation(target, UiMotion.Standard) { EasingFunction = UiMotion.EaseOut });
    }
}

/// <summary>
/// Scorciatoia a riquadro verso una sezione: icona, titolo e una riga che dice a che cosa serve. Al
/// passaggio del mouse il bordo si accende e il riquadro si solleva di due punti (animazione breve).
/// </summary>
internal sealed class TileButton : Border
{
    private readonly TranslateTransform _lift = new();
    private readonly SolidColorBrush _edge = Ui.Mutable("Brush.Divider");
    private readonly Border _chip;
    private readonly FrameworkElement _icon;

    public TileButton(string title, string subtitle, Glyph glyph)
    {
        Title = title;
        Focusable = true;
        FocusVisualStyle = null;
        Cursor = Cursors.Hand;
        Background = Ui.Brush("Brush.Surface");
        BorderBrush = _edge;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(14);
        Padding = new Thickness(16);
        RenderTransform = _lift;
        System.Windows.Automation.AutomationProperties.SetName(this, title);
        System.Windows.Automation.AutomationProperties.SetHelpText(this, subtitle);

        _icon = Icons.Create(glyph, Ui.Brush("Brush.BrandInk"), 20, 1.7);
        _icon.HorizontalAlignment = HorizontalAlignment.Center;
        _icon.VerticalAlignment = VerticalAlignment.Center;
        _chip = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(12),
            Background = Ui.Brush("Brush.BrandSoft"),
            VerticalAlignment = VerticalAlignment.Top,
            Child = _icon
        };
        var text = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
        text.Children.Add(Ui.Text(title, 15, FontWeights.SemiBold));
        var sub = Ui.Text(subtitle, 13, ink: "Brush.Muted", wrap: true);
        sub.Margin = new Thickness(0, 2, 0, 0);
        text.Children.Add(sub);
        var row = Ui.Columns(GridLength.Auto, new GridLength(1, GridUnitType.Star));
        row.Children.Add(_chip.At(0, 0));
        row.Children.Add(text.At(0, 1));
        Child = row;

        MouseEnter += (_, _) => Hover(true);
        MouseLeave += (_, _) => Hover(false);
        GotKeyboardFocus += (_, _) => Hover(true);
        LostKeyboardFocus += (_, _) => Hover(IsMouseOver);
        MouseLeftButtonUp += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
        KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                Clicked?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        };
    }

    public string Title { get; }

    public event EventHandler? Clicked;

    private void Hover(bool on)
    {
        var target = Ui.Color(on ? "Brush.Brand" : "Brush.Divider");
        _edge.BeginAnimation(
            SolidColorBrush.ColorProperty,
            UiMotion.Enabled ? new System.Windows.Media.Animation.ColorAnimation(target, UiMotion.Quick) : null);
        if (!UiMotion.Enabled)
        {
            _edge.Color = target;
        }
        UiMotion.To(_lift, TranslateTransform.YProperty, on ? -2 : 0, UiMotion.Quick);
        _chip.Background = Ui.Brush(on ? "Brush.Brand" : "Brush.BrandSoft");
        SetIcon(_icon, on ? System.Windows.Media.Brushes.White : Ui.Brush("Brush.BrandInk"));
    }

    private static void SetIcon(DependencyObject root, Brush brush)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is System.Windows.Shapes.Shape shape)
            {
                if (shape.Stroke is not null)
                {
                    shape.Stroke = brush;
                }
                if (shape.Fill is not null)
                {
                    shape.Fill = brush;
                }
            }
            SetIcon(child, brush);
        }
    }
}
