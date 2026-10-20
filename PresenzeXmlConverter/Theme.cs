using System.Drawing.Drawing2D;

namespace PresenzeXmlConverter;

/// <summary>
/// Palette, tipografia e stili condivisi da tutta l'interfaccia.
/// Ogni colore è definito una sola volta e nominato per ruolo, così la
/// finestra principale e i dialoghi non mostrano mai tinte diverse per lo
/// stesso elemento (blu del pulsante primario, bordo delle card, testo
/// attenuato, stati disabilitati) e ogni nuovo controllo nasce coerente.
/// </summary>
internal static class Theme
{
    /// <summary>La tavolozza in uso: si sceglie una volta all'avvio (<see cref="Apply"/>) e i controlli leggono sempre da qui.</summary>
    public static ThemePalette Current { get; private set; } = ThemePalette.Light;

    public static bool IsDark => Current.IsDark;

    /// <summary>Sceglie il tema. Va chiamato prima di creare qualunque finestra o controllo: i colori si leggono alla creazione.</summary>
    public static void Apply(bool dark) => Current = dark ? ThemePalette.Dark : ThemePalette.Light;

    /// <summary>Sceglie la tavolozza indicata (anche «Vetro»). Stesso vincolo di <see cref="Apply(bool)"/>.</summary>
    public static void Apply(ThemePalette palette) => Current = palette;

    /// <summary>Tema «Vetro» attivo?</summary>
    public static bool IsGlass => Current.IsGlass;

    public static Color GlowColor => Current.GlowColor;
    public static Color GlassTop => Current.GlassTop;
    public static Color GlassBottom => Current.GlassBottom;
    public static Color GlassEdge => Current.GlassEdge;
    public static Color GlassEdgeDim => Current.GlassEdgeDim;

    // Colori aggiunti con il tema scuro (nel tema chiaro hanno gli stessi valori usati prima nel codice).
    public static Color HoverRow => Current.HoverRow;
    public static Color WarningSoft => Current.WarningSoft;
    public static Color DangerSoft => Current.DangerSoft;
    public static Color HeaderFieldFill => Current.HeaderFieldFill;
    public static Color HeaderFieldBorder => Current.HeaderFieldBorder;
    public static Color HeaderButtonFill => Current.HeaderButtonFill;
    public static Color HeaderButtonHover => Current.HeaderButtonHover;
    public static Color HeaderButtonPressed => Current.HeaderButtonPressed;
    public static Color HeaderButtonBorder => Current.HeaderButtonBorder;

    /* ------------------------------ Testo ------------------------------ */
    public static Color Ink => Current.Ink;
    public static Color InkSecondary => Current.InkSecondary;
    public static Color MutedInk => Current.MutedInk;
    public static Color FaintInk => Current.FaintInk;

    /* --------------------------- Superfici ----------------------------- */
    public static Color Surface => Current.Surface;
    public static Color Canvas => Current.Canvas;
    public static Color Sunken => Current.Sunken;
    public static Color AltRow => Current.AltRow;
    public static Color FieldFill => Current.FieldFill;

    /* ---------------------------- Bordi -------------------------------- */
    public static Color Divider => Current.Divider;
    public static Color DividerStrong => Current.DividerStrong;

    /* ------------------------- Marca (primario) ------------------------ */
    public static Color Brand => Current.Brand;
    public static Color BrandHover => Current.BrandHover;
    public static Color BrandPressed => Current.BrandPressed;
    public static Color BrandDeep => Current.BrandDeep;
    public static Color BrandSoft => Current.BrandSoft;
    public static Color BrandLite => Current.BrandLite;
    public static Color BrandInk => Current.BrandInk;
    public static Color Accent => Current.Accent;

    /* --------------------------- Esito positivo ------------------------ */
    public static Color Success => Current.Success;
    public static Color SuccessHover => Current.SuccessHover;
    public static Color SuccessPressed => Current.SuccessPressed;
    public static Color SuccessSoft => Current.SuccessSoft;
    public static Color SuccessInk => Current.SuccessInk;

