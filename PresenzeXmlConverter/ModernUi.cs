using System.Drawing.Drawing2D;

namespace PresenzeXmlConverter;

/// <summary>
/// Libreria delle icone dell'interfaccia. Ogni icona sta in un quadrato (di solito
/// 16-20 px) ed è disegnata con una penna a estremità tonde, così le linee restano
/// leggibili sia sul fondo chiaro sia su quello scuro.
/// </summary>
internal static class Glyphs
{
    /// <summary>
    /// Disegna l'icona nel riquadro. I tracciati stanno in <see cref="IconGeometry"/> (griglia 24 × 24): qui si
    /// scalano al riquadro e si tracciano con una penna a estremità tonde dello spessore indicato (in pixel).
    /// </summary>
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
        var side = Math.Min(box.Width, box.Height);
        var scale = side / IconGeometry.Grid;
        var left = box.Left + ((box.Width - side) / 2f);
        var top = box.Top + ((box.Height - side) / 2f);
        PointF Map(float x, float y) => new(left + (x * scale), top + (y * scale));

        foreach (var shape in IconGeometry.For(glyph))
        {
            using var path = new GraphicsPath();
            var current = PointF.Empty;
            var figureOpen = false;
            foreach (var seg in shape.Segs)
            {
                var v = seg.Values;
                switch (seg.Op)
                {
                    case 'M':
                        if (figureOpen)
                        {
                            path.StartFigure();
                        }
                        current = Map(v[0], v[1]);
                        figureOpen = true;
                        break;
                    case 'L':
                        var line = Map(v[0], v[1]);
                        path.AddLine(current, line);
                        current = line;
                        break;
                    case 'C':
                        var end = Map(v[4], v[5]);
                        path.AddBezier(current, Map(v[0], v[1]), Map(v[2], v[3]), end);
                        current = end;
                        break;
                    case 'Z':
                        path.CloseFigure();
                        break;
                }
            }
            if (shape.Filled)
            {
                graphics.FillPath(brush, path);
            }
            else
            {
                graphics.DrawPath(pen, path);
            }
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
        Theme.ButtonTone.Danger => (Theme.Danger, Theme.Shade(Theme.Danger, 0.10f), Theme.Shade(Theme.Danger, 0.20f), Theme.Danger, Color.White),
        Theme.ButtonTone.OnDark => (
            Theme.HeaderButtonFill, Theme.HeaderButtonHover, Theme.HeaderButtonPressed,
            Theme.HeaderButtonBorder, Theme.Ink),
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
    // 0 = riposo, 1 = passaggio del mouse: il colore scorre con una curva a tempo invece di scattare.
    private AnimationClock? _hoverClock;

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
            var blend = _hoverClock?.Value ?? (_hover ? 1f : 0f);
            fill = _pressed ? palette.Pressed : Mix(palette.Fill, palette.Hover, blend);
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
            UiText.Draw(
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
        SyncHover();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _pressed = false;
        SyncHover();
    }

    private void SyncHover()
    {
        _hoverClock ??= CreateHoverClock();
        // Entra in fretta (risponde al mouse), esce un po' più piano.
        _hoverClock.AnimateTo(_hover && Enabled ? 1f : 0f, _hover ? Motion.Quick : Motion.Standard);
        Invalidate();
    }

    private AnimationClock CreateHoverClock()
    {
        var clock = new AnimationClock(this);
        clock.Frame += (_, _) => Invalidate();
        return clock;
    }

    private static Color Mix(Color from, Color to, float amount) => Color.FromArgb(
        255,
        (int)Math.Round(from.R + ((to.R - from.R) * amount)),
        (int)Math.Round(from.G + ((to.G - from.G) * amount)),
        (int)Math.Round(from.B + ((to.B - from.B) * amount)));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hoverClock?.Dispose();
            _hoverClock = null;
        }
        base.Dispose(disposing);
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
        _hoverClock?.Jump(0f);
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
    private bool _removeHot;
    // 0 = a riposo, 1 = attiva (mouse sopra, file in arrivo, tastiera): colori e bordo scorrono invece di scattare.
    private AnimationClock? _activeClock;

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

    /// <summary>L'utente vuole togliere il file caricato (la «×» a destra o il tasto Canc).</summary>
    public event Action? RemoveRequested;

