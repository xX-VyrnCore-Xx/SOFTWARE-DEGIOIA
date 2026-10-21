using System.Drawing.Drawing2D;

namespace PresenzeXmlConverter;

/// <summary>
/// Scorciatoia a riquadro verso una sezione del programma: icona, titolo e una riga che dice a che cosa serve.
/// Sulla dashboard vuota rende evidente dove andare, così tutto il programma si usa e non solo la conversione.
/// Al passaggio del mouse il bordo si accende e la freccia scivola in avanti (animazione breve).
/// </summary>
internal sealed class ToolTile : Control
{
    private readonly string _title;
    private readonly string _subtitle;
    private readonly Glyph _glyph;
    private AnimationClock? _hover;
    private bool _pressed;

    public ToolTile(string title, string subtitle, Glyph glyph)
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        _title = title;
        _subtitle = subtitle;
        _glyph = glyph;
        TabStop = true;
        Cursor = Cursors.Hand;
        Height = 92;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = title;
        AccessibleDescription = subtitle;
    }

    public string Title => _title;

    private AnimationClock Clock => _hover ??= CreateClock();

    private AnimationClock CreateClock()
    {
        var clock = new AnimationClock(this, 0f);
        clock.Frame += (_, _) => Invalidate();
        return clock;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Clock.AnimateTo(1f, Motion.Quick);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _pressed = false;
        Clock.AnimateTo(Focused ? 1f : 0f, Motion.Quick);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Focus();
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var wasPressed = _pressed;
        _pressed = false;
        Invalidate();
        if (wasPressed && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location))
        {
            OnClick(EventArgs.Empty);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        Invalidate();
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var behind = new SolidBrush(Parent?.BackColor ?? Theme.Canvas))
        {
            g.FillRectangle(behind, ClientRectangle);
        }
        var amount = Math.Clamp(_hover?.Value ?? 0f, 0f, 1f);
        var bounds = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        if (bounds.Width < 40 || bounds.Height < 40)
        {
            return;
        }
        var radius = this.Px(14);
        using (var path = Theme.RoundedRectangle(bounds, radius))
        {
            using (var fill = new SolidBrush(_pressed ? Theme.Sunken : Theme.Surface))
            {
                g.FillPath(fill, path);
            }
            var edge = Mix(Theme.Divider, Theme.Brand, amount);
            using var pen = new Pen(edge, 1f + amount);
            g.DrawPath(pen, path);
            if (Focused && ShowFocusCues)
            {
                using var ring = new Pen(Theme.Brand, Math.Max(2f, this.Px(2f)));
                using var ringPath = Theme.RoundedRectangle(Rectangle.Inflate(bounds, -1, -1), radius);
                g.DrawPath(ring, ringPath);
            }
        }

        var pad = this.Px(16);
        var chipSize = this.Px(40);
        var chip = new Rectangle(bounds.Left + pad, bounds.Top + pad, chipSize, chipSize);
        using (var chipPath = Theme.RoundedRectangle(chip, this.Px(11)))
        using (var chipBrush = new SolidBrush(Mix(Theme.BrandSoft, Theme.Brand, amount)))
        {
            g.FillPath(chipBrush, chipPath);
        }
        var chipInk = Mix(Theme.BrandInk, Color.White, amount);
        Glyphs.Draw(g, Rectangle.Inflate(chip, -this.Px(10), -this.Px(10)), _glyph, chipInk, this.Px(1.7f));

        var textLeft = chip.Right + this.Px(14);
        var arrowBox = this.Px(18);
        var textWidth = Math.Max(10, bounds.Right - textLeft - pad - arrowBox);
        UiText.Draw(
            g,
            _title,
            Theme.Fonts.TabBold,
            new Rectangle(textLeft, chip.Top - this.Px(1), textWidth, Theme.Fonts.TabBold.Height + this.Px(4)),
            Theme.Ink,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        var subtitleTop = chip.Top + Theme.Fonts.TabBold.Height + this.Px(5);
        UiText.Draw(
            g,
            _subtitle,
            Theme.Fonts.Small,
            new Rectangle(textLeft, subtitleTop, textWidth, Math.Max(10, bounds.Bottom - subtitleTop - this.Px(10))),
            Theme.MutedInk,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

        // Freccia che scivola in avanti quando il mouse è sopra.
        var nudge = (int)Math.Round(this.Px(4) * amount);
        var arrow = new Rectangle(bounds.Right - pad - arrowBox + nudge, chip.Top + ((chipSize - arrowBox) / 2), arrowBox, arrowBox);
        Glyphs.Draw(g, arrow, Glyph.Arrow, Mix(Theme.FaintInk, Theme.Brand, amount), this.Px(1.6f));
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
            _hover?.Dispose();
            _hover = null;
        }
        base.Dispose(disposing);
    }
}
