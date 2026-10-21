using System.Windows;
using System.Windows.Controls;
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
using Brush = System.Windows.Media.Brush;
using Application = System.Windows.Application;

/// <summary>
/// Impaginazione comune a tutte le pagine: in alto il titolo grande con il sottotitolo e, a destra, le azioni
/// della pagina; sotto il contenuto. Il titolo ha l'altezza del suo testo, qualunque sia la scala dello schermo.
/// </summary>
internal sealed class PageScaffold : Grid
{
    private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

    public PageScaffold(string title, string subtitle, UIElement body)
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        heading.Children.Add(Ui.Text(title, 28, FontWeights.SemiBold, trimming: TextTrimming.CharacterEllipsis));
        heading.Children.Add(Ui.Text(subtitle, 14, ink: "Brush.Muted", wrap: true).WithMargin(new Thickness(1, 4, 0, 0)));
        Children.Add(heading.At(0, 0));
        Children.Add(_actions.At(0, 1));
        _actions.Margin = new Thickness(16, 0, 0, 20);
        Children.Add(body.At(1, 0, columnSpan: 2));
    }

    /// <summary>Aggiunge un'azione nell'angolo in alto a destra della pagina.</summary>
    public void AddAction(UIElement action)
    {
        if (_actions.Children.Count > 0 && action is FrameworkElement element)
        {
            element.Margin = new Thickness(10, 0, 0, 0);
        }
        _actions.Children.Add(action);
    }
}

internal static class FrameworkElementExtensions
{
    public static T WithMargin<T>(this T element, Thickness margin)
        where T : FrameworkElement
    {
        element.Margin = margin;
        return element;
    }
}
