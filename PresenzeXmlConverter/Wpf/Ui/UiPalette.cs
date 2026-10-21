using System.Windows.Media;
using Color = System.Windows.Media.Color;
using DrawingColor = System.Drawing.Color;

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
using FontFamily = System.Windows.Media.FontFamily;
using Brush = System.Windows.Media.Brush;
using Application = System.Windows.Application;

/// <summary>
/// I colori della nuova interfaccia: arrivano dalla stessa tavolozza dell'interfaccia classica
/// (<see cref="ThemePalette"/>), quindi tema chiaro, scuro e «Vetro» scelti nelle Impostazioni valgono
/// per entrambe. Ogni colore ha anche la sua versione esadecimale, usata per comporre gli stili.
/// </summary>
internal sealed class UiPalette
{
    private readonly ThemePalette _palette;

    public UiPalette(ThemePalette palette) => _palette = palette;

    /// <summary>La tavolozza del tema attivo.</summary>
    public static UiPalette Current => new(Theme.Current);

    public bool IsDark => _palette.IsDark;

    public string Ink => Hex(_palette.Ink);
    public string InkSecondary => Hex(_palette.InkSecondary);
    public string Muted => Hex(_palette.MutedInk);
    public string Faint => Hex(_palette.FaintInk);
    public string Surface => Hex(_palette.Surface);
    public string Canvas => Hex(_palette.Canvas);
    public string Sunken => Hex(_palette.Sunken);
    public string Sidebar => Hex(_palette.Sidebar);
    public string SidebarHover => Hex(_palette.SidebarHover);
    public string AltRow => Hex(_palette.AltRow);
    public string HoverRow => Hex(_palette.HoverRow);
    public string Field => Hex(_palette.FieldFill);
    public string Divider => Hex(_palette.Divider);
    public string DividerStrong => Hex(_palette.DividerStrong);
    public string Brand => Hex(_palette.Brand);
    public string BrandHover => Hex(_palette.BrandHover);
    public string BrandPressed => Hex(_palette.BrandPressed);
    public string BrandSoft => Hex(_palette.BrandSoft);
    public string BrandLite => Hex(_palette.BrandLite);
    public string BrandInk => Hex(_palette.BrandInk);
    public string Success => Hex(_palette.Success);
    public string SuccessSoft => Hex(_palette.SuccessSoft);
    public string SuccessInk => Hex(_palette.SuccessInk);
    public string Warning => Hex(_palette.Warning);
    public string WarningSoft => Hex(_palette.WarningSoft);
    public string WarningInk => Hex(_palette.WarningInk);
    public string Danger => Hex(_palette.Danger);
    public string DangerSoft => Hex(_palette.DangerSoft);
    public string DangerInk => Hex(_palette.DangerInk);
    public string NeutralBorder => Hex(_palette.NeutralBorder);
    public string DisabledInk => Hex(_palette.DisabledInk);
    public string DisabledFill => Hex(_palette.DisabledFill);

    /// <summary>Velo scuro (chiaro nel tema scuro) per lo stato «sotto il mouse» dei pulsanti.</summary>
    public string ThumbRest => IsDark ? "#55FFFFFF" : "#38000000";
    public string ThumbHover => IsDark ? "#88FFFFFF" : "#66000000";
    public string ThumbDrag => IsDark ? "#AAFFFFFF" : "#88000000";
    public string HoverVeil => _palette.IsDark ? "#14FFFFFF" : "#0C000000";

    public string PressVeil => _palette.IsDark ? "#22FFFFFF" : "#18000000";

    public static string Hex(DrawingColor color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>Pennello pieno e congelato a partire da un colore esadecimale.</summary>
    public static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