    private bool _busy;
    private System.Windows.Forms.Timer? _spinTimer;
    private readonly System.Diagnostics.Stopwatch _spinClock = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>Lettura in corso: al posto dell'icona gira un anello (fermo se Windows ha gli effetti spenti).</summary>
    public bool Busy
    {
        get => _busy;
        set
        {
            if (_busy == value)
            {
                return;
            }
            _busy = value;
            UpdateSpinner();
            Invalidate();
        }
    }

    private void UpdateSpinner()
    {
        if (_busy && AnimationClock.EffectsEnabled)
        {
            _spinTimer ??= new System.Windows.Forms.Timer { Interval = 30 };
            _spinTimer.Tick -= OnSpinTick;
            _spinTimer.Tick += OnSpinTick;
            _spinTimer.Start();
        }
        else
        {
            _spinTimer?.Stop();
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateSpinner();
    }

    private void OnSpinTick(object? sender, EventArgs e)
    {
        if (!_busy || !Visible)
        {
            _spinTimer?.Stop();
            return;
        }
        Invalidate();
    }

    /// <summary>Zona cliccabile della «×» che toglie il file: solo a file caricato.</summary>
    private Rectangle RemoveBounds
    {
        get
        {
            var size = Math.Min(24, Math.Max(0, Height - 16));
            return new Rectangle(Width - 3 - 12 - size, (Height - size) / 2, size, size);
        }
    }

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
        var blend = Busy ? 0f : _activeClock?.Value ?? (active ? 1f : 0f);
        var fill = _loaded ? Theme.SuccessSoft : Mix(Theme.FieldFill, Theme.BrandSoft, blend);
        var line = _loaded ? Theme.Success : Mix(Theme.DividerStrong, Theme.Brand, blend);
        using (var path = Theme.RoundedRectangle(bounds, 12))
        {
            using var fillBrush = new SolidBrush(fill);
            graphics.FillPath(fillBrush, path);
            using var pen = new Pen(line, 1.4f) { DashStyle = _loaded ? DashStyle.Solid : DashStyle.Dash };
            graphics.DrawPath(pen, path);
        }

        if (!_loaded && !Busy && bounds.Height >= 120)
        {
            PaintLargeEmpty(graphics, bounds, blend);
            return;
        }

        var chip = Math.Min(44, bounds.Height - 20);
        var chipBox = new Rectangle(bounds.Left + 16, bounds.Top + ((bounds.Height - chip) / 2), chip, chip);
        using (var chipPath = Theme.RoundedRectangle(chipBox, 12))
        using (var chipBrush = new SolidBrush(_loaded ? Theme.Success : Busy ? Theme.Brand : Mix(Theme.BrandLite, Theme.Brand, blend)))
        {
            graphics.FillPath(chipBrush, chipPath);
        }
        var chipInk = _loaded ? Color.White : blend > 0.5f ? Color.White : Theme.BrandInk;
        if (Busy)
        {
            // Anello che gira: l'arco si allunga e si accorcia, senza mai fermarsi mentre il foglio viene letto.
            var ring = Rectangle.Inflate(chipBox, -(chip / 4), -(chip / 4));
            var elapsed = _spinClock.Elapsed.TotalMilliseconds;
            var animated = _spinTimer is { Enabled: true };
            using var track = new Pen(Color.FromArgb(70, Color.White), 2.4f);
            using var arc = new Pen(Color.White, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawEllipse(track, ring);
            graphics.DrawArc(
                arc,
                ring,
                animated ? SpinnerMath.StartAngle(elapsed) : -90f,
                animated ? SpinnerMath.Sweep(elapsed) : 120f);
        }
        else
        {
            Glyphs.Draw(
                graphics,
                Rectangle.Inflate(chipBox, -(chip / 4), -(chip / 4)),
                _loaded ? Glyph.Check : Glyph.Upload,
                chipInk,
                1.8f);
        }

        var textLeft = chipBox.Right + 14;
        var textWidth = Math.Max(10, bounds.Right - textLeft - (_loaded ? 12 + RemoveBounds.Width + 8 : 12));
        var titleFont = Theme.Fonts.SmallBold;
        var subFont = Theme.Fonts.Small;
        var titleHeight = titleFont.Height;
        var subHeight = subFont.Height;
        var total = titleHeight + 2 + subHeight;
        var top = bounds.Top + ((bounds.Height - total) / 2);
        UiText.Draw(
            graphics,
            Busy ? "Lettura del file in corso…" : _title,
            titleFont,
            new Rectangle(textLeft, top, textWidth, titleHeight),
            _loaded ? Theme.SuccessInk : Theme.Ink,
            ModernPalette.Single);
        UiText.Draw(
            graphics,
            _subtitle,
            subFont,
            new Rectangle(textLeft, top + titleHeight + 2, textWidth, subHeight),
            Theme.MutedInk,
            ModernPalette.Single);

        if (_loaded && !Busy)
        {
            var remove = RemoveBounds;
            using (var back = new SolidBrush(_removeHot ? Theme.Danger : Color.FromArgb(40, Theme.SuccessInk)))
            {
                graphics.FillEllipse(back, remove);
            }
            var inset = remove.Width / 3;
            using var cross = new Pen(_removeHot ? Color.White : Theme.SuccessInk, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawLine(cross, remove.Left + inset, remove.Top + inset, remove.Right - inset, remove.Bottom - inset);
            graphics.DrawLine(cross, remove.Right - inset, remove.Top + inset, remove.Left + inset, remove.Bottom - inset);
        }
    }

    /// <summary>Area grande e vuota: icona, titolo e invito centrati, invece di una riga isolata a sinistra.</summary>
    private void PaintLargeEmpty(Graphics graphics, Rectangle bounds, float blend)
    {
        var chip = 56;
        var titleFont = Theme.Fonts.Field;
        var subFont = Theme.Fonts.Small;
        var total = chip + 14 + titleFont.Height + 4 + subFont.Height;
        var top = bounds.Top + Math.Max(8, (bounds.Height - total) / 2);
        var chipBox = new Rectangle(bounds.Left + ((bounds.Width - chip) / 2), top, chip, chip);
        using (var chipPath = Theme.RoundedRectangle(chipBox, 16))
        using (var chipBrush = new SolidBrush(Mix(Theme.BrandLite, Theme.Brand, blend)))
        {
            graphics.FillPath(chipBrush, chipPath);
        }
        Glyphs.Draw(
            graphics,
            Rectangle.Inflate(chipBox, -15, -15),
            Glyph.Upload,
            blend > 0.5f ? Color.White : Theme.BrandInk,
            2f);
        var centered = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        UiText.Draw(
            graphics,
            _title,
            titleFont,
            new Rectangle(bounds.Left + 12, chipBox.Bottom + 14, bounds.Width - 24, titleFont.Height + 2),
            Theme.Ink,
            centered);
        UiText.Draw(
            graphics,
            _subtitle,
            subFont,
            new Rectangle(bounds.Left + 12, chipBox.Bottom + 14 + titleFont.Height + 4, bounds.Width - 24, subFont.Height + 2),
            Theme.MutedInk,
            centered);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetRemoveHot(_loaded && !Busy && RemoveBounds.Contains(e.Location));
    }

    private void SetRemoveHot(bool hot)
    {
        if (_removeHot == hot)
        {
            return;
        }
        _removeHot = hot;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        if (_removeHot)
        {
            RemoveRequested?.Invoke();
            return;
        }
        base.OnClick(e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        SyncActive();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        _removeHot = false;
        SyncActive();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        SyncActive();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        SyncActive();
    }

    private void SyncActive()
    {
        _activeClock ??= CreateActiveClock();
        var active = (_hover || _dragging || Focused) && !Busy;
        _activeClock.AnimateTo(active ? 1f : 0f, active ? Motion.Quick : Motion.Standard);
        Invalidate();
    }

    private AnimationClock CreateActiveClock()
    {
        var clock = new AnimationClock(this);
        clock.Frame += (_, _) => Invalidate();
        return clock;
    }

    private static Color Mix(Color from, Color to, float amount) => Color.FromArgb(
        255,
        (int)Math.Round(from.R + ((to.R - from.R) * amount)),
        (int)Math.Round(from.G + ((to.G - from.G) * amount)),
        (int)Math.Round(from.B + ((to.B - from.B) * amount)));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _activeClock?.Dispose();
            _activeClock = null;
            _spinTimer?.Dispose();
            _spinTimer = null;
        }
        base.Dispose(disposing);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            _removeHot = false;
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Delete && _loaded && !Busy)
        {
            RemoveRequested?.Invoke();
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
        SyncActive();
    }

    protected override void OnDragLeave(EventArgs e)
    {
        base.OnDragLeave(e);
        _dragging = false;
        SyncActive();
    }

    protected override void OnDragDrop(DragEventArgs drgevent)
    {
        base.OnDragDrop(drgevent);
        _dragging = false;
        SyncActive();
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
    // Conteggio animato: dal testo mostrato al testo di arrivo (vedi CountUpText).
    private string _shown = "0";
    private string _from = "0";
    private AnimationClock? _count;

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
            _count?.Jump(1f);
            _value = value;
            _shown = value;
            AccessibleDescription = $"{Label}: {value}";
            Invalidate();
        }
    }

