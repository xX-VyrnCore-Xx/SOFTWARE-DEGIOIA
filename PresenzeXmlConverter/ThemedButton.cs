using System.Drawing.Drawing2D;

namespace PresenzeXmlConverter;

/// <summary>
/// Pulsante dipinto dall'app: angoli arrotondati con bordo antialiasing (come le
/// card e i campi), stati al passaggio e alla pressione, anello di focus da
/// tastiera e testo sempre su una sola riga. Il pulsante piatto di Windows ha
/// gli angoli vivi, va a capo quando l'etichetta è lunga e stona con tutto il
/// resto dell'interfaccia. I colori restano quelli impostati da
/// <see cref="Theme.StyleButton"/> (FlatAppearance e BackColor/ForeColor).
/// </summary>
internal sealed class ThemedButton : Button
{
    private const int Radius = 8;
    private bool _hover;
    private bool _pressed;
    // 0 = riposo, 1 = passaggio del mouse: il colore scorre fra i due con una curva a tempo
    // (non a passi fissi) invece di scattare, come nelle app curate.
    private float _blend;
    private AnimationClock? _clock;
    private Glyph? _icon;

    public ThemedButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        FlatStyle = FlatStyle.Flat;
    }

    /// <summary>Icona a sinistra del testo; si toglie da sola se il pulsante è troppo stretto per entrambi.</summary>
    public Glyph? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        StartFade();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        StartFade();
        base.OnMouseLeave(e);
    }

    private void StartFade()
    {
        _clock ??= CreateClock();
        if (!Enabled)
        {
            _clock.Jump(0f);
            _blend = 0f;
            Invalidate();
            return;
        }
        // Entra in fretta (il pulsante risponde al mouse), esce un po' più piano.
        _clock.AnimateTo(_hover ? 1f : 0f, _hover ? Motion.Quick : Motion.Standard);
    }

    private AnimationClock CreateClock()
    {
        var clock = new AnimationClock(this);
        clock.Frame += (_, _) =>
        {
            _blend = clock.Value;
            Invalidate();
        };
        return clock;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clock?.Dispose();
            _clock = null;
        }
        base.Dispose(disposing);
    }

    private static Color Mix(Color from, Color to, float amount) => Color.FromArgb(
        255,
        (int)Math.Round(from.R + ((to.R - from.R) * amount)),
        (int)Math.Round(from.G + ((to.G - from.G) * amount)),
        (int)Math.Round(from.B + ((to.B - from.B) * amount)));

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        _blend = 0f;
        _clock?.Jump(0f);
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

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var graphics = pevent.Graphics;
        using (var backdrop = new SolidBrush(BackdropColor()))
        {
            graphics.FillRectangle(backdrop, ClientRectangle);
        }
        // Una riga in basso resta libera per l'ombra: i pulsanti poggiano sulla pagina come in macOS.
        var bounds = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 2);
        if (bounds.Width < 4 || bounds.Height < 4)
        {
            return;
        }

        var fill = BackColor;
        if (Enabled && _pressed)
        {
            fill = FlatAppearance.MouseDownBackColor;
        }
        else if (Enabled && _blend > 0f)
        {
            fill = Mix(BackColor, FlatAppearance.MouseOverBackColor, _blend);
        }

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var radius = Math.Min(Radius, Math.Max(2, bounds.Height / 2));
        using (var path = Theme.RoundedRectangle(bounds, radius))
        {
            if (Enabled && !_pressed)
            {
                var shadowBounds = bounds;
                shadowBounds.Offset(0, 1);
                using var shadowPath = Theme.RoundedRectangle(shadowBounds, radius);
                using var shadow = new SolidBrush(Color.FromArgb(fill.GetBrightness() < 0.5f ? 46 : 22, 0, 0, 0));
                graphics.FillPath(shadow, shadowPath);
            }
            if (Theme.IsGlass && Enabled)
            {
                // Vetro: i pulsanti pieni sono sfumati (più chiari in alto), quelli neutri appena velati.
                var filled = FlatAppearance.BorderSize == 0;
                var top = Mix(fill, Color.White, filled ? 0.16f : 0.07f);
                var bottom = filled ? Theme.Shade(fill, 0.12f) : fill;
                using var gradient = new LinearGradientBrush(
                    new Rectangle(bounds.X, bounds.Y, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height)),
                    top,
                    bottom,
                    90f);
                graphics.FillPath(gradient, path);
            }
            else
            {
                using var brush = new SolidBrush(fill);
                graphics.FillPath(brush, path);
            }
            if (FlatAppearance.BorderSize > 0 && Enabled)
            {
                using var border = new Pen(FlatAppearance.BorderColor, 1f);
                graphics.DrawPath(border, path);
            }
            // Riflesso sottile lungo il bordo alto dei pulsanti pieni (blu, verde): da' un
            // minimo di rilievo senza sfumature pesanti.
            if (Enabled && !_pressed && (fill.GetBrightness() < 0.5f || Theme.IsGlass) && bounds.Width > radius * 2 + 6)
            {
                using var gloss = new Pen(Color.FromArgb(46, Color.White), 1f);
                graphics.DrawLine(gloss, bounds.Left + radius, bounds.Top + 1, bounds.Right - radius, bounds.Top + 1);
            }
        }

        if (Focused && ShowFocusCues && Enabled)
        {
            var ringBounds = Rectangle.Inflate(bounds, -2, -2);
            if (ringBounds.Width > 4 && ringBounds.Height > 4)
            {
                using var ringPath = Theme.RoundedRectangle(ringBounds, Math.Max(2, radius - 2));
                using var halo = new Pen(Color.FromArgb(70, Theme.Brand), 3.5f);
                using var ring = new Pen(Color.FromArgb(170, Color.White), 1.5f);
                using var outer = new Pen(Theme.Brand, 1.5f);
                var light = BackColor.GetBrightness() > 0.8f;
                if (light)
                {
                    graphics.DrawPath(halo, ringPath);
                }
                graphics.DrawPath(light ? outer : ring, ringPath);
            }
        }

        var ink = Enabled ? ForeColor : Theme.DisabledInk;
        const TextFormatFlags flags = TextFormatFlags.VerticalCenter |
                                      TextFormatFlags.SingleLine |
                                      TextFormatFlags.EndEllipsis |
                                      TextFormatFlags.NoPrefix;
        if (_icon is { } glyph && !string.IsNullOrEmpty(Text))
        {
            var iconSize = Height >= 32 ? 16 : 14;
            var layout = ButtonLayout.Compute(
                ClientSize.Width,
                ModernPalette.Measure(Text, Font).Width,
                iconSize,
                7,
                Math.Max(8, Padding.Left),
                hasIcon: true);
            if (layout.ShowIcon)
            {
                Glyphs.Draw(graphics, new Rectangle(layout.IconLeft, (ClientSize.Height - 2 - iconSize) / 2, iconSize, iconSize), glyph, ink, 1.6f);
                UiText.Draw(
                    graphics,
                    Text,
                    Font,
                    new Rectangle(layout.TextLeft, 0, layout.TextWidth + 2, ClientSize.Height),
                    ink,
                    flags | TextFormatFlags.Left | TextFormatFlags.NoPadding);
                return;
            }
        }

        var textBounds = new Rectangle(
            Padding.Left,
            0,
            Math.Max(1, ClientSize.Width - Padding.Horizontal),
            ClientSize.Height);
        UiText.Draw(
            graphics,
            Text,
            Font,
            textBounds,
            ink,
            flags | TextFormatFlags.HorizontalCenter);
    }

    /// <summary>Fondo dietro gli angoli arrotondati: il primo colore pieno risalendo i genitori.</summary>
    private Color BackdropColor()
    {
        for (var parent = Parent; parent is not null; parent = parent.Parent)
        {
            if (parent.BackColor.A == 255)
            {
                return parent.BackColor;
            }
        }
        return Theme.Canvas;
    }
}

internal static class ButtonIcons
{
    /// <summary>Mette un'icona a un pulsante dell'app (i pulsanti di altro tipo restano com'erano).</summary>
    public static T WithIcon<T>(this T button, Glyph glyph) where T : Button
    {
        if (button is ThemedButton themed)
        {
            themed.Icon = glyph;
            Theme.FitButtonToText(themed);
        }
        return button;
    }
}
