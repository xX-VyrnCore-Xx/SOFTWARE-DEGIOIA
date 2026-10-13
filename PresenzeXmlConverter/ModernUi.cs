using System.Drawing.Drawing2D;

namespace PresenzeXmlConverter;

/// <summary>Icone vettoriali disegnate con qualche tratto: nessuna immagine esterna, nitide a ogni DPI.</summary>
internal enum Glyph
{
    Search,
    Gear,
    Refresh,
    Upload,
    File,
    Check,
    Clock,
    Database,
    Shield,
    Arrow,
    Folder,
    Alert,
    Table,
    Code,
    Chip,
    Users,
    Sum,
    Download
}

/// <summary>
/// Libreria delle icone dell'interfaccia. Ogni icona sta in un quadrato (di solito
/// 16-20 px) ed è disegnata con una penna a estremità tonde, così le linee restano
/// leggibili sia sul fondo chiaro sia su quello scuro.
/// </summary>
internal static class Glyphs
{
    public static void Draw(Graphics graphics, Rectangle box, Glyph glyph, Color ink, float stroke = 1.6f)
    {
        if (box.Width < 6 || box.Height < 6)
        {
            return;
        }
        var previous = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(ink, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(ink);
        var s = Math.Min(box.Width, box.Height);
        var x = box.Left + ((box.Width - s) / 2f);
        var y = box.Top + ((box.Height - s) / 2f);
        PointF P(float fx, float fy) => new(x + (fx * s), y + (fy * s));
        RectangleF R(float fx, float fy, float fw, float fh) => new(x + (fx * s), y + (fy * s), fw * s, fh * s);

        switch (glyph)
        {
            case Glyph.Search:
                graphics.DrawEllipse(pen, R(0.08f, 0.08f, 0.62f, 0.62f));
                graphics.DrawLine(pen, P(0.62f, 0.62f), P(0.92f, 0.92f));
                break;

            case Glyph.Gear:
                graphics.DrawEllipse(pen, R(0.3f, 0.3f, 0.4f, 0.4f));
                for (var tooth = 0; tooth < 8; tooth++)
                {
                    var angle = tooth * Math.PI / 4;
                    var inner = 0.34f;
                    var outer = 0.47f;
                    graphics.DrawLine(
                        pen,
                        P(0.5f + (float)(Math.Cos(angle) * inner), 0.5f + (float)(Math.Sin(angle) * inner)),
                        P(0.5f + (float)(Math.Cos(angle) * outer), 0.5f + (float)(Math.Sin(angle) * outer)));
                }
                break;

            case Glyph.Refresh:
                graphics.DrawArc(pen, R(0.12f, 0.12f, 0.76f, 0.76f), -40, 270);
                graphics.DrawLine(pen, P(0.82f, 0.04f), P(0.82f, 0.30f));
                graphics.DrawLine(pen, P(0.82f, 0.30f), P(0.56f, 0.30f));
                break;

            case Glyph.Upload:
                graphics.DrawLine(pen, P(0.5f, 0.64f), P(0.5f, 0.1f));
                graphics.DrawLine(pen, P(0.3f, 0.3f), P(0.5f, 0.1f));
                graphics.DrawLine(pen, P(0.7f, 0.3f), P(0.5f, 0.1f));
                graphics.DrawLines(pen, [P(0.12f, 0.6f), P(0.12f, 0.9f), P(0.88f, 0.9f), P(0.88f, 0.6f)]);
                break;

            case Glyph.File:
                graphics.DrawLines(pen, [P(0.2f, 0.06f), P(0.62f, 0.06f), P(0.82f, 0.26f), P(0.82f, 0.94f), P(0.2f, 0.94f), P(0.2f, 0.06f)]);
                graphics.DrawLines(pen, [P(0.62f, 0.06f), P(0.62f, 0.26f), P(0.82f, 0.26f)]);
                graphics.DrawLine(pen, P(0.34f, 0.52f), P(0.68f, 0.52f));
                graphics.DrawLine(pen, P(0.34f, 0.72f), P(0.68f, 0.72f));
                break;

            case Glyph.Check:
                graphics.DrawLines(pen, [P(0.14f, 0.54f), P(0.4f, 0.78f), P(0.86f, 0.24f)]);
                break;

            case Glyph.Clock:
                graphics.DrawEllipse(pen, R(0.08f, 0.08f, 0.84f, 0.84f));
                graphics.DrawLines(pen, [P(0.5f, 0.26f), P(0.5f, 0.5f), P(0.68f, 0.6f)]);
                break;

            case Glyph.Database:
                graphics.DrawEllipse(pen, R(0.16f, 0.08f, 0.68f, 0.26f));
                graphics.DrawArc(pen, R(0.16f, 0.34f, 0.68f, 0.26f), 0, 180);
                graphics.DrawArc(pen, R(0.16f, 0.58f, 0.68f, 0.26f), 0, 180);
                graphics.DrawLine(pen, P(0.16f, 0.21f), P(0.16f, 0.71f));
                graphics.DrawLine(pen, P(0.84f, 0.21f), P(0.84f, 0.71f));
                break;

            case Glyph.Shield:
                graphics.DrawLines(pen, [P(0.5f, 0.06f), P(0.86f, 0.2f), P(0.82f, 0.56f), P(0.5f, 0.94f), P(0.18f, 0.56f), P(0.14f, 0.2f), P(0.5f, 0.06f)]);
                graphics.DrawLines(pen, [P(0.34f, 0.5f), P(0.46f, 0.62f), P(0.68f, 0.38f)]);
                break;

            case Glyph.Arrow:
                graphics.DrawLine(pen, P(0.1f, 0.5f), P(0.88f, 0.5f));
                graphics.DrawLines(pen, [P(0.6f, 0.22f), P(0.88f, 0.5f), P(0.6f, 0.78f)]);
                break;

            case Glyph.Folder:
                graphics.DrawLines(pen, [P(0.08f, 0.2f), P(0.38f, 0.2f), P(0.5f, 0.34f), P(0.92f, 0.34f), P(0.92f, 0.8f), P(0.08f, 0.8f), P(0.08f, 0.2f)]);
                break;

            case Glyph.Alert:
                graphics.DrawLines(pen, [P(0.5f, 0.08f), P(0.94f, 0.86f), P(0.06f, 0.86f), P(0.5f, 0.08f)]);
                graphics.DrawLine(pen, P(0.5f, 0.38f), P(0.5f, 0.6f));
                graphics.FillEllipse(brush, R(0.45f, 0.7f, 0.1f, 0.1f));
                break;

            case Glyph.Table:
                graphics.DrawRectangle(pen, x + (0.08f * s), y + (0.14f * s), 0.84f * s, 0.72f * s);
                graphics.DrawLine(pen, P(0.08f, 0.38f), P(0.92f, 0.38f));
                graphics.DrawLine(pen, P(0.4f, 0.14f), P(0.4f, 0.86f));
                break;

            case Glyph.Code:
                graphics.DrawLines(pen, [P(0.32f, 0.26f), P(0.08f, 0.5f), P(0.32f, 0.74f)]);
                graphics.DrawLines(pen, [P(0.68f, 0.26f), P(0.92f, 0.5f), P(0.68f, 0.74f)]);
                graphics.DrawLine(pen, P(0.56f, 0.18f), P(0.44f, 0.82f));
                break;

            case Glyph.Chip:
                graphics.DrawRectangle(pen, x + (0.26f * s), y + (0.26f * s), 0.48f * s, 0.48f * s);
                foreach (var t in new[] { 0.38f, 0.62f })
                {
                    graphics.DrawLine(pen, P(t, 0.1f), P(t, 0.26f));
                    graphics.DrawLine(pen, P(t, 0.74f), P(t, 0.9f));
                    graphics.DrawLine(pen, P(0.1f, t), P(0.26f, t));
                    graphics.DrawLine(pen, P(0.74f, t), P(0.9f, t));
                }
                break;

            case Glyph.Users:
                graphics.DrawEllipse(pen, R(0.3f, 0.1f, 0.4f, 0.4f));
                graphics.DrawArc(pen, R(0.12f, 0.56f, 0.76f, 0.7f), 190, 160);
                break;

            case Glyph.Download:
                graphics.DrawLine(pen, P(0.5f, 0.1f), P(0.5f, 0.64f));
                graphics.DrawLine(pen, P(0.3f, 0.44f), P(0.5f, 0.64f));
                graphics.DrawLine(pen, P(0.7f, 0.44f), P(0.5f, 0.64f));
                graphics.DrawLines(pen, [P(0.12f, 0.6f), P(0.12f, 0.9f), P(0.88f, 0.9f), P(0.88f, 0.6f)]);
                break;

            default: // Sum
                graphics.DrawLines(pen, [P(0.76f, 0.14f), P(0.24f, 0.14f), P(0.54f, 0.5f), P(0.24f, 0.86f), P(0.76f, 0.86f)]);
                break;
        }
        graphics.SmoothingMode = previous;
    }
}

/// <summary>Colori e metriche condivise dai controlli disegnati a mano.</summary>
internal static class ModernPalette
{
    public static (Color Fill, Color Hover, Color Pressed, Color Border, Color Ink) For(Theme.ButtonTone tone) => tone switch
    {
        Theme.ButtonTone.Primary => (Theme.Brand, Theme.BrandHover, Theme.BrandPressed, Theme.Brand, Color.White),
        Theme.ButtonTone.Positive => (Theme.Success, Theme.SuccessHover, Theme.SuccessPressed, Theme.Success, Color.White),
        Theme.ButtonTone.Danger => (Theme.Danger, Color.FromArgb(185, 28, 28), Color.FromArgb(153, 27, 27), Theme.Danger, Color.White),
        Theme.ButtonTone.OnDark => (
            Color.FromArgb(28, 44, 72), Color.FromArgb(44, 68, 108), Color.FromArgb(22, 35, 58),
            Color.FromArgb(70, 96, 140), Color.White),
        _ => (Theme.NeutralFill, Theme.NeutralHover, Theme.NeutralPressed, Theme.NeutralBorder, Theme.NeutralInk)
    };

