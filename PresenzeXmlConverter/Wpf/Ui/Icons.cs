using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using ShapePath = System.Windows.Shapes.Path;

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
/// Le icone dell'interfaccia classica (<see cref="IconGeometry"/>, griglia 24 × 24, tratto con estremità tonde)
/// come elementi vettoriali di WPF: stessa forma, nitida a qualunque scala dello schermo.
/// </summary>
internal static class Icons
{
    /// <summary>Un'icona quadrata di <paramref name="size"/> unità, tracciata col pennello dato.</summary>
    public static FrameworkElement Create(Glyph glyph, Brush brush, double size = 18, double stroke = 1.7)
    {
        var canvas = new Canvas { Width = IconGeometry.Grid, Height = IconGeometry.Grid };
        foreach (var shape in IconGeometry.For(glyph))
        {
            var path = new ShapePath
            {
                Data = ToGeometry(shape),
                StrokeThickness = stroke * IconGeometry.Grid / size,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            if (shape.Filled)
            {
                path.Fill = brush;
            }
            else
            {
                path.Stroke = brush;
            }
            canvas.Children.Add(path);
        }
        return new Viewbox { Width = size, Height = size, Child = canvas, Stretch = Stretch.Uniform, IsHitTestVisible = false };
    }

    /// <summary>Il tracciato dell'icona come geometria congelata.</summary>
    internal static Geometry ToGeometry(IconPath shape)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var segments = shape.Segs;
            var current = new System.Windows.Point();
            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                var v = segment.Values;
                switch (segment.Op)
                {
                    case 'M':
                        current = new System.Windows.Point(v[0], v[1]);
                        context.BeginFigure(current, shape.Filled, ClosesBeforeNextMove(segments, index));
                        break;
                    case 'L':
                        current = new System.Windows.Point(v[0], v[1]);
                        context.LineTo(current, true, true);
                        break;
                    case 'C':
                        context.BezierTo(
                            new System.Windows.Point(v[0], v[1]),
                            new System.Windows.Point(v[2], v[3]),
                            new System.Windows.Point(v[4], v[5]),
                            true,
                            true);
                        break;
                }
            }
        }
        geometry.Freeze();
        return geometry;
    }

    /// <summary>La figura che comincia qui si chiude (Z) prima della prossima «M»?</summary>
    private static bool ClosesBeforeNextMove(IReadOnlyList<IconSeg> segments, int start)
    {
        for (var index = start + 1; index < segments.Count; index++)
        {
            if (segments[index].Op == 'M')
            {
                return false;
            }
            if (segments[index].Op == 'Z')
            {
                return true;
            }
        }
        return false;
    }
}
