using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
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
using FontFamily = System.Windows.Media.FontFamily;
using Application = System.Windows.Application;

/// <summary>
/// Barra di navigazione della finestra: voci con icona e titolo, e una pillola blu piena che scorre da una
/// voce all'altra (con la curva di decelerazione, 240 ms) mentre il colore di testo e icona vira al bianco.
/// Frecce su e giù cambiano voce; ogni voce ha il nome per i lettori di schermo.
/// </summary>
internal sealed class NavRail : Grid
{
    private const double ItemHeight = 44;
    private const double Gap = 4;

    private readonly List<Item> _items = new();
    private readonly Border _pill;
    private readonly TranslateTransform _pillMove = new();
    private readonly StackPanel _stack = new() { Orientation = Orientation.Vertical };

    public NavRail()
    {
        Focusable = true;
        FocusVisualStyle = null;
        _pill = new Border
        {
            Height = ItemHeight,
            CornerRadius = new CornerRadius(11),
            Background = Ui.Brush("Brush.Brand"),
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransform = _pillMove,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        Children.Add(_pill);
        Children.Add(_stack);
        AutomationProperties_SetName(this, "Sezioni dell'applicazione");
    }

    private static void AutomationProperties_SetName(DependencyObject element, string name) =>
        System.Windows.Automation.AutomationProperties.SetName(element, name);

    public event EventHandler? SelectionChanged;

    public int SelectedIndex { get; private set; } = -1;

    public int ItemCount => _items.Count;

    public string TitleAt(int index) => _items[index].Title;

    public void AddItem(string title, Glyph glyph)
    {
        var item = new Item(title, glyph, _items.Count);
        item.Root.Margin = new Thickness(0, 0, 0, Gap);
        item.Root.MouseLeftButtonUp += (_, _) => Select(item.Index);
        _items.Add(item);
        _stack.Children.Add(item.Root);
    }

    /// <summary>Sceglie una voce; la pillola scorre fino a lei (la prima volta compare già al suo posto).</summary>
    public void Select(int index, bool raiseEvent = true)
    {
        if (_items.Count == 0)
        {
            return;
        }
        index = Math.Clamp(index, 0, _items.Count - 1);
        if (index == SelectedIndex)
        {
            return;
        }
        var first = SelectedIndex < 0;
        var previous = SelectedIndex;
        SelectedIndex = index;
        var target = index * (ItemHeight + Gap);
        _pill.Visibility = Visibility.Visible;
        if (first || !UiMotion.Enabled)
        {
            _pillMove.BeginAnimation(TranslateTransform.YProperty, null);
            _pillMove.Y = target;
        }
        else
        {
            UiMotion.To(_pillMove, TranslateTransform.YProperty, target, UiMotion.Standard, UiMotion.EaseInOut);
        }
        if (previous >= 0)
        {
            _items[previous].SetActive(false);
        }
        _items[index].SetActive(true);
        if (raiseEvent)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Down:
                Select(SelectedIndex + 1);
                e.Handled = true;
                break;
            case Key.Up:
                Select(SelectedIndex - 1);
                e.Handled = true;
                break;
            case Key.Home:
                Select(0);
                e.Handled = true;
                break;
            case Key.End:
                Select(_items.Count - 1);
                e.Handled = true;
                break;
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
    }

    /// <summary>Una voce: icona e titolo sopra un velo che compare al passaggio del mouse.</summary>
    private sealed class Item
    {
        private readonly SolidColorBrush _ink;
        private readonly Border _hover;
        private readonly TextBlock _text;
        private bool _active;

        public Item(string title, Glyph glyph, int index)
        {
            Title = title;
            Index = index;
            _ink = Ui.Mutable("Brush.InkSecondary");
            _hover = new Border
            {
                Background = Ui.Brush("Brush.SidebarHover"),
                CornerRadius = new CornerRadius(11),
                Opacity = 0,
                IsHitTestVisible = false
            };
            _text = new TextBlock
            {
                Text = title,
                FontFamily = Ui.Font,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = _ink,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                IsHitTestVisible = false
            };
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(14, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            row.Children.Add(Icons.Create(glyph, _ink, 18, 1.7));
            row.Children.Add(_text);
            Root = new Grid { Height = ItemHeight, Background = System.Windows.Media.Brushes.Transparent, Cursor = Cursors.Hand };
            Root.Children.Add(_hover);
            Root.Children.Add(row);
            System.Windows.Automation.AutomationProperties.SetName(Root, title);
            Root.MouseEnter += (_, _) =>
            {
                if (!_active)
                {
                    UiMotion.To(_hover, UIElement.OpacityProperty, 1, UiMotion.Quick);
                }
            };
            Root.MouseLeave += (_, _) => UiMotion.To(_hover, UIElement.OpacityProperty, 0, UiMotion.Quick);
        }

        public string Title { get; }

        public int Index { get; }

        public Grid Root { get; }

        public void SetActive(bool active)
        {
            _active = active;
            UiMotion.To(_hover, UIElement.OpacityProperty, 0, UiMotion.Quick);
            var target = active ? Colors.White : Ui.Color("Brush.InkSecondary");
            if (UiMotion.Enabled)
            {
                _ink.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(target, UiMotion.Standard) { EasingFunction = UiMotion.EaseOut });
            }
            else
            {
                _ink.Color = target;
            }
            _text.FontWeight = active ? FontWeights.SemiBold : FontWeights.Medium;
        }
    }
}