    /// <summary>
    /// Larghezza naturale di un testo su una riga. Si misura SENZA i flag di ellissi e
    /// con un riquadro illimitato: con l'ellissi attiva e nessun limite alcune
    /// implementazioni restituiscono una larghezza minima e il testo veniva ridotto a «…».
    /// </summary>
    public static Size Measure(string text, Font font) => TextRenderer.MeasureText(
        text,
        font,
        new Size(int.MaxValue / 2, int.MaxValue / 2),
        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

    public const TextFormatFlags Single =
        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
        TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
}

/// <summary>
/// Pulsante moderno: angoli arrotondati, icona opzionale, stati al passaggio e alla
/// pressione, anello del focus per la tastiera. La larghezza segue il testo (anche a
/// DPI alti), quindi l'etichetta non viene mai tagliata. Senza testo è un pulsante
/// quadrato con la sola icona.
/// </summary>
internal sealed class IconButton : Control
{
    private Theme.ButtonTone _tone;
    private Glyph? _glyph;
    private bool _hover;
    private bool _pressed;
    private bool _badge;

    public IconButton(string text, Glyph? glyph, Theme.ButtonTone tone = Theme.ButtonTone.Neutral)
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.StandardClick |
            ControlStyles.StandardDoubleClick,
            true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Font = Theme.Fonts.ButtonSmall;
        Text = text;
        _glyph = glyph;
        _tone = tone;
        AccessibleRole = AccessibleRole.PushButton;
        Height = 34;
        Margin = new Padding(0);
        // Un Control semplice nasce largo 0: con AutoSize la larghezza la dà
        // GetPreferredSize (testo + icona), quindi segue anche il DPI.
        AutoSize = true;
    }

