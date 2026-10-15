using System.Drawing;

namespace PresenzeXmlConverter;

/// <summary>Come si sceglie il tema: segue Windows, sempre chiaro o sempre scuro.</summary>
internal enum ThemeMode
{
    System,
    Light,
    Dark
}

/// <summary>Scelta del tema: lettura/scrittura del testo salvato e risoluzione in chiaro o scuro. Solo calcolo.</summary>
internal static class ThemeChoice
{
    public static ThemeMode Parse(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "light" => ThemeMode.Light,
        "dark" => ThemeMode.Dark,
        _ => ThemeMode.System
    };

    public static string Serialize(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => "light",
        ThemeMode.Dark => "dark",
        _ => "system"
    };

    public static bool ResolveDark(ThemeMode mode, bool systemIsDark) => mode switch
    {
        ThemeMode.Dark => true,
        ThemeMode.Light => false,
        _ => systemIsDark
    };
}

/// <summary>Rapporto di contrasto WCAG fra due colori (1 = uguali, 21 = nero su bianco).</summary>
internal static class ColorContrast
{
    public static double Ratio(Color a, Color b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var (light, dark) = la >= lb ? (la, lb) : (lb, la);
        return (light + 0.05) / (dark + 0.05);
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }
}

/// <summary>
/// Tutti i colori dell'interfaccia, per ruolo, in due versioni (chiara e scura). <see cref="Theme"/> ne tiene una
/// attiva; i controlli leggono sempre da lì, quindi cambiare tema vuol dire solo scegliere l'altra tavolozza.
/// </summary>
internal sealed record ThemePalette
{
    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    public bool IsDark { get; init; }

    // Testo
    public Color Ink { get; init; }
    public Color InkSecondary { get; init; }
    public Color MutedInk { get; init; }
    public Color FaintInk { get; init; }

    // Superfici
    public Color Surface { get; init; }
    public Color Canvas { get; init; }
    public Color Sunken { get; init; }
    public Color AltRow { get; init; }
    public Color FieldFill { get; init; }
    public Color HoverRow { get; init; }

    // Bordi
    public Color Divider { get; init; }
    public Color DividerStrong { get; init; }

    // Marca
    public Color Brand { get; init; }
    public Color BrandHover { get; init; }
    public Color BrandPressed { get; init; }
    public Color BrandDeep { get; init; }
    public Color BrandSoft { get; init; }
    public Color BrandLite { get; init; }
    public Color BrandInk { get; init; }
    public Color Accent { get; init; }

    // Esiti
    public Color Success { get; init; }
    public Color SuccessHover { get; init; }
    public Color SuccessPressed { get; init; }
    public Color SuccessSoft { get; init; }
    public Color SuccessInk { get; init; }
    public Color Warning { get; init; }
    public Color WarningInk { get; init; }
    public Color WarningSoft { get; init; }
    public Color Danger { get; init; }
    public Color DangerInk { get; init; }
    public Color DangerSoft { get; init; }

    // Pulsanti
    public Color NeutralFill { get; init; }
    public Color NeutralHover { get; init; }
    public Color NeutralPressed { get; init; }
    public Color NeutralBorder { get; init; }
    public Color NeutralInk { get; init; }
    public Color DisabledFill { get; init; }
    public Color DisabledInk { get; init; }

    // Testata
    public Color HeaderFill { get; init; }
    public Color HeaderInk { get; init; }
    public Color HeaderMuted { get; init; }
    public Color HeaderFieldFill { get; init; }
    public Color HeaderFieldBorder { get; init; }
    public Color HeaderButtonFill { get; init; }
    public Color HeaderButtonHover { get; init; }
    public Color HeaderButtonPressed { get; init; }
    public Color HeaderButtonBorder { get; init; }

    // Pillole
    public Color PillIdleFill { get; init; }
    public Color PillIdleInk { get; init; }
    public Color PillReadyFill { get; init; }
    public Color PillReadyInk { get; init; }
    public Color PillWarningFill { get; init; }
    public Color PillWarningInk { get; init; }

    // Editor XML (scuro in entrambi i temi)
    public Color EditorFill { get; init; }
    public Color EditorInk { get; init; }
    public Color EditorTag { get; init; }
    public Color EditorString { get; init; }
    public Color EditorValue { get; init; }

    // Griglie
    public Color CodeInk { get; init; }
    public Color NumberInk { get; init; }
    public Color DateInk { get; init; }
    public Color RowNumberInk { get; init; }

