using System.Drawing;

namespace PresenzeXmlConverter;

/// <summary>
/// Misure e colori dei menù, calcolati senza disegnare: i test controllano che il testo resti leggibile su ogni
/// sfondo del menù, in tema chiaro e scuro.
/// </summary>
internal static class MenuPalette
{
    public static Color Back(ThemePalette p) => p.Surface;

    public static Color Border(ThemePalette p) => p.DividerStrong;

    public static Color HoverFill(ThemePalette p) => p.BrandSoft;

    public static Color HoverInk(ThemePalette p) => p.BrandInk;

    public static Color Text(ThemePalette p, bool enabled, bool hot) =>
        !enabled ? p.DisabledInk : hot ? HoverInk(p) : p.Ink;
}