    public Theme.ButtonTone Tone
    {
        get => _tone;
        set
        {
            _tone = value;
            Invalidate();
        }
    }

    /// <summary>Pallino di segnalazione (per esempio: c'è una versione nuova).</summary>
    public bool Badge
    {
        get => _badge;
        set
        {
            _badge = value;
            Invalidate();
        }
    }

    public Glyph? Icon
    {
        get => _glyph;
        set
        {
            _glyph = value;
            Invalidate();
        }
    }

    // Control.Text accetta null in scrittura ([AllowNull]): l'override deve dichiararlo uguale,
    // altrimenti il compilatore avvisa (CS8765) a ogni build.
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => base.Text;
        set
        {
            base.Text = value ?? string.Empty;
            if (string.IsNullOrEmpty(AccessibleName))
            {
                AccessibleName = base.Text;
            }
            Invalidate();
        }
    }

    public void PerformClick()
    {
        if (Enabled)
        {
            OnClick(EventArgs.Empty);
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var height = Math.Max(Height, 28);
        if (string.IsNullOrEmpty(Text))
        {
            return new Size(height, height);
        }
        var textWidth = ModernPalette.Measure(Text, Font).Width;
        var icon = _glyph is null ? 0 : GlyphSize + 8;
        return new Size(14 + icon + textWidth + 14 + 2, height);
    }

    private int GlyphSize => Height >= 32 ? 16 : 14;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var palette = ModernPalette.For(_tone);
        using (var back = new SolidBrush(Parent?.BackColor ?? Theme.Canvas))
        {
            graphics.FillRectangle(back, ClientRectangle);
        }
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        if (bounds.Width < 4 || bounds.Height < 4)
        {
            return;
        }

        Color fill;
        Color border;
        Color ink;
        if (!Enabled)
        {
            fill = Theme.DisabledFill;
            border = Theme.Divider;
            ink = Theme.DisabledInk;
        }
        else
        {
            fill = _pressed ? palette.Pressed : _hover ? palette.Hover : palette.Fill;
            border = palette.Border;
            ink = palette.Ink;
        }
        using (var path = Theme.RoundedRectangle(bounds, 9))
        {
            using var fillBrush = new SolidBrush(fill);
            graphics.FillPath(fillBrush, path);
            using var borderPen = new Pen(border);
            graphics.DrawPath(borderPen, path);
        }
        if (Focused && ShowFocusCues)
        {
            using var focusPath = Theme.RoundedRectangle(Rectangle.Inflate(bounds, -2, -2), 7);
            using var focusPen = new Pen(Color.FromArgb(160, ink), 1f) { DashStyle = DashStyle.Dot };
            graphics.DrawPath(focusPen, focusPath);
        }

        var hasText = !string.IsNullOrEmpty(Text);
        var glyph = GlyphSize;
        var textWidth = hasText ? ModernPalette.Measure(Text, Font).Width : 0;
        var contentWidth = (_glyph is null ? 0 : glyph) + (_glyph is not null && hasText ? 8 : 0) + textWidth;
        var left = Math.Max(10, (Width - contentWidth) / 2);
        if (_glyph is { } icon)
        {
            Glyphs.Draw(graphics, new Rectangle(left, (Height - glyph) / 2, glyph, glyph), icon, ink, 1.6f);
            left += glyph + (hasText ? 8 : 0);
        }
        if (hasText)
        {
            TextRenderer.DrawText(
                graphics,
                Text,
                Font,
                new Rectangle(left, 0, Math.Max(1, Width - left - 6), Height),
                ink,
                ModernPalette.Single);
        }
        if (_badge)
        {
            var dot = 9;
            using var ring = new SolidBrush(Parent?.BackColor ?? Theme.Canvas);
            using var core = new SolidBrush(Theme.Warning);
            graphics.FillEllipse(ring, Width - dot - 2, 1, dot + 2, dot + 2);
            graphics.FillEllipse(core, Width - dot - 1, 2, dot, dot);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _pressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            Focus();
            _pressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _pressed = false;
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            PerformClick();
            e.Handled = true;
        }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }
}

