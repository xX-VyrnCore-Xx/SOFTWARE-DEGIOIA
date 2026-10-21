using System.Windows;
using Application = System.Windows.Application;

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

/// <summary>
/// Avvio della nuova interfaccia: crea l'applicazione WPF (una sola per processo), unisce gli stili della
/// tavolozza attiva e apre la finestra principale. Qualunque errore di avvio sale a chi chiama, che
/// ripiega sull'interfaccia classica.
/// </summary>
internal static class WpfApp
{
    /// <summary>
    /// Prepara l'applicazione WPF e gli stili (anche per i test, che non aprono la finestra principale).
    /// </summary>
    public static Application EnsureApplication()
    {
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        if (application.TryFindResource("Brush.Ink") is null)
        {
            application.Resources.MergedDictionaries.Add(UiStyles.Create(UiPalette.Current));
        }
        return application;
    }
}
