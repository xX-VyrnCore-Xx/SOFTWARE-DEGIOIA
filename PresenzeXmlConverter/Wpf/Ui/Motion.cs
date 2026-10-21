using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

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

/// <summary>
/// Animazioni di base della nuova interfaccia: durate brevi e curve con decelerazione, come i movimenti
/// dei sistemi operativi. Se Windows ha gli effetti disattivati (accessibilità) i valori saltano al traguardo.
/// </summary>
internal static class UiMotion
{
    public static readonly Duration Quick = new(TimeSpan.FromMilliseconds(140));
    public static readonly Duration Standard = new(TimeSpan.FromMilliseconds(240));
    public static readonly Duration Slow = new(TimeSpan.FromMilliseconds(420));

    /// <summary>Gli effetti di animazione di Windows sono attivi?</summary>
    public static bool Enabled
    {
        get
        {
            try
            {
                return SystemParameters.ClientAreaAnimation;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    public static IEasingFunction EaseOut { get; } = new CubicEase { EasingMode = EasingMode.EaseOut };

    public static IEasingFunction EaseInOut { get; } = new CubicEase { EasingMode = EasingMode.EaseInOut };

    /// <summary>
    /// Anima una proprietà numerica verso un valore. Il valore finale viene scritto subito nella proprietà e
    /// l'animazione parte dal valore di prima e poi si ferma: così più tardi la proprietà si può impostare
    /// di nuovo normalmente (un'animazione che "tiene" il valore finale lo bloccherebbe).
    /// </summary>
    public static void To(DependencyObject target, DependencyProperty property, double value, Duration? duration = null, IEasingFunction? easing = null)
    {
        var from = (double)target.GetValue(property);
        if (target is not IAnimatable animatable)
        {
            target.SetValue(property, value);
            return;
        }
        animatable.BeginAnimation(property, null);
        target.SetValue(property, value);
        if (!Enabled || Math.Abs(from - value) < 0.001)
        {
            return;
        }
        animatable.BeginAnimation(property, new DoubleAnimation
        {
            From = from,
            To = value,
            Duration = duration ?? Standard,
            EasingFunction = easing ?? EaseOut,
            FillBehavior = FillBehavior.Stop
        });
    }

    /// <summary>Dissolvenza in entrata con un leggero scorrimento dal basso (ingresso di una pagina o di un riquadro).</summary>
    public static void FadeIn(FrameworkElement element, double rise = 10, Duration? duration = null, TimeSpan? delay = null)
    {
        var slide = new TranslateTransform(0, 0);
        element.RenderTransform = slide;
        element.Opacity = 1;
        if (!Enabled)
        {
            return;
        }
        var length = duration ?? Standard;
        var begin = delay ?? TimeSpan.Zero;
        // Prima del via (con un ritardo) il riquadro deve già essere invisibile: i valori di partenza
        // si fissano subito, e a fine corsa si torna ai valori normali.
        element.Opacity = 0;
        slide.Y = rise;
        var fade = new DoubleAnimation(0, 1, length) { BeginTime = begin, EasingFunction = EaseOut, FillBehavior = FillBehavior.HoldEnd };
        var rises = new DoubleAnimation(rise, 0, length) { BeginTime = begin, EasingFunction = EaseOut, FillBehavior = FillBehavior.HoldEnd };
        fade.Completed += (_, _) =>
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 1;
            slide.BeginAnimation(TranslateTransform.YProperty, null);
            slide.Y = 0;
        };
        element.BeginAnimation(UIElement.OpacityProperty, fade);
        slide.BeginAnimation(TranslateTransform.YProperty, rises);
    }
}