/// <summary>
/// Area di rilascio del file Excel: rettangolo tratteggiato che si accende al
/// passaggio del mouse o quando ci si trascina sopra un file. Mostra lo stato (nessun
/// file, file caricato con foglio e conteggi) e raccoglie sia il clic sia il rilascio.
/// </summary>
internal sealed class DropZone : Control
{
    private bool _hover;
    private bool _dragging;
    private string _title = "Trascina qui il foglio Excel";
    private string _subtitle = "oppure fai clic per sceglierlo";
    private bool _loaded;

    public DropZone()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.StandardClick,
            true);
        AllowDrop = true;
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = "Scegli o trascina il foglio Excel";
        Height = 84;
    }

    /// <summary>Un file è stato rilasciato sull'area (percorso del primo .xlsx valido).</summary>
    public event Action<string>? FileDropped;

    public bool Busy { get; set; }

    /// <summary>Mostra il file caricato: nome, dettaglio (foglio, righe) e invito a cambiarlo.</summary>
    public void ShowLoaded(string fileName, string detail)
    {
        _loaded = true;
        _title = fileName;
        _subtitle = detail;
        Invalidate();
    }

    public void ShowEmpty()
    {
        _loaded = false;
        _title = "Trascina qui il foglio Excel";
        _subtitle = "oppure fai clic per sceglierlo";
        Invalidate();
    }

    public string TitleText => _title;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var back = new SolidBrush(Parent?.BackColor ?? Theme.Surface))
        {
            graphics.FillRectangle(back, ClientRectangle);
        }
        var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
        if (bounds.Width < 40 || bounds.Height < 30)
        {
            return;
        }

        var active = (_hover || _dragging || Focused) && !Busy;
        var fill = _loaded ? Theme.SuccessSoft : active ? Theme.BrandSoft : Theme.FieldFill;
        var line = _loaded ? Theme.Success : active ? Theme.Brand : Theme.DividerStrong;
        using (var path = Theme.RoundedRectangle(bounds, 12))
        {
            using var fillBrush = new SolidBrush(fill);
            graphics.FillPath(fillBrush, path);
            using var pen = new Pen(line, 1.4f) { DashStyle = _loaded ? DashStyle.Solid : DashStyle.Dash };
            graphics.DrawPath(pen, path);
        }

        var chip = Math.Min(44, bounds.Height - 20);
        var chipBox = new Rectangle(bounds.Left + 16, bounds.Top + ((bounds.Height - chip) / 2), chip, chip);
        using (var chipPath = Theme.RoundedRectangle(chipBox, 12))
        using (var chipBrush = new SolidBrush(_loaded ? Theme.Success : active ? Theme.Brand : Theme.BrandLite))
        {
            graphics.FillPath(chipBrush, chipPath);
        }
        var chipInk = _loaded ? Color.White : active ? Color.White : Theme.BrandInk;
        Glyphs.Draw(
            graphics,
            Rectangle.Inflate(chipBox, -(chip / 4), -(chip / 4)),
            _loaded ? Glyph.Check : Glyph.Upload,
            chipInk,
            1.8f);

        var textLeft = chipBox.Right + 14;
        var textWidth = Math.Max(10, bounds.Right - textLeft - 12);
        var titleFont = Theme.Fonts.SmallBold;
        var subFont = Theme.Fonts.Small;
        var titleHeight = titleFont.Height;
        var subHeight = subFont.Height;
        var total = titleHeight + 2 + subHeight;
        var top = bounds.Top + ((bounds.Height - total) / 2);
        TextRenderer.DrawText(
            graphics,
            Busy ? "Lettura del file in corso…" : _title,
            titleFont,
            new Rectangle(textLeft, top, textWidth, titleHeight),
            _loaded ? Theme.SuccessInk : Theme.Ink,
            ModernPalette.Single);
        TextRenderer.DrawText(
            graphics,
            _subtitle,
            subFont,
            new Rectangle(textLeft, top + titleHeight + 2, textWidth, subHeight),
            Theme.MutedInk,
            ModernPalette.Single);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override void OnDragEnter(DragEventArgs drgevent)
    {
        base.OnDragEnter(drgevent);
        var path = WorkbookDrop.FirstWorkbookPath(
            drgevent.Data?.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>());
        _dragging = path is not null && !Busy;
        drgevent.Effect = _dragging ? DragDropEffects.Copy : DragDropEffects.None;
        Invalidate();
    }

    protected override void OnDragLeave(EventArgs e)
    {
        base.OnDragLeave(e);
        _dragging = false;
        Invalidate();
    }

    protected override void OnDragDrop(DragEventArgs drgevent)
    {
        base.OnDragDrop(drgevent);
        _dragging = false;
        Invalidate();
        var path = WorkbookDrop.FirstWorkbookPath(
            drgevent.Data?.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>());
        if (path is not null && !Busy)
        {
            FileDropped?.Invoke(path);
        }
    }
}