    /* ---------------------------- Attenzione --------------------------- */
    public static Color Warning => Current.Warning;
    public static Color WarningInk => Current.WarningInk;

    /* ------------------------------ Errore ----------------------------- */
    public static Color Danger => Current.Danger;
    public static Color DangerInk => Current.DangerInk;

    /* ------------------------ Pulsanti neutri -------------------------- */
    public static Color NeutralFill => Current.NeutralFill;
    public static Color NeutralHover => Current.NeutralHover;
    public static Color NeutralPressed => Current.NeutralPressed;
    public static Color NeutralBorder => Current.NeutralBorder;
    public static Color NeutralInk => Current.NeutralInk;

    /* ----------------------- Stato disabilitato ------------------------ */
    public static Color DisabledFill => Current.DisabledFill;
    public static Color DisabledInk => Current.DisabledInk;

    /* --------------------------- Header scuro -------------------------- */
    public static Color HeaderFill => Current.HeaderFill;
    public static Color HeaderInk => Current.HeaderInk;
    public static Color HeaderMuted => Current.HeaderMuted;

    /* --------------------------- Badge in header ----------------------- */
    public static Color PillIdleFill => Current.PillIdleFill;
    public static Color PillIdleInk => Current.PillIdleInk;
    public static Color PillReadyFill => Current.PillReadyFill;
    public static Color PillReadyInk => Current.PillReadyInk;
    public static Color PillWarningFill => Current.PillWarningFill;
    public static Color PillWarningInk => Current.PillWarningInk;

    /* ------------------------ Editor XML scuro ------------------------- */
    public static Color EditorFill => Current.EditorFill;
    public static Color EditorInk => Current.EditorInk;
    public static Color EditorTag => Current.EditorTag;
    public static Color EditorString => Current.EditorString;
    public static Color EditorValue => Current.EditorValue;

    /* ----------------------- Colori della griglia ---------------------- */
    public static Color CodeInk => Current.CodeInk;
    public static Color NumberInk => Current.NumberInk;
    public static Color DateInk => Current.DateInk;
    public static Color RowNumberInk => Current.RowNumberInk;

    /// <summary>
    /// Font condivisi: creati una volta sola e nominati per uso, così le
    /// finestre non disperdono oggetti e la gerarchia tipografica resta
    /// identica ovunque. La famiglia monospaziata viene scelta fra quelle
    /// realmente presenti sul sistema (Cascadia non esiste su tutti).
    /// </summary>
    public static class Fonts
    {
        private const string Semibold = "Segoe UI Semibold";
        private static readonly string MonoFamily = ResolveMonoFamily();

        public static readonly Font Ui = new("Segoe UI", 10.5f);
        public static readonly Font HeaderTitle = new(Semibold, 21f);
        public static readonly Font CardTitle = new(Semibold, 17f);
        public static readonly Font Section = new(Semibold, 9f);
        public static readonly Font Small = new("Segoe UI", 9.8f);
        public static readonly Font SmallBold = new(Semibold, 9.8f);
        public static readonly Font Tiny = new("Segoe UI", 9.2f);
        public static readonly Font Step = new(Semibold, 9.5f);
        public static readonly Font Button = new(Semibold, 10.5f);
        public static readonly Font ButtonSmall = new(Semibold, 10f);
        public static readonly Font Pill = new(Semibold, 9f);
        public static readonly Font Metric = new(Semibold, 22f);
        public static readonly Font Stat = new(Semibold, 15f);
        public static readonly Font Kpi = new(Semibold, 19f);
        public static readonly Font Field = new(Semibold, 13f);
        public static readonly Font DialogTitle = new(Semibold, 13f);
        public static readonly Font Grid = new("Segoe UI", 10f);
        public static readonly Font GridHeader = new(Semibold, 9.5f);
        public static readonly Font Tab = new("Segoe UI", 10.5f);
        public static readonly Font TabBold = new(Semibold, 10.5f);
        public static readonly Font Number = new(Semibold, 10f);
        public static readonly Font Code = new(MonoFamily, 9.3f);
        public static readonly Font Mono = new(MonoFamily, 9.3f);
        public static readonly Font MonoSmall = new(MonoFamily, 9f);

