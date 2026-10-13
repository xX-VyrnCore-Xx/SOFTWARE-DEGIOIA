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
    // 0 = riposo, 1 = passaggio del mouse: il colore scorre fra i due in pochi fotogrammi
    // invece di scattare, come nelle app curate.
    private float _blend;
    private System.Windows.Forms.Timer? _fade;

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
        if (!IsHandleCreated || !Enabled)
        {
            _blend = _hover && Enabled ? 1f : 0f;
            Invalidate();
            return;
        }
        _fade ??= CreateFadeTimer();
        _fade.Start();
    }

    private System.Windows.Forms.Timer CreateFadeTimer()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 16 };
        timer.Tick += (_, _) =>
        {
            var target = _hover ? 1f : 0f;
            var step = target > _blend ? 0.25f : -0.2f;
            _blend = Math.Clamp(_blend + step, 0f, 1f);
            if (Math.Abs(_blend - target) < 0.01f)
            {
                _blend = target;
                timer.Stop();
            }
            Invalidate();
        };
        return timer;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fade?.Dispose();
            _fade = null;
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
        var bounds = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
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
            using (var brush = new SolidBrush(fill))
            {
                graphics.FillPath(brush, path);
            }
            if (FlatAppearance.BorderSize > 0 && Enabled)
            {
                using var border = new Pen(FlatAppearance.BorderColor, 1f);
                graphics.DrawPath(border, path);
            }
            // Riflesso sottile lungo il bordo alto dei pulsanti pieni (blu, verde): da' un
            // minimo di rilievo senza sfumature pesanti.
            if (Enabled && !_pressed && fill.GetBrightness() < 0.5f && bounds.Width > radius * 2 + 6)
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
                using var ring = new Pen(Color.FromArgb(150, Color.White), 1.5f);
                using var outer = new Pen(Theme.Brand, 1.5f);
                graphics.DrawPath(Enabled && BackColor.GetBrightness() > 0.8f ? outer : ring, ringPath);
            }
        }

        var textBounds = new Rectangle(
            Padding.Left,
            0,
            Math.Max(1, ClientSize.Width - Padding.Horizontal),
            ClientSize.Height);
        TextRenderer.DrawText(
            graphics,
            Text,
            Font,
            textBounds,
            Enabled ? ForeColor : Theme.DisabledInk,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
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