/// <summary>
/// Scheda di riepilogo con icona: riquadro colorato con il simbolo, etichetta in
/// maiuscoletto, valore grande e una riga di contesto. Tutto è disegnato a mano e
/// ellissato alla larghezza disponibile, quindi nessun numero viene mai tagliato.
/// </summary>
internal sealed class MetricCard : Control
{
    private readonly Glyph _glyph;
    private readonly Color _accent;
    private readonly Color _soft;
    private string _value = "0";
    private string _caption = string.Empty;

    public MetricCard(string label, Glyph glyph, Color accent, Color soft)
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        Label = label;
        _glyph = glyph;
        _accent = accent;
        _soft = soft;
        AccessibleRole = AccessibleRole.StaticText;
        AccessibleName = label;
        Height = 92;
    }

    public string Label { get; }

    public string Value
    {
        get => _value;
        set
        {
            _value = value;
            AccessibleDescription = $"{Label}: {value}";
            Invalidate();
        }
    }

    public string Caption
    {
        get => _caption;
        set
        {
            _caption = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var back = new SolidBrush(Parent?.BackColor ?? Theme.Canvas))
        {
            graphics.FillRectangle(back, ClientRectangle);
        }
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        if (bounds.Width < 60 || bounds.Height < 50)
        {
            return;
        }
        using (var path = Theme.RoundedRectangle(bounds, 14))
        {
            using var fill = new SolidBrush(Theme.Surface);
            graphics.FillPath(fill, path);
            using var pen = new Pen(Theme.Divider);
            graphics.DrawPath(pen, path);
            // Barra d'accento in cima, rientrata dagli angoli: ogni metrica ha il suo colore.
            var barBounds = new Rectangle(bounds.Left + 18, bounds.Top, Math.Max(8, bounds.Width - 36), 3);
            using var barPath = Theme.RoundedRectangle(barBounds, 1);
            using var bar = new SolidBrush(Color.FromArgb(210, _accent));
            graphics.FillPath(bar, barPath);
        }

        var chip = new Rectangle(bounds.Left + 14, bounds.Top + 14, 38, 38);
        using (var chipPath = Theme.RoundedRectangle(chip, 11))
        using (var chipBrush = new SolidBrush(_soft))
        {
            graphics.FillPath(chipBrush, chipPath);
        }
        Glyphs.Draw(graphics, Rectangle.Inflate(chip, -9, -9), _glyph, _accent, 1.7f);

        var left = chip.Right + 12;
        var width = Math.Max(10, bounds.Right - left - 10);
        TextRenderer.DrawText(
            graphics,
            Label,
            Theme.Fonts.Section,
            new Rectangle(left, bounds.Top + 12, width, 16),
            Theme.MutedInk,
            ModernPalette.Single);
        TextRenderer.DrawText(
            graphics,
            _value,
            Theme.Fonts.Kpi,
            new Rectangle(left, bounds.Top + 27, width, 32),
            _accent,
            ModernPalette.Single);
        if (_caption.Length > 0)
        {
            TextRenderer.DrawText(
                graphics,
                _caption,
                Theme.Fonts.Tiny,
                new Rectangle(bounds.Left + 14, bounds.Bottom - 26, Math.Max(10, bounds.Width - 28), 18),
                Theme.MutedInk,
                ModernPalette.Single);
        }
    }
}