        /// <summary>
        /// Sceglie la prima famiglia monospaziata disponibile. Si prova a creare
        /// direttamente la famiglia invece di enumerare TUTTI i font installati
        /// (InstalledFontCollection): l'enumerazione è lenta e avveniva una volta
        /// sola ma all'avvio, rallentando l'apertura della finestra.
        /// </summary>
        private static string ResolveMonoFamily()
        {
            foreach (var candidate in new[] { "Cascadia Mono", "Consolas", "Cascadia Code" })
            {
                try
                {
                    using var family = new FontFamily(candidate);
                    return candidate;
                }
                catch (ArgumentException)
                {
                    // Famiglia non installata: si passa alla successiva.
                }
            }
            return "Courier New";
        }
    }

    /* --------------------------- Pulsanti ------------------------------ */

    public enum ButtonTone
    {
        Primary,   // azione principale: blu pieno
        Positive,  // conferma di flusso: verde pieno
        Neutral,   // azione secondaria: fondo chiaro con bordo
        Danger,    // uscita / azione irreversibile
        OnDark     // azione secondaria sull'intestazione scura: fondo appena più chiaro, bordo sottile
    }

    private readonly record struct ButtonPalette(
        Color Fill, Color Hover, Color Pressed, Color Border, Color Ink, int BorderWidth);

    /// <summary>
    /// Applica lo stile pieno di un pulsante secondo il ruolo, incluse le
    /// varianti hover, premuto e disabilitato: senza queste ultime un
    /// pulsante disabilitato resta colorato e sembra ancora premibile.
    /// </summary>
    public static void StyleButton(Button button, ButtonTone tone, bool compact = false)
    {
        var palette = PaletteFor(tone);
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.BackColor = palette.Fill;
        button.ForeColor = palette.Ink;
        button.FlatAppearance.BorderSize = palette.BorderWidth;
        button.FlatAppearance.BorderColor = palette.Border;
        button.FlatAppearance.MouseOverBackColor = palette.Hover;
        button.FlatAppearance.MouseDownBackColor = palette.Pressed;
        button.Cursor = Cursors.Hand;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Font = compact ? Fonts.ButtonSmall : Fonts.Button;
        button.AutoSize = false;
        button.MinimumSize = compact ? new Size(84, 30) : new Size(110, 36);
        button.Size = compact ? new Size(Math.Max(button.Width, 110), 32) : button.Size;
        button.Padding = new Padding(compact ? 10 : 14, 0, compact ? 10 : 14, 0);
        button.Margin = Padding.Empty;
        button.Tag = palette;   // palette di riferimento per ripristinare gli stati
        button.EnabledChanged -= OnButtonEnabledChanged;
        button.EnabledChanged += OnButtonEnabledChanged;
        button.TextChanged -= OnButtonTextChanged;
        button.TextChanged += OnButtonTextChanged;
        ApplyButtonColors(button, button.Enabled);
        FitButtonToText(button);
    }

