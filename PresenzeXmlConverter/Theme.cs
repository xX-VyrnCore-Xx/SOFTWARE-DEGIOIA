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
    public static readonly Color Ink = Color.FromArgb(29, 29, 31);             // titoli, testo principale
    public static readonly Color InkSecondary = Color.FromArgb(60, 60, 67);   // corpo del testo
    public static readonly Color MutedInk = Color.FromArgb(110, 110, 115);     // testo di supporto
    public static readonly Color FaintInk = Color.FromArgb(174, 174, 178);     // numerazioni, placeholder

    /* --------------------------- Superfici ----------------------------- */
    public static readonly Color Surface = Color.White;                        // fondo delle card
    public static readonly Color Canvas = Color.FromArgb(245, 245, 247);       // fondo della finestra
    public static readonly Color Sunken = Color.FromArgb(236, 236, 241);       // fasce e intestazioni di griglia
    public static readonly Color AltRow = Color.FromArgb(249, 249, 251);       // righe alternate della griglia
    public static readonly Color FieldFill = Color.FromArgb(255, 255, 255);    // fondo dei campi di testo

    /* ---------------------------- Bordi -------------------------------- */
    public static readonly Color Divider = Color.FromArgb(229, 229, 234);      // linee sottili
    public static readonly Color DividerStrong = Color.FromArgb(209, 209, 214);// bordi dei campi

    /* ------------------------- Marca (primario) ------------------------ */
    public static readonly Color Brand = Color.FromArgb(0, 122, 255);
    public static readonly Color BrandHover = Color.FromArgb(0, 106, 230);
    public static readonly Color BrandPressed = Color.FromArgb(0, 88, 200);
    public static readonly Color BrandDeep = Color.FromArgb(0, 64, 148);      // fondo dei badge scuri
    public static readonly Color BrandSoft = Color.FromArgb(232, 242, 255);    // fondo bottoni secondari
    public static readonly Color BrandLite = Color.FromArgb(207, 228, 255);    // selezione in griglia
    public static readonly Color BrandInk = Color.FromArgb(0, 98, 204);       // testo su fondo chiaro
    public static readonly Color Accent = Color.FromArgb(90, 200, 250);         // accento secondario (ciano) dei gradienti

    /* --------------------------- Esito positivo ------------------------ */
    public static readonly Color Success = Color.FromArgb(52, 199, 89);
    public static readonly Color SuccessHover = Color.FromArgb(40, 176, 74);
    public static readonly Color SuccessPressed = Color.FromArgb(30, 150, 62);
    public static readonly Color SuccessSoft = Color.FromArgb(226, 247, 232);
    public static readonly Color SuccessInk = Color.FromArgb(24, 110, 48);

    /* ---------------------------- Attenzione --------------------------- */
    public static readonly Color Warning = Color.FromArgb(255, 149, 0);
    public static readonly Color WarningInk = Color.FromArgb(158, 88, 0);

    /* ------------------------------ Errore ----------------------------- */
    public static readonly Color Danger = Color.FromArgb(255, 59, 48);
    public static readonly Color DangerInk = Color.FromArgb(176, 28, 20);

    /* ------------------------ Pulsanti neutri -------------------------- */
    public static readonly Color NeutralFill = Color.FromArgb(255, 255, 255);
    public static readonly Color NeutralHover = Color.FromArgb(245, 245, 247);
    public static readonly Color NeutralPressed = Color.FromArgb(232, 232, 237);
    public static readonly Color NeutralBorder = Color.FromArgb(209, 209, 214);
    public static readonly Color NeutralInk = Color.FromArgb(29, 29, 31);

    /* ----------------------- Stato disabilitato ------------------------ */
    public static readonly Color DisabledFill = Color.FromArgb(244, 244, 246);
    public static readonly Color DisabledInk = Color.FromArgb(174, 174, 178);

    /* --------------------------- Header scuro -------------------------- */
    public static readonly Color HeaderFill = Color.FromArgb(255, 255, 255);
    public static readonly Color HeaderInk = Color.FromArgb(29, 29, 31);
    public static readonly Color HeaderMuted = Color.FromArgb(110, 110, 115);

    /* --------------------------- Badge in header ----------------------- */
    public static readonly Color PillIdleFill = Color.FromArgb(232, 242, 255);
    public static readonly Color PillIdleInk = Color.FromArgb(0, 98, 204);
    public static readonly Color PillReadyFill = Color.FromArgb(226, 247, 232);
    public static readonly Color PillReadyInk = Color.FromArgb(24, 110, 48);
    public static readonly Color PillWarningFill = Color.FromArgb(255, 243, 224);
    public static readonly Color PillWarningInk = Color.FromArgb(158, 88, 0);

    /* ------------------------ Editor XML scuro ------------------------- */
    public static readonly Color EditorFill = Color.FromArgb(15, 23, 42);
    public static readonly Color EditorInk = Color.FromArgb(226, 232, 240);
    public static readonly Color EditorTag = Color.FromArgb(125, 211, 252);
    public static readonly Color EditorString = Color.FromArgb(190, 242, 100);
    public static readonly Color EditorValue = Color.FromArgb(167, 243, 208);

    /* ----------------------- Colori della griglia ---------------------- */
    public static readonly Color CodeInk = Color.FromArgb(0, 98, 204);        // codici PAGHE
    public static readonly Color NumberInk = Color.FromArgb(24, 110, 48);       // ore e minuti
    public static readonly Color DateInk = Color.FromArgb(110, 110, 115);       // date
    public static readonly Color RowNumberInk = Color.FromArgb(174, 174, 178);

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

        public static readonly Font Ui = new("Segoe UI", 10f);
        public static readonly Font HeaderTitle = new(Semibold, 21f);
        public static readonly Font CardTitle = new(Semibold, 17f);
        public static readonly Font Section = new(Semibold, 8.2f);
        public static readonly Font Small = new("Segoe UI", 8.7f);
        public static readonly Font SmallBold = new(Semibold, 8.7f);
        public static readonly Font Tiny = new("Segoe UI", 8.4f);
        public static readonly Font Step = new(Semibold, 8.6f);
        public static readonly Font Button = new(Semibold, 9.5f);
        public static readonly Font ButtonSmall = new(Semibold, 9f);
        public static readonly Font Pill = new(Semibold, 8.2f);
        public static readonly Font Metric = new(Semibold, 22f);
        public static readonly Font Stat = new(Semibold, 15f);
        public static readonly Font Kpi = new(Semibold, 19f);
        public static readonly Font Field = new(Semibold, 13f);
        public static readonly Font DialogTitle = new(Semibold, 13f);
        public static readonly Font Grid = new("Segoe UI", 8.9f);
        public static readonly Font GridHeader = new(Semibold, 8.6f);
        public static readonly Font Tab = new("Segoe UI", 9f);
        public static readonly Font TabBold = new(Semibold, 9f);
        public static readonly Font Number = new(Semibold, 9f);
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
        ButtonTone.Danger => new ButtonPalette(Danger, Color.FromArgb(230, 50, 40), Color.FromArgb(200, 40, 32), Danger, Color.White, 0),
        ButtonTone.OnDark => new ButtonPalette(
            Color.FromArgb(242, 242, 247), Color.FromArgb(229, 229, 234), Color.FromArgb(216, 216, 222),
            Color.FromArgb(222, 222, 228), Ink, 1),
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