/// <summary>Tono di una riga di stato: colora il pallino.</summary>
internal enum StatusTone
{
    Neutral,
    Good,
    Warning,
    Bad
}

/// <summary>Riga di stato del sistema: pallino colorato, nome a sinistra e valore a destra, entrambi ellissati.</summary>
internal sealed class StatusRow : Control
{
    private readonly Glyph _glyph;
    private string _value = string.Empty;
    private StatusTone _tone = StatusTone.Neutral;

    public StatusRow(string title, Glyph glyph)
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        Title = title;
        _glyph = glyph;
        AccessibleRole = AccessibleRole.StaticText;
        AccessibleName = title;
        Height = 30;
    }

    public string Title { get; }

    public string Value => _value;

    public void SetState(string value, StatusTone tone)
    {
        _value = value;
        _tone = tone;
        AccessibleDescription = $"{Title}: {value}";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var back = new SolidBrush(Parent?.BackColor ?? Theme.Surface))
        {
            graphics.FillRectangle(back, ClientRectangle);
        }
        var (soft, ink) = _tone switch
        {
            StatusTone.Good => (Theme.SuccessSoft, Theme.SuccessInk),
            StatusTone.Warning => (Color.FromArgb(254, 243, 199), Theme.WarningInk),
            StatusTone.Bad => (Color.FromArgb(254, 226, 226), Theme.DangerInk),
            _ => (Theme.Sunken, Theme.MutedInk)
        };
        var chip = new Rectangle(0, (Height - 24) / 2, 24, 24);
        using (var path = Theme.RoundedRectangle(chip, 7))
        using (var brush = new SolidBrush(soft))
        {
            graphics.FillPath(brush, path);
        }
        Glyphs.Draw(graphics, Rectangle.Inflate(chip, -6, -6), _glyph, ink, 1.5f);

        var titleWidth = ModernPalette.Measure(Title, Theme.Fonts.SmallBold).Width;
        var left = chip.Right + 10;
        TextRenderer.DrawText(
            graphics,
            Title,
            Theme.Fonts.SmallBold,
            new Rectangle(left, 0, Math.Min(titleWidth + 4, Math.Max(10, Width - left)), Height),
            Theme.Ink,
            ModernPalette.Single);
        var valueLeft = left + titleWidth + 12;
        if (valueLeft < Width - 20)
        {
            TextRenderer.DrawText(
                graphics,
                _value,
                Theme.Fonts.Small,
                new Rectangle(valueLeft, 0, Width - valueLeft, Height),
                ink,
                ModernPalette.Single | TextFormatFlags.Right);
        }
    }
}