    public static ThemePalette Light { get; } = new()
    {
        IsDark = false,
        Ink = C(29, 29, 31),
        InkSecondary = C(60, 60, 67),
        MutedInk = C(110, 110, 115),
        FaintInk = C(174, 174, 178),
        Surface = C(255, 255, 255),
        Canvas = C(245, 245, 247),
        Sunken = C(236, 236, 241),
        AltRow = C(249, 249, 251),
        FieldFill = C(255, 255, 255),
        HoverRow = C(243, 247, 255),
        Divider = C(229, 229, 234),
        DividerStrong = C(209, 209, 214),
        Brand = C(0, 122, 255),
        BrandHover = C(0, 106, 230),
        BrandPressed = C(0, 88, 200),
        BrandDeep = C(0, 64, 148),
        BrandSoft = C(232, 242, 255),
        BrandLite = C(207, 228, 255),
        BrandInk = C(0, 98, 204),
        Accent = C(90, 200, 250),
        Success = C(52, 199, 89),
        SuccessHover = C(40, 176, 74),
        SuccessPressed = C(30, 150, 62),
        SuccessSoft = C(226, 247, 232),
        SuccessInk = C(24, 110, 48),
        Warning = C(255, 149, 0),
        WarningInk = C(158, 88, 0),
        WarningSoft = C(254, 243, 199),
        Danger = C(255, 59, 48),
        DangerInk = C(176, 28, 20),
        DangerSoft = C(254, 226, 226),
        NeutralFill = C(255, 255, 255),
        NeutralHover = C(245, 245, 247),
        NeutralPressed = C(232, 232, 237),
        NeutralBorder = C(209, 209, 214),
        NeutralInk = C(29, 29, 31),
        DisabledFill = C(244, 244, 246),
        DisabledInk = C(174, 174, 178),
        HeaderFill = C(255, 255, 255),
        HeaderInk = C(29, 29, 31),
        HeaderMuted = C(110, 110, 115),
        HeaderFieldFill = C(238, 238, 243),
        HeaderFieldBorder = C(226, 226, 232),
        HeaderButtonFill = C(242, 242, 247),
        HeaderButtonHover = C(229, 229, 234),
        HeaderButtonPressed = C(216, 216, 222),
        HeaderButtonBorder = C(222, 222, 228),
        PillIdleFill = C(232, 242, 255),
        PillIdleInk = C(0, 98, 204),
        PillReadyFill = C(226, 247, 232),
        PillReadyInk = C(24, 110, 48),
        PillWarningFill = C(255, 243, 224),
        PillWarningInk = C(158, 88, 0),
        EditorFill = C(15, 23, 42),
        EditorInk = C(226, 232, 240),
        EditorTag = C(125, 211, 252),
        EditorString = C(190, 242, 100),
        EditorValue = C(167, 243, 208),
        CodeInk = C(0, 98, 204),
        NumberInk = C(24, 110, 48),
        DateInk = C(110, 110, 115),
        RowNumberInk = C(174, 174, 178)
    };

    public static ThemePalette Dark { get; } = new()
    {
        IsDark = true,
        Ink = C(245, 245, 247),
        InkSecondary = C(209, 209, 214),
        MutedInk = C(160, 160, 166),
        FaintInk = C(110, 110, 115),
        Surface = C(44, 44, 46),
        Canvas = C(28, 28, 30),
        Sunken = C(58, 58, 60),
        AltRow = C(50, 50, 52),
        FieldFill = C(28, 28, 30),
        HoverRow = C(52, 58, 70),
        Divider = C(62, 62, 66),
        DividerStrong = C(84, 84, 88),
        Brand = C(10, 132, 255),
        BrandHover = C(64, 156, 255),
        BrandPressed = C(0, 112, 230),
        BrandDeep = C(0, 64, 148),
        BrandSoft = C(14, 42, 74),
        BrandLite = C(24, 66, 116),
        BrandInk = C(110, 186, 255),
        Accent = C(100, 210, 255),
        Success = C(48, 209, 88),
        SuccessHover = C(70, 222, 108),
        SuccessPressed = C(36, 180, 72),
        SuccessSoft = C(20, 56, 32),
        SuccessInk = C(118, 224, 146),
        Warning = C(255, 159, 10),
        WarningInk = C(255, 196, 92),
        WarningSoft = C(74, 54, 14),
        Danger = C(255, 69, 58),
        DangerInk = C(255, 140, 130),
        DangerSoft = C(80, 30, 28),
        NeutralFill = C(58, 58, 60),
        NeutralHover = C(72, 72, 74),
        NeutralPressed = C(44, 44, 46),
        NeutralBorder = C(88, 88, 92),
        NeutralInk = C(245, 245, 247),
        DisabledFill = C(40, 40, 42),
        DisabledInk = C(110, 110, 115),
        HeaderFill = C(44, 44, 46),
        HeaderInk = C(245, 245, 247),
        HeaderMuted = C(160, 160, 166),
        HeaderFieldFill = C(28, 28, 30),
        HeaderFieldBorder = C(72, 72, 76),
        HeaderButtonFill = C(58, 58, 60),
        HeaderButtonHover = C(72, 72, 74),
        HeaderButtonPressed = C(44, 44, 46),
        HeaderButtonBorder = C(84, 84, 88),
        PillIdleFill = C(14, 42, 74),
        PillIdleInk = C(110, 186, 255),
        PillReadyFill = C(20, 56, 32),
        PillReadyInk = C(118, 224, 146),
        PillWarningFill = C(66, 44, 10),
        PillWarningInk = C(255, 196, 92),
        EditorFill = C(15, 23, 42),
        EditorInk = C(226, 232, 240),
        EditorTag = C(125, 211, 252),
        EditorString = C(190, 242, 100),
        EditorValue = C(167, 243, 208),
        CodeInk = C(110, 186, 255),
        NumberInk = C(118, 224, 146),
        DateInk = C(160, 160, 166),
        RowNumberInk = C(110, 110, 115)
    };
}