    /// <summary>
    /// Cambia il valore facendo scorrere i numeri dal testo mostrato a quello nuovo (circa 0,4 secondi). Se il testo
    /// è lo stesso non fa nulla; senza finestra visibile o con gli effetti di Windows spenti salta subito al valore.
    /// </summary>
    public void AnimateTo(string value)
    {
        if (string.Equals(_value, value, StringComparison.Ordinal))
        {
            return;
        }
        _from = _shown;
        _value = value;
        AccessibleDescription = $"{Label}: {value}";
        if (_count is null)
        {
            _count = new AnimationClock(this, 1f);
            _count.Frame += (_, _) =>
            {
                _shown = CountUpText.Frame(_from, _value, _count.Value);
                Invalidate();
            };
        }
        _count.Jump(0f);
        _count.AnimateTo(1f, Motion.Slow);
        if (!_count.IsRunning)
        {
            _shown = _value;
        }
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _count?.Dispose();
            _count = null;
        }
        base.Dispose(disposing);
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

    private Trend? _trend;
    private float[] _spark = [];

    /// <summary>Andamento delle ultime esportazioni: una linea con area sfumata accanto al valore (serve almeno due punti).</summary>
    public float[] Spark
    {
        get => _spark;
        set
        {
            _spark = SparklineMath.Normalize(value ?? []);
            Invalidate();
        }
    }

