using System.Drawing;

namespace PresenzeXmlConverter;

/// <summary>Come si sceglie il tema: segue Windows, sempre chiaro o sempre scuro.</summary>
internal enum ThemeMode
{
    System,
    Light,
    Dark,
    Glass
}

/// <summary>Scelta del tema: lettura/scrittura del testo salvato e risoluzione in chiaro o scuro. Solo calcolo.</summary>
internal static class ThemeChoice
{
    public static ThemeMode Parse(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "light" => ThemeMode.Light,
        "dark" => ThemeMode.Dark,
        "glass" => ThemeMode.Glass,
        _ => ThemeMode.System
    };

    public static string Serialize(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => "light",
        ThemeMode.Dark => "dark",
        ThemeMode.Glass => "glass",
        _ => "system"
    };

    public static bool ResolveDark(ThemeMode mode, bool systemIsDark) => mode switch
    {
        ThemeMode.Dark => true,
        ThemeMode.Glass => true,
        ThemeMode.Light => false,
        _ => systemIsDark
    };

    /// <summary>La tavolozza da usare: «Vetro» ha la sua, «Come Windows» sceglie fra chiara e scura.</summary>
    public static ThemePalette PaletteFor(ThemeMode mode, bool systemIsDark) =>
        mode == ThemeMode.Glass
            ? ThemePalette.Glass
            : ResolveDark(mode, systemIsDark) ? ThemePalette.Dark : ThemePalette.Light;
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

    /// <summary>Tema «Vetro»: superfici a gradiente con bordo luminoso, pulsanti con bagliore, colori vivi.</summary>
    public bool IsGlass { get; init; }

    // Vetro (negli altri temi hanno valori neutri e non vengono usati)
    public Color GlowColor { get; init; }
    public Color GlassTop { get; init; }
    public Color GlassBottom { get; init; }
    public Color GlassEdge { get; init; }
    public Color GlassEdgeDim { get; init; }

    // Testo
    public Color Ink { get; init; }
    public Color InkSecondary { get; init; }
    public Color MutedInk { get; init; }
    public Color FaintInk { get; init; }

