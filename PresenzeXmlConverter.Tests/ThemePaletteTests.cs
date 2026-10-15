using System.Drawing;
using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public class ThemePaletteTests
{
    public static IEnumerable<object[]> Palettes() => [[false], [true]];

    private static ThemePalette Pick(bool dark) => dark ? ThemePalette.Dark : ThemePalette.Light;

    private static void AtLeast(double minimum, Color text, Color back, string what)
    {
        var ratio = ColorContrast.Ratio(text, back);
        Assert.True(ratio >= minimum, $"{what}: contrasto {ratio:0.00}:1, ne servono almeno {minimum}:1");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void BodyTextIsReadableOnEverySurface(bool dark)
    {
        var p = Pick(dark);
        AtLeast(7, p.Ink, p.Canvas, "Ink su Canvas");
        AtLeast(7, p.Ink, p.Surface, "Ink su Surface");
        AtLeast(4.5, p.InkSecondary, p.Surface, "InkSecondary su Surface");
        AtLeast(4.5, p.MutedInk, p.Surface, "MutedInk su Surface");
        AtLeast(4.5, p.MutedInk, p.Canvas, "MutedInk su Canvas");
        AtLeast(4.5, p.Ink, p.Sunken, "Ink su Sunken");
        AtLeast(4.5, p.Ink, p.AltRow, "Ink su AltRow");
        AtLeast(4.5, p.Ink, p.FieldFill, "Ink su FieldFill");
        AtLeast(4.5, p.Ink, p.HoverRow, "Ink su HoverRow");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void StatusColorsAreReadableOnTheirFills(bool dark)
    {
        var p = Pick(dark);
        AtLeast(4.5, p.BrandInk, p.Surface, "BrandInk su Surface");
        AtLeast(4.5, p.BrandInk, p.BrandSoft, "BrandInk su BrandSoft");
        AtLeast(4.5, p.SuccessInk, p.SuccessSoft, "SuccessInk su SuccessSoft");
        AtLeast(4.5, p.WarningInk, p.WarningSoft, "WarningInk su WarningSoft");
        AtLeast(4.5, p.DangerInk, p.DangerSoft, "DangerInk su DangerSoft");
        AtLeast(4.5, p.PillIdleInk, p.PillIdleFill, "Pillola a riposo");
        AtLeast(4.5, p.PillReadyInk, p.PillReadyFill, "Pillola pronta");
        AtLeast(4.5, p.PillWarningInk, p.PillWarningFill, "Pillola di avviso");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void HeaderButtonsAndGridsAreReadable(bool dark)
    {
        var p = Pick(dark);
        AtLeast(7, p.HeaderInk, p.HeaderFill, "HeaderInk su HeaderFill");
        AtLeast(4.5, p.HeaderMuted, p.HeaderFill, "HeaderMuted su HeaderFill");
        AtLeast(4.5, p.HeaderInk, p.HeaderFieldFill, "HeaderInk nel campo di ricerca");
        AtLeast(3, p.HeaderMuted, p.HeaderFieldFill, "Lente nel campo di ricerca (icona: bastano 3:1)");
        AtLeast(7, p.NeutralInk, p.NeutralFill, "Pulsante neutro");
        AtLeast(7, p.NeutralInk, p.NeutralHover, "Pulsante neutro sotto il mouse");
        AtLeast(7, p.Ink, p.HeaderButtonFill, "Pulsante della testata");
        AtLeast(4.5, p.CodeInk, p.Surface, "Codici nelle griglie");
        AtLeast(4.5, p.NumberInk, p.Surface, "Numeri nelle griglie");
        AtLeast(4.5, p.DateInk, p.Surface, "Date nelle griglie");
        AtLeast(7, p.EditorInk, p.EditorFill, "Editor XML");
        AtLeast(2, p.DisabledInk, p.DisabledFill, "Testo disabilitato (volutamente tenue ma visibile)");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void SurfacesAreDistinguishableFromEachOther(bool dark)
    {
        var p = Pick(dark);
        Assert.NotEqual(p.Canvas, p.Surface);
        Assert.NotEqual(p.Surface, p.Sunken);
        Assert.NotEqual(p.Surface, p.Divider);
        AtLeast(1.1, p.Divider, p.Surface, "Divider contro Surface");
        AtLeast(1.5, p.DividerStrong, p.Surface, "Bordo dei campi contro Surface");
        AtLeast(3, Color.White, p.Brand, "Testo bianco sul pulsante primario");
    }

    [Fact]
    public void DarkPaletteIsDarkAndLightPaletteIsLight()
    {
        Assert.True(ThemePalette.Dark.IsDark);
        Assert.False(ThemePalette.Light.IsDark);
        Assert.True(ThemePalette.Dark.Canvas.GetBrightness() < 0.2f);
        Assert.True(ThemePalette.Light.Canvas.GetBrightness() > 0.9f);
        Assert.True(ThemePalette.Dark.Ink.GetBrightness() > ThemePalette.Dark.Surface.GetBrightness());
    }

    [Fact]
    public void EveryColorIsSetInBothPalettes()
    {
        foreach (var palette in new[] { ThemePalette.Light, ThemePalette.Dark })
        {
            foreach (var property in typeof(ThemePalette).GetProperties().Where(p => p.PropertyType == typeof(Color)))
            {
                var color = (Color)property.GetValue(palette)!;
                Assert.True(color.A == 255 && !color.IsEmpty, $"{property.Name} non è impostato");
            }
        }
    }

    [Theory]
    [InlineData(null, "System")]
    [InlineData("", "System")]
    [InlineData("system", "System")]
    [InlineData("light", "Light")]
    [InlineData(" DARK \n", "Dark")]
    [InlineData("qualcosa", "System")]
    public void SavedTextIsParsed(string? text, string expected) => Assert.Equal(expected, ThemeChoice.Parse(text).ToString());

    [Fact]
    public void ModeRoundTripsAndResolves()
    {
        foreach (var mode in Enum.GetValues<ThemeMode>())
        {
            Assert.Equal(mode, ThemeChoice.Parse(ThemeChoice.Serialize(mode)));
        }
        Assert.True(ThemeChoice.ResolveDark(ThemeMode.Dark, false));
        Assert.False(ThemeChoice.ResolveDark(ThemeMode.Light, true));
        Assert.True(ThemeChoice.ResolveDark(ThemeMode.System, true));
        Assert.False(ThemeChoice.ResolveDark(ThemeMode.System, false));
    }

    [Fact]
    public void ContrastRatioMatchesKnownValues()
    {
        Assert.Equal(21.0, ColorContrast.Ratio(Color.Black, Color.White), 1);
        Assert.Equal(1.0, ColorContrast.Ratio(Color.Gray, Color.Gray), 3);
        Assert.Equal(ColorContrast.Ratio(Color.Black, Color.White), ColorContrast.Ratio(Color.White, Color.Black), 6);
    }
}
