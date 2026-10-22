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
using Binding = System.Windows.Data.Binding;

/// <summary>Una fila di «pillole» a scelta singola (filtri, schede): quella scelta è in blu, le altre sfumano al passaggio del mouse.</summary>
internal sealed class PillBar : WrapPanel
{
    private readonly List<(Border Pill, TextBlock Text)> _items = new();
    private int _selected = -1;

    public PillBar(IEnumerable<string> titles)
    {
        foreach (var title in titles)
        {
            var text = Ui.Text(title, 13.5, FontWeights.SemiBold, "Brush.Muted");
            text.VerticalAlignment = VerticalAlignment.Center;
            var pill = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 7, 14, 7),
                Margin = new Thickness(0, 0, 6, 6),
                Background = System.Windows.Media.Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = text
            };
            var index = _items.Count;
            pill.MouseLeftButtonUp += (_, _) => Select(index);
            System.Windows.Automation.AutomationProperties.SetName(pill, title);
            Children.Add(pill);
            _items.Add((pill, text));
        }
        Select(0, raise: false);
    }

    public int SelectedIndex => _selected;

    public event EventHandler? SelectionChanged;

    public void Select(int index, bool raise = true)
    {
        if (index < 0 || index >= _items.Count)
        {
            return;
        }
        _selected = index;
        for (var i = 0; i < _items.Count; i++)
        {
            var active = i == index;
            _items[i].Pill.Background = active ? Ui.Brush("Brush.BrandSoft") : System.Windows.Media.Brushes.Transparent;
            _items[i].Text.Foreground = Ui.Brush(active ? "Brush.BrandInk" : "Brush.Muted");
        }
        if (raise)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