    // Superfici
    public Color Surface { get; init; }
    public Color Canvas { get; init; }
    public Color Sunken { get; init; }
    /// <summary>Fondo della barra laterale (più scuro della tela, come la barra dei pannelli di macOS).</summary>
    public Color Sidebar { get; init; }
    /// <summary>Voce della barra laterale sotto il mouse.</summary>
    public Color SidebarHover { get; init; }
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
        GlowColor = C(0, 122, 255),
        GlassTop = C(255, 255, 255),
        GlassBottom = C(255, 255, 255),
        GlassEdge = C(226, 226, 232),
        GlassEdgeDim = C(226, 226, 232),
        // Stile Windows 11 / Office: grigi neutri, testo quasi nero, blu di sistema, bordi netti.
        Ink = C(29, 29, 31),
        InkSecondary = C(60, 60, 67),
        MutedInk = C(99, 99, 104),
        FaintInk = C(142, 142, 147),
        Surface = C(255, 255, 255),
        Canvas = C(246, 246, 248),
        Sunken = C(238, 238, 243),
        Sidebar = C(235, 235, 241),
        SidebarHover = C(225, 225, 233),
        AltRow = C(250, 250, 252),
        FieldFill = C(255, 255, 255),
        HoverRow = C(236, 244, 255),
        Divider = C(226, 226, 232),
        DividerStrong = C(190, 190, 198),
        Brand = C(0, 122, 255),
        BrandHover = C(0, 105, 224),
        BrandPressed = C(0, 88, 196),
        BrandDeep = C(0, 64, 153),
        BrandSoft = C(226, 238, 255),
        BrandLite = C(200, 224, 255),
        BrandInk = C(0, 98, 214),
        Accent = C(10, 132, 255),
        Success = C(40, 160, 70),
        SuccessHover = C(34, 140, 60),
        SuccessPressed = C(28, 118, 50),
        SuccessSoft = C(226, 246, 232),
        SuccessInk = C(24, 116, 48),
        Warning = C(230, 130, 0),
        WarningInk = C(150, 84, 0),
        WarningSoft = C(255, 241, 214),
        Danger = C(235, 52, 40),
        DangerInk = C(190, 30, 22),
        DangerSoft = C(255, 233, 231),
        NeutralFill = C(255, 255, 255),
        NeutralHover = C(246, 246, 249),
        NeutralPressed = C(234, 234, 240),
        NeutralBorder = C(190, 190, 198),
        NeutralInk = C(29, 29, 31),
        DisabledFill = C(244, 244, 247),
        DisabledInk = C(160, 160, 168),
        HeaderFill = C(255, 255, 255),
        HeaderInk = C(29, 29, 31),
        HeaderMuted = C(99, 99, 104),
        HeaderFieldFill = C(238, 238, 243),
        HeaderFieldBorder = C(230, 230, 236),
        HeaderButtonFill = C(250, 250, 252),
        HeaderButtonHover = C(240, 240, 246),
        HeaderButtonPressed = C(230, 230, 238),
        HeaderButtonBorder = C(210, 210, 218),
        PillIdleFill = C(226, 238, 255),
        PillIdleInk = C(0, 98, 214),
        PillReadyFill = C(226, 246, 232),
        PillReadyInk = C(24, 116, 48),
        PillWarningFill = C(255, 241, 214),
        PillWarningInk = C(150, 84, 0),
        EditorFill = C(30, 30, 30),
        EditorInk = C(228, 228, 228),
        EditorTag = C(125, 211, 252),
        EditorString = C(190, 242, 100),
        EditorValue = C(167, 243, 208),
        CodeInk = C(0, 98, 214),
        NumberInk = C(24, 116, 48),
        DateInk = C(99, 99, 104),
        RowNumberInk = C(150, 150, 156)
    };

    public static ThemePalette Dark { get; } = new()
    {
        IsDark = true,
        GlowColor = C(36, 130, 220),
        GlassTop = C(27, 33, 43),
        GlassBottom = C(27, 33, 43),
        GlassEdge = C(42, 51, 66),
        GlassEdgeDim = C(42, 51, 66),
        Ink = C(238, 242, 248),
        InkSecondary = C(188, 199, 216),
        MutedInk = C(150, 163, 184),
        FaintInk = C(100, 112, 132),
        Surface = C(27, 33, 43),
        Canvas = C(18, 22, 29),
        Sunken = C(38, 46, 60),
        Sidebar = C(23, 28, 37),
        SidebarHover = C(34, 42, 55),
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

    public static ThemePalette Glass { get; } = new()
    {
        IsDark = true,
        IsGlass = true,
        GlowColor = C(139, 92, 246),
        GlassTop = C(32, 28, 54),
        GlassBottom = C(23, 20, 40),
        GlassEdge = C(130, 112, 190),
        GlassEdgeDim = C(52, 46, 84),
        Ink = C(243, 240, 255),
        InkSecondary = C(206, 200, 230),
        MutedInk = C(160, 152, 192),
        FaintInk = C(110, 102, 142),
        Surface = C(26, 23, 44),
        Canvas = C(13, 11, 22),
        Sunken = C(34, 30, 56),
        Sidebar = C(19, 16, 33),
        SidebarHover = C(34, 30, 56),
        AltRow = C(30, 27, 49),
        FieldFill = C(17, 15, 29),
        HoverRow = C(42, 36, 72),
        Divider = C(46, 41, 72),
        DividerStrong = C(74, 66, 110),
        Brand = C(139, 92, 246),
        BrandHover = C(167, 139, 250),
        BrandPressed = C(109, 40, 217),
        BrandDeep = C(76, 29, 149),
        BrandSoft = C(44, 32, 86),
        BrandLite = C(64, 46, 122),
        BrandInk = C(196, 181, 253),
        Accent = C(34, 211, 238),
        Success = C(52, 211, 153),
        SuccessHover = C(110, 231, 183),
        SuccessPressed = C(16, 185, 129),
        SuccessSoft = C(16, 58, 48),
        SuccessInk = C(110, 231, 183),
        Warning = C(251, 191, 36),
        WarningInk = C(251, 191, 36),
        WarningSoft = C(62, 46, 12),
        Danger = C(244, 63, 94),
        DangerInk = C(253, 164, 175),
        DangerSoft = C(70, 22, 38),
        NeutralFill = C(38, 34, 64),
        NeutralHover = C(50, 44, 80),
        NeutralPressed = C(30, 27, 50),
        NeutralBorder = C(84, 74, 126),
        NeutralInk = C(243, 240, 255),
        DisabledFill = C(28, 25, 44),
        DisabledInk = C(106, 98, 138),
        HeaderFill = C(22, 19, 38),
        HeaderInk = C(243, 240, 255),
        HeaderMuted = C(160, 152, 192),
        HeaderFieldFill = C(14, 12, 24),
        HeaderFieldBorder = C(64, 56, 100),
        HeaderButtonFill = C(38, 34, 64),
        HeaderButtonHover = C(50, 44, 80),
        HeaderButtonPressed = C(30, 27, 50),
        HeaderButtonBorder = C(84, 74, 126),
        PillIdleFill = C(44, 32, 86),
        PillIdleInk = C(196, 181, 253),
        PillReadyFill = C(16, 58, 48),
        PillReadyInk = C(110, 231, 183),
        PillWarningFill = C(62, 46, 12),
        PillWarningInk = C(251, 191, 36),
        EditorFill = C(12, 10, 24),
        EditorInk = C(226, 232, 240),
        EditorTag = C(196, 181, 253),
        EditorString = C(110, 231, 183),
        EditorValue = C(125, 211, 252),
        CodeInk = C(196, 181, 253),
        NumberInk = C(110, 231, 183),
        DateInk = C(160, 152, 192),
        RowNumberInk = C(110, 102, 142)
    };
}
