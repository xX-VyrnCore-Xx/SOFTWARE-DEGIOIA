using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
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
using Orientation = System.Windows.Controls.Orientation;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Application = System.Windows.Application;

/// <summary>
/// Area di rilascio del foglio Excel: rettangolo tratteggiato che si accende al passaggio del mouse e quando
/// ci si trascina sopra un file valido; mostra anche il file già caricato. Si clicca per scegliere un file
/// o si rilascia un .xlsx; Invio e Spazio fanno lo stesso da tastiera.
/// </summary>
internal sealed class DropZoneView : Grid
{
    private readonly ShapeRectangle _frame;
    private readonly Border _fill;
    private readonly Border _chip;
    private readonly TextBlock _title;
    private readonly TextBlock _subtitle;
    private readonly FrameworkElement _icon;
    private readonly SolidColorBrush _frameBrush;
    private readonly SolidColorBrush _fillBrush;
    private bool _loaded;
    private bool _hot;

    public DropZoneView()
    {
        Focusable = true;
        FocusVisualStyle = null;
        Background = System.Windows.Media.Brushes.Transparent;
        AllowDrop = true;
        Cursor = Cursors.Hand;
        MinHeight = 150;
        System.Windows.Automation.AutomationProperties.SetName(this, "Scegli o trascina il foglio Excel");

        _fillBrush = Ui.Mutable("Brush.Canvas");
        _frameBrush = Ui.Mutable("Brush.DividerStrong");
        _fill = new Border { Background = _fillBrush, CornerRadius = new CornerRadius(14), IsHitTestVisible = false };
        _frame = new ShapeRectangle
        {
            RadiusX = 14,
            RadiusY = 14,
            Stroke = _frameBrush,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 5, 4 },
            Margin = new Thickness(0.75),
            IsHitTestVisible = false
        };
        _icon = Icons.Create(Glyph.Upload, Ui.Brush("Brush.BrandInk"), 24, 1.8);
        _chip = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(16),
            Background = Ui.Brush("Brush.BrandSoft"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = _icon
        };
        _icon.HorizontalAlignment = HorizontalAlignment.Center;
        _icon.VerticalAlignment = VerticalAlignment.Center;
        _title = Ui.Text("Trascina qui il foglio Excel", 17, FontWeights.SemiBold);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        _title.Margin = new Thickness(0, 14, 0, 0);
        _subtitle = Ui.Text("Excel (.xlsx, .xlsm) o CSV · oppure fai clic per sceglierlo", 13.5, ink: "Brush.Muted");
        _subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
        _subtitle.Margin = new Thickness(0, 4, 0, 0);
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16), IsHitTestVisible = false };
        stack.Children.Add(_chip);
        stack.Children.Add(_title);
        stack.Children.Add(_subtitle);
        Children.Add(_fill);
        Children.Add(_frame);
        Children.Add(stack);

        MouseEnter += (_, _) => SetHot(true);
        MouseLeave += (_, _) => SetHot(false);
        MouseLeftButtonUp += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
        GotKeyboardFocus += (_, _) => SetHot(true);
        LostKeyboardFocus += (_, _) => SetHot(IsMouseOver);
        KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                Clicked?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        };
        DragEnter += OnDrag;
        DragOver += OnDrag;
        DragLeave += (_, _) => SetHot(IsMouseOver);
        Drop += (_, e) =>
        {
            SetHot(false);
            var path = WorkbookDrop.FirstWorkbookPath(e.Data.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>());
            if (path is not null)
            {
                FileDropped?.Invoke(this, path);
            }
        };
    }

    public event EventHandler? Clicked;

    public event EventHandler<string>? FileDropped;

    /// <summary>Come un clic sull'area: apre la scelta del file.</summary>
    public void RaiseClicked() => Clicked?.Invoke(this, EventArgs.Empty);

    private void OnDrag(object sender, DragEventArgs e)
    {
        var path = WorkbookDrop.FirstWorkbookPath(e.Data.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>());
        e.Effects = path is null ? DragDropEffects.None : DragDropEffects.Copy;
        SetHot(path is not null);
        e.Handled = true;
    }

    private void SetHot(bool hot)
    {
        _hot = hot;
        Refresh();
    }

    private void Refresh()
    {
        var frame = _loaded ? "Brush.Success" : _hot ? "Brush.Brand" : "Brush.DividerStrong";
        var fill = _loaded ? "Brush.SuccessSoft" : _hot ? "Brush.BrandSoft" : "Brush.Canvas";
        Animate(_frameBrush, Ui.Color(frame));
        Animate(_fillBrush, Ui.Color(fill));
        _frame.StrokeDashArray = _loaded ? null : new DoubleCollection { 5, 4 };
    }

    private static void Animate(SolidColorBrush brush, System.Windows.Media.Color target)
    {
        if (!UiMotion.Enabled)
        {
            brush.Color = target;
            return;
        }
        brush.BeginAnimation(SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(target, UiMotion.Quick));
    }

    public void ShowEmpty()
    {
        _loaded = false;
        _title.Text = "Trascina qui il foglio Excel";
        _subtitle.Text = "oppure fai clic per sceglierlo";
        _chip.Background = Ui.Brush("Brush.BrandSoft");
        Refresh();
    }

    public void ShowLoaded(string fileName, string detail)
    {
        _loaded = true;
        _title.Text = fileName;
        _subtitle.Text = detail;
        _chip.Background = Ui.Brush("Brush.SuccessSoft");
        Refresh();
    }
}
