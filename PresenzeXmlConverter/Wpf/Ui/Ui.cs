using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
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

/// <summary>
/// Scorciatoie per costruire l'interfaccia in codice: pennelli e stili presi dal dizionario unito
/// all'applicazione, testi, riquadri e pile. Tutte le viste usano queste e non scrivono mai un colore a mano.
/// </summary>
internal static class Ui
{
    public static readonly FontFamily Font = new(UiStyles.FontStack);

    /// <summary>Un pennello del tema, per nome (per esempio <c>Brush.Brand</c>).</summary>
    public static Brush Brush(string key) =>
        (Brush)(Application.Current?.TryFindResource(key) ?? System.Windows.Media.Brushes.Magenta);

    public static Color Color(string key) => Brush(key) is SolidColorBrush solid ? solid.Color : Colors.Magenta;

    /// <summary>Un pennello nuovo e modificabile (per animare il colore) con il colore del tema.</summary>
    public static SolidColorBrush Mutable(string key) => new(Color(key));

    public static Style? Style(string key) => Application.Current?.TryFindResource(key) as Style;

    public static TextBlock Text(
        string text,
        double size = 14,
        FontWeight? weight = null,
        string ink = "Brush.Ink",
        bool wrap = false,
        TextTrimming trimming = TextTrimming.None)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = Font,
            FontSize = size,
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = Brush(ink),
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = trimming
        };
        return block;
    }

    /// <summary>Un riquadro in stile «card»: sfondo di superficie, filo sottile e angoli morbidi.</summary>
    public static Border Card(UIElement? child = null, Thickness? padding = null, double radius = 14, bool shadow = true)
    {
        var card = new Border
        {
            Background = Brush("Brush.Surface"),
            BorderBrush = Brush("Brush.Divider"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(radius),
            Padding = padding ?? new Thickness(20),
            Child = child,
            SnapsToDevicePixels = true
        };
        if (shadow && Application.Current?.TryFindResource("Shadow.Card") is System.Windows.Media.Effects.Effect effect)
        {
            card.Effect = effect;
        }
        return card;
    }

    public static StackPanel Stack(Orientation orientation = Orientation.Vertical, params UIElement[] children)
    {
        var stack = new StackPanel { Orientation = orientation };
        foreach (var child in children)
        {
            stack.Children.Add(child);
        }
        return stack;
    }

    /// <summary>Una colonna di righe di una griglia con le altezze date (Auto, stella o pixel).</summary>
    public static Grid Rows(params GridLength[] heights)
    {
        var grid = new Grid();
        foreach (var height in heights)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = height });
        }
        return grid;
    }

    public static Grid Columns(params GridLength[] widths)
    {
        var grid = new Grid();
        foreach (var width in widths)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        }
        return grid;
    }

    public static T At<T>(this T element, int row = 0, int column = 0, int rowSpan = 1, int columnSpan = 1)
        where T : UIElement
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetRowSpan(element, rowSpan);
        Grid.SetColumnSpan(element, columnSpan);
        return element;
    }

    public static GridLength Auto => GridLength.Auto;

    public static GridLength Star(double weight = 1) => new(weight, GridUnitType.Star);

    public static GridLength Px(double value) => new(value, GridUnitType.Pixel);
}