/// <summary>
/// Indicatore dei tre passi del lavoro (Importa, Verifica, Esporta): cerchi collegati da
/// una linea; i passi fatti diventano verdi con la spunta, quello in corso è blu.
/// </summary>
internal sealed class StepperBar : Control
{
    private static readonly string[] Names = ["Importa", "Verifica", "Esporta"];
    private int _current;

    public StepperBar()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        AccessibleRole = AccessibleRole.StaticText;
        AccessibleName = "Avanzamento del lavoro";
        Height = 34;
    }

    /// <summary>Passo in corso: 0 Importa, 1 Verifica, 2 Esporta, 3 tutto completato.</summary>
    public int Current
    {
        get => _current;
        set
        {
            _current = Math.Clamp(value, 0, Names.Length);
            AccessibleDescription = _current >= Names.Length ? "Tutti i passi completati" : $"Passo in corso: {Names[_current]}";
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var back = new SolidBrush(Parent?.BackColor ?? Theme.Surface))
        {
            graphics.FillRectangle(back, ClientRectangle);
        }
        var cell = Width / Names.Length;
        const int dot = 24;
        for (var index = 0; index < Names.Length; index++)
        {
            var left = index * cell;
            var circle = new Rectangle(left, (Height - dot) / 2, dot, dot);
            var done = index < _current;
            var active = index == _current;

            // La linea verso il passo successivo: piena se questo passo è completato.
            if (index < Names.Length - 1)
            {
                var from = circle.Right + 6 + ModernPalette.Measure(Names[index], Theme.Fonts.SmallBold).Width + 8;
                var to = left + cell - 8;
                if (to - from > 10)
                {
                    using var line = new Pen(done ? Theme.Success : Theme.Divider, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    graphics.DrawLine(line, from, Height / 2f, to, Height / 2f);
                }
            }

            using (var brush = new SolidBrush(done ? Theme.Success : active ? Theme.Brand : Theme.Sunken))
            {
                graphics.FillEllipse(brush, circle);
            }
            if (!done && !active)
            {
                using var ring = new Pen(Theme.DividerStrong, 1f);
                graphics.DrawEllipse(ring, circle.X, circle.Y, circle.Width - 1, circle.Height - 1);
            }
            if (done)
            {
                Glyphs.Draw(graphics, Rectangle.Inflate(circle, -6, -6), Glyph.Check, Color.White, 1.8f);
            }
            else
            {
                TextRenderer.DrawText(
                    graphics,
                    (index + 1).ToString(),
                    Theme.Fonts.SmallBold,
                    circle,
                    active ? Color.White : Theme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            TextRenderer.DrawText(
                graphics,
                Names[index],
                active ? Theme.Fonts.SmallBold : Theme.Fonts.Small,
                new Rectangle(circle.Right + 6, 0, Math.Max(10, cell - dot - 10), Height),
                done ? Theme.SuccessInk : active ? Theme.BrandInk : Theme.MutedInk,
                ModernPalette.Single);
        }
    }
}
