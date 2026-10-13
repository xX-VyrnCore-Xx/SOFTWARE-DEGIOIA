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
    /* ------------------------------ Testo ------------------------------ */
    public static readonly Color Ink = Color.FromArgb(15, 26, 40);             // titoli, testo principale
    public static readonly Color InkSecondary = Color.FromArgb(55, 75, 100);   // corpo del testo
    public static readonly Color MutedInk = Color.FromArgb(103, 120, 142);     // testo di supporto
    public static readonly Color FaintInk = Color.FromArgb(148, 163, 184);     // numerazioni, placeholder

    /* --------------------------- Superfici ----------------------------- */
    public static readonly Color Surface = Color.White;                        // fondo delle card
    public static readonly Color Canvas = Color.FromArgb(246, 248, 252);       // fondo della finestra
    public static readonly Color Sunken = Color.FromArgb(240, 243, 249);       // fasce e intestazioni di griglia
    public static readonly Color AltRow = Color.FromArgb(249, 250, 253);       // righe alternate della griglia
    public static readonly Color FieldFill = Color.FromArgb(250, 251, 254);    // fondo dei campi di testo

    /* ---------------------------- Bordi -------------------------------- */
    public static readonly Color Divider = Color.FromArgb(227, 232, 240);      // linee sottili
    public static readonly Color DividerStrong = Color.FromArgb(203, 213, 225);// bordi dei campi

    /* ------------------------- Marca (primario) ------------------------ */
    public static readonly Color Brand = Color.FromArgb(37, 99, 235);
    public static readonly Color BrandHover = Color.FromArgb(29, 78, 216);
    public static readonly Color BrandPressed = Color.FromArgb(23, 63, 180);
    public static readonly Color BrandDeep = Color.FromArgb(30, 58, 138);      // fondo dei badge scuri
    public static readonly Color BrandSoft = Color.FromArgb(239, 246, 255);    // fondo bottoni secondari
    public static readonly Color BrandLite = Color.FromArgb(219, 234, 254);    // selezione in griglia
    public static readonly Color BrandInk = Color.FromArgb(30, 64, 175);       // testo su fondo chiaro
    public static readonly Color Accent = Color.FromArgb(8, 145, 178);         // accento secondario (ciano) dei gradienti

    /* --------------------------- Esito positivo ------------------------ */
    public static readonly Color Success = Color.FromArgb(22, 163, 74);
    public static readonly Color SuccessHover = Color.FromArgb(21, 128, 61);
    public static readonly Color SuccessPressed = Color.FromArgb(18, 101, 51);
    public static readonly Color SuccessSoft = Color.FromArgb(220, 252, 231);
    public static readonly Color SuccessInk = Color.FromArgb(22, 101, 52);

    /* ---------------------------- Attenzione --------------------------- */
    public static readonly Color Warning = Color.FromArgb(217, 119, 6);
    public static readonly Color WarningInk = Color.FromArgb(146, 64, 14);

    /* ------------------------------ Errore ----------------------------- */
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);
    public static readonly Color DangerInk = Color.FromArgb(153, 27, 27);

    /* ------------------------ Pulsanti neutri -------------------------- */
    public static readonly Color NeutralFill = Color.FromArgb(238, 242, 247);
    public static readonly Color NeutralHover = Color.FromArgb(226, 232, 240);
    public static readonly Color NeutralPressed = Color.FromArgb(214, 222, 234);
    public static readonly Color NeutralBorder = Color.FromArgb(214, 222, 234);
    public static readonly Color NeutralInk = Color.FromArgb(51, 65, 85);

    /* ----------------------- Stato disabilitato ------------------------ */
    public static readonly Color DisabledFill = Color.FromArgb(241, 244, 248);
    public static readonly Color DisabledInk = Color.FromArgb(163, 177, 196);

    /* --------------------------- Header scuro -------------------------- */
    public static readonly Color HeaderFill = Color.FromArgb(15, 26, 44);
    public static readonly Color HeaderInk = Color.White;
    public static readonly Color HeaderMuted = Color.FromArgb(163, 186, 219);

    /* --------------------------- Badge in header ----------------------- */
    public static readonly Color PillIdleFill = Color.FromArgb(30, 58, 138);
    public static readonly Color PillIdleInk = Color.FromArgb(191, 219, 254);
    public static readonly Color PillReadyFill = Color.FromArgb(20, 83, 45);
    public static readonly Color PillReadyInk = Color.FromArgb(187, 247, 208);
    public static readonly Color PillWarningFill = Color.FromArgb(120, 53, 15);
    public static readonly Color PillWarningInk = Color.FromArgb(253, 230, 138);

    /* ------------------------ Editor XML scuro ------------------------- */
    public static readonly Color EditorFill = Color.FromArgb(15, 23, 42);
    public static readonly Color EditorInk = Color.FromArgb(226, 232, 240);
    public static readonly Color EditorTag = Color.FromArgb(125, 211, 252);
    public static readonly Color EditorString = Color.FromArgb(190, 242, 100);
    public static readonly Color EditorValue = Color.FromArgb(167, 243, 208);

    /* ----------------------- Colori della griglia ---------------------- */
    public static readonly Color CodeInk = Color.FromArgb(29, 78, 216);        // codici PAGHE
    public static readonly Color NumberInk = Color.FromArgb(19, 88, 74);       // ore e minuti
    public static readonly Color DateInk = Color.FromArgb(82, 102, 127);       // date
    public static readonly Color RowNumberInk = Color.FromArgb(148, 163, 184);

    /// <summary>
    /// Font condivisi: creati una volta sola e nominati per uso, così le
    /// finestre non disperdono oggetti e la gerarchia tipografica resta
    /// identica ovunque. La famiglia monospaziata viene scelta fra quelle
    /// realmente presenti sul sistema (Cascadia non esiste su tutti).
    /// </summary>
    public static class Fonts
    {
        private static readonly string MonoFamily = ResolveMonoFamily();

        public static readonly Font Ui = new("Segoe UI", 10f);
        public static readonly Font HeaderTitle = new("Segoe UI", 23f, FontStyle.Bold);
        public static readonly Font CardTitle = new("Segoe UI", 17f, FontStyle.Bold);
        public static readonly Font Section = new("Segoe UI", 8.2f, FontStyle.Bold);
        public static readonly Font Small = new("Segoe UI", 8.7f);
        public static readonly Font SmallBold = new("Segoe UI", 8.7f, FontStyle.Bold);
        public static readonly Font Tiny = new("Segoe UI", 8.4f);
        public static readonly Font Step = new("Segoe UI", 8.6f, FontStyle.Bold);
        public static readonly Font Button = new("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font ButtonSmall = new("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font Pill = new("Segoe UI", 8.2f, FontStyle.Bold);
        public static readonly Font Metric = new("Segoe UI", 22f, FontStyle.Bold);
        public static readonly Font Field = new("Segoe UI", 13f, FontStyle.Bold);
        public static readonly Font DialogTitle = new("Segoe UI", 13f, FontStyle.Bold);
        public static readonly Font Grid = new("Segoe UI", 8.9f);
        public static readonly Font GridHeader = new("Segoe UI", 8.6f, FontStyle.Bold);
        public static readonly Font Tab = new("Segoe UI", 9f);
        public static readonly Font TabBold = new("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font Number = new("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font Code = new(MonoFamily, 8.4f);
        public static readonly Font Mono = new(MonoFamily, 9.3f);
        public static readonly Font MonoSmall = new(MonoFamily, 8.2f);

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
        ApplyButtonColors(button, button.Enabled);
    }

    /// <summary>Crea un pulsante già temizzato (nessuna Personalizzazione rimessa a mano).</summary>
    public static Button CreateButton(string text, ButtonTone tone, bool compact = false)
    {
        var button = new Button { Text = text };
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
        ButtonTone.Danger => new ButtonPalette(Danger, Color.FromArgb(185, 28, 28), Color.FromArgb(153, 27, 27), Danger, Color.White, 0),
        ButtonTone.OnDark => new ButtonPalette(
            Color.FromArgb(28, 44, 72), Color.FromArgb(42, 64, 102), Color.FromArgb(22, 35, 58),
            Color.FromArgb(70, 96, 140), Color.White, 1),
        _ => new ButtonPalette(NeutralFill, NeutralHover, NeutralPressed, NeutralBorder, NeutralInk, 1)
    };

    /* ------------------------- Controlli di base ----------------------- */

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