    private void PaintSparkline(Graphics graphics, Rectangle bounds, int valueRight)
    {
        if (_spark.Length < 2)
        {
            return;
        }
        var left = Math.Max(valueRight + 12, bounds.Left + (bounds.Width / 2));
        var area = new Rectangle(left, bounds.Top + 20, bounds.Right - 14 - left, 38);
        if (area.Width < 48)
        {
            return;
        }
        var points = new PointF[_spark.Length];
        for (var index = 0; index < points.Length; index++)
        {
            points[index] = new PointF(
                area.Left + (area.Width * index / (float)(points.Length - 1)),
                area.Bottom - (area.Height * _spark[index]));
        }
        var color = Theme.IsGlass ? Theme.BrandHover : _accent;
        using (var fillPath = new GraphicsPath())
        {
            fillPath.AddLines(points);
            fillPath.AddLine(points[^1], new PointF(points[^1].X, area.Bottom + 4));
            fillPath.AddLine(new PointF(points[^1].X, area.Bottom + 4), new PointF(points[0].X, area.Bottom + 4));
            fillPath.CloseFigure();
            using var fill = new LinearGradientBrush(
                new Rectangle(area.Left, area.Top, area.Width, area.Height + 6),
                Color.FromArgb(Theme.IsGlass ? 90 : 60, color),
                Color.FromArgb(0, color),
                90f);
            graphics.FillPath(fill, fillPath);
        }
        using var line = new Pen(color, 2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawCurve(line, points, 0.35f);
        var last = points[^1];
        using var dot = new SolidBrush(color);
        graphics.FillEllipse(dot, last.X - 3f, last.Y - 3f, 6f, 6f);
    }

    /// <summary>Confronto con la volta scorsa: se c'è, al posto della didascalia compare una pillola colorata.</summary>
    public Trend? Trend
    {
        get => _trend;
        set
        {
            _trend = value;
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
            if (Theme.IsGlass)
            {
                using var glassFill = new LinearGradientBrush(bounds, Theme.GlassTop, Theme.GlassBottom, 90f);
                graphics.FillPath(glassFill, path);
                using var glassEdge = new LinearGradientBrush(bounds, Theme.GlassEdge, Theme.GlassEdgeDim, 40f);
                using var glassPen = new Pen(glassEdge, 1f);
                graphics.DrawPath(glassPen, path);
            }
            else
            {
                using var fill = new SolidBrush(Theme.Surface);
                graphics.FillPath(fill, path);
                using var pen = new Pen(Theme.Divider);
                graphics.DrawPath(pen, path);
            }
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
        UiText.Draw(
            graphics,
            Label,
            Theme.Fonts.Section,
            new Rectangle(left, bounds.Top + 12, width, 16),
            Theme.MutedInk,
            ModernPalette.Single);
        UiText.Draw(
            graphics,
            _shown,
            Theme.Fonts.Kpi,
            new Rectangle(left, bounds.Top + 27, width, 32),
            _accent,
            ModernPalette.Single);
        var valueWidth = TextRenderer.MeasureText(
            _shown,
            Theme.Fonts.Kpi,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        PaintSparkline(graphics, bounds, left + valueWidth);
        if (_trend is { } trend)
        {
            var (chipFill, chipInk) = trend.Tone switch
            {
                TrendTone.Up => (Theme.SuccessSoft, Theme.SuccessInk),
                TrendTone.Down => (Theme.WarningSoft, Theme.WarningInk),
                _ => (Theme.BrandSoft, Theme.BrandInk)
            };
            var available = Math.Max(10, bounds.Width - 28);
            var measured = TextRenderer.MeasureText(
                trend.Text,
                Theme.Fonts.Pill,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + 18;
            var pill = new Rectangle(bounds.Left + 14, bounds.Bottom - 28, Math.Min(measured, available), 20);
            using (var pillPath = Theme.RoundedRectangle(pill, 10))
            using (var pillBrush = new SolidBrush(chipFill))
            {
                graphics.FillPath(pillBrush, pillPath);
            }
            UiText.Draw(
                graphics,
                trend.Text,
                Theme.Fonts.Pill,
                pill,
                chipInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            AccessibleDescription = $"{Label}: {_value}, {trend.Text}";
        }
        else if (_caption.Length > 0)
        {
            UiText.Draw(
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
            StatusTone.Warning => (Theme.WarningSoft, Theme.WarningInk),
            StatusTone.Bad => (Theme.DangerSoft, Theme.DangerInk),
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
        UiText.Draw(
            graphics,
            Title,
            Theme.Fonts.SmallBold,
            new Rectangle(left, 0, Math.Min(titleWidth + 4, Math.Max(10, Width - left)), Height),
            Theme.Ink,
            ModernPalette.Single);
        var valueLeft = left + titleWidth + 12;
        if (valueLeft < Width - 20)
        {
            if (_tone == StatusTone.Neutral)
            {
                UiText.Draw(
                    graphics,
                    _value,
                    Theme.Fonts.Small,
                    new Rectangle(valueLeft, 0, Width - valueLeft, Height),
                    ink,
                    ModernPalette.Single | TextFormatFlags.Right);
            }
            else
            {
                // Stato buono, da controllare o in errore: il valore sta in una pillola colorata a destra.
                var pillWidth = Math.Min(Width - valueLeft, ModernPalette.Measure(_value, Theme.Fonts.Small).Width + 20);
                var pill = new Rectangle(Width - pillWidth, (Height - 22) / 2, pillWidth, 22);
                using (var pillPath = Theme.RoundedRectangle(pill, 11))
                using (var pillBrush = new SolidBrush(soft))
                {
                    graphics.FillPath(pillBrush, pillPath);
                }
                UiText.Draw(
                    graphics,
                    _value,
                    Theme.Fonts.Small,
                    Rectangle.Inflate(pill, -8, 0),
                    ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
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
                UiText.Draw(
                    graphics,
                    (index + 1).ToString(),
                    Theme.Fonts.SmallBold,
                    circle,
                    active ? Color.White : Theme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            UiText.Draw(
                graphics,
                Names[index],
                active ? Theme.Fonts.SmallBold : Theme.Fonts.Small,
                new Rectangle(circle.Right + 6, 0, Math.Max(10, cell - dot - 10), Height),
                done ? Theme.SuccessInk : active ? Theme.BrandInk : Theme.MutedInk,
                ModernPalette.Single);
        }
    }
}