    private static void OnButtonTextChanged(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            FitButtonToText(button);
        }
    }

    /// <summary>
    /// Allarga il pulsante quanto serve al suo testo (con il riempimento interno e il
    /// bordo): una larghezza fissa di 110 px tagliava «Salva abbinamenti», «Copia
    /// percorso» e ogni etichetta più lunga. Il minimo di larghezza resta quello del
    /// ruolo, quindi i pulsanti corti non cambiano.
    /// </summary>
    public static void FitButtonToText(Button button)
    {
        if (string.IsNullOrEmpty(button.Text))
        {
            return;
        }
        var textWidth = TextRenderer.MeasureText(
            button.Text,
            button.Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        // Con un'icona serve anche il suo posto (come nel disegno del pulsante: icona + 7 px di distanza).
        var iconRoom = button is ThemedButton { Icon: not null } ? (button.Height >= 32 ? 16 : 14) + 7 : 0;
        var needed = textWidth + iconRoom + button.Padding.Horizontal + (button.FlatAppearance.BorderSize * 2) + 16;
        if (needed > button.MinimumSize.Width)
        {
            button.MinimumSize = new Size(needed, button.MinimumSize.Height);
        }
        if (button.Width < needed)
        {
            button.Width = needed;
        }
    }

    /// <summary>Crea un pulsante già temizzato (nessuna Personalizzazione rimessa a mano).</summary>
    public static Button CreateButton(string text, ButtonTone tone, bool compact = false)
    {
        var button = new ThemedButton { Text = text };
        StyleButton(button, tone, compact);
        return button;
    }

    private static void OnButtonEnabledChanged(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            ApplyButtonColors(button, button.Enabled);
        }
    }

    private static void ApplyButtonColors(Button button, bool enabled)
    {
        if (button.Tag is not ButtonPalette palette)
        {
            return;
        }
        if (enabled)
        {
            button.BackColor = palette.Fill;
            button.ForeColor = palette.Ink;
            button.FlatAppearance.BorderColor = palette.Border;
            button.FlatAppearance.MouseOverBackColor = palette.Hover;
            button.FlatAppearance.MouseDownBackColor = palette.Pressed;
        }
        else
        {
            // Grigio pieno e testo smorto: lo stato si legge a colpo d'occhio.
            button.BackColor = DisabledFill;
            button.ForeColor = DisabledInk;
            button.FlatAppearance.BorderColor = Divider;
            button.FlatAppearance.MouseOverBackColor = DisabledFill;
            button.FlatAppearance.MouseDownBackColor = DisabledFill;
        }
    }

    private static ButtonPalette PaletteFor(ButtonTone tone) => tone switch
    {
        ButtonTone.Primary => new ButtonPalette(Brand, BrandHover, BrandPressed, Brand, Color.White, 0),
        ButtonTone.Positive => new ButtonPalette(Success, SuccessHover, SuccessPressed, Success, Color.White, 0),
        ButtonTone.Danger => new ButtonPalette(Danger, Shade(Danger, 0.10f), Shade(Danger, 0.20f), Danger, Color.White, 0),
        ButtonTone.OnDark => new ButtonPalette(
            HeaderButtonFill, HeaderButtonHover, HeaderButtonPressed,
            HeaderButtonBorder, Ink, 1),
        _ => new ButtonPalette(NeutralFill, NeutralHover, NeutralPressed, NeutralBorder, NeutralInk, 1)
    };

    /* ------------------------- Controlli di base ----------------------- */

    /// <summary>Lo stesso colore più scuro di <paramref name="amount"/> (0–1): per gli stati sotto il mouse e premuto.</summary>
    public static Color Shade(Color color, float amount)
    {
        var factor = 1f - Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(color.A, (int)(color.R * factor), (int)(color.G * factor), (int)(color.B * factor));
    }

    /// <summary>Uniforma l'aspetto delle caselle combinate dei dialoghi.</summary>
    public static void StyleCombo(ComboBox combo)
    {
        combo.Font = Fonts.Ui;
        combo.BackColor = Surface;
        combo.ForeColor = Ink;
        combo.FlatStyle = FlatStyle.Flat;
        combo.Cursor = Cursors.Hand;
    }

    /// <summary>Chrome comune di ogni finestra di dialogo.</summary>
    public static void StyleDialog(Form form)
    {
        form.Font = Fonts.Ui;
        form.BackColor = Canvas;
        form.ForeColor = Ink;
        form.KeyPreview = true;
    }

    /// <summary>
    /// Percorso di un rettangolo arrotondato: condiviso da card, badge e
    /// campi di testo perché gli angoli restino identici in tutta l'app.
    /// </summary>
    public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width < 1 || bounds.Height < 1)
        {
            return path;
        }
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
