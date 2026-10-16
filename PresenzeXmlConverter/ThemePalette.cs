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
        Ink = C(22, 32, 46),
        InkSecondary = C(68, 84, 106),
        MutedInk = C(96, 110, 130),
        FaintInk = C(160, 170, 186),
        Surface = C(255, 255, 255),
        Canvas = C(238, 241, 246),
        Sunken = C(232, 236, 243),
        AltRow = C(246, 248, 251),
        FieldFill = C(255, 255, 255),
        HoverRow = C(227, 239, 251),
        Divider = C(225, 230, 238),
        DividerStrong = C(196, 205, 219),
        Brand = C(15, 108, 189),
        BrandHover = C(17, 94, 163),
        BrandPressed = C(12, 78, 138),
        BrandDeep = C(10, 60, 110),
        BrandSoft = C(227, 239, 251),
        BrandLite = C(200, 223, 246),
        BrandInk = C(11, 83, 148),
        Accent = C(107, 181, 245),
        Success = C(16, 124, 65),
        SuccessHover = C(13, 105, 55),
        SuccessPressed = C(10, 88, 46),
        SuccessSoft = C(225, 244, 234),
        SuccessInk = C(14, 100, 52),
        Warning = C(214, 126, 0),
        WarningInk = C(154, 91, 0),
        WarningSoft = C(253, 240, 217),
        Danger = C(196, 43, 28),
        DangerInk = C(179, 38, 30),
        DangerSoft = C(251, 229, 227),
        NeutralFill = C(255, 255, 255),
        NeutralHover = C(246, 248, 251),
        NeutralPressed = C(232, 236, 243),
        NeutralBorder = C(196, 205, 219),
        NeutralInk = C(22, 32, 46),
        DisabledFill = C(243, 245, 249),
        DisabledInk = C(160, 170, 186),
        HeaderFill = C(255, 255, 255),
        HeaderInk = C(22, 32, 46),
        HeaderMuted = C(96, 110, 130),
        HeaderFieldFill = C(243, 246, 250),
        HeaderFieldBorder = C(225, 230, 238),
        HeaderButtonFill = C(246, 248, 251),
        HeaderButtonHover = C(232, 236, 243),
        HeaderButtonPressed = C(221, 227, 237),
        HeaderButtonBorder = C(216, 223, 233),
        PillIdleFill = C(227, 239, 251),
        PillIdleInk = C(11, 83, 148),
        PillReadyFill = C(225, 244, 234),
        PillReadyInk = C(14, 100, 52),
        PillWarningFill = C(253, 240, 217),
        PillWarningInk = C(154, 91, 0),
        EditorFill = C(15, 23, 42),
        EditorInk = C(226, 232, 240),
        EditorTag = C(125, 211, 252),
        EditorString = C(190, 242, 100),
        EditorValue = C(167, 243, 208),
        CodeInk = C(11, 83, 148),
        NumberInk = C(14, 100, 52),
        DateInk = C(96, 110, 130),
        RowNumberInk = C(160, 170, 186)
    };

    public static ThemePalette Dark { get; } = new()
    {
        IsDark = true,
        Ink = C(238, 242, 248),
        InkSecondary = C(188, 199, 216),
        MutedInk = C(150, 163, 184),
        FaintInk = C(100, 112, 132),
        Surface = C(27, 33, 43),
        Canvas = C(18, 22, 29),
        Sunken = C(38, 46, 60),
        AltRow = C(32, 39, 51),
        FieldFill = C(18, 22, 29),
        HoverRow = C(30, 48, 72),
        Divider = C(42, 51, 66),
        DividerStrong = C(62, 74, 94),
        Brand = C(36, 130, 220),
        BrandHover = C(74, 163, 240),
        BrandPressed = C(22, 104, 184),
        BrandDeep = C(14, 70, 130),
        BrandSoft = C(22, 48, 75),
        BrandLite = C(30, 66, 106),
        BrandInk = C(142, 200, 248),
        Accent = C(107, 181, 245),
        Success = C(44, 170, 104),
        SuccessHover = C(76, 195, 138),
        SuccessPressed = C(34, 140, 84),
        SuccessSoft = C(18, 52, 37),
        SuccessInk = C(110, 214, 160),
        Warning = C(240, 181, 89),
        WarningInk = C(240, 181, 89),
        WarningSoft = C(58, 42, 14),
        Danger = C(232, 76, 62),
        DangerInk = C(255, 138, 128),
        DangerSoft = C(60, 26, 24),
        NeutralFill = C(38, 46, 60),
        NeutralHover = C(48, 58, 75),
        NeutralPressed = C(30, 37, 49),
        NeutralBorder = C(62, 74, 94),
        NeutralInk = C(238, 242, 248),
        DisabledFill = C(30, 36, 47),
        DisabledInk = C(100, 112, 132),
        HeaderFill = C(27, 33, 43),
        HeaderInk = C(238, 242, 248),
        HeaderMuted = C(150, 163, 184),
        HeaderFieldFill = C(18, 22, 29),
        HeaderFieldBorder = C(54, 65, 85),
        HeaderButtonFill = C(38, 46, 60),
        HeaderButtonHover = C(48, 58, 75),
        HeaderButtonPressed = C(30, 37, 49),
        HeaderButtonBorder = C(62, 74, 94),
        PillIdleFill = C(22, 48, 75),
        PillIdleInk = C(142, 200, 248),
        PillReadyFill = C(18, 52, 37),
        PillReadyInk = C(110, 214, 160),
        PillWarningFill = C(58, 42, 14),
        PillWarningInk = C(240, 181, 89),
        EditorFill = C(15, 23, 42),
        EditorInk = C(226, 232, 240),
        EditorTag = C(125, 211, 252),
        EditorString = C(190, 242, 100),
        EditorValue = C(167, 243, 208),
        CodeInk = C(142, 200, 248),
        NumberInk = C(110, 214, 160),
        DateInk = C(150, 163, 184),
        RowNumberInk = C(100, 112, 132)
    };
}
