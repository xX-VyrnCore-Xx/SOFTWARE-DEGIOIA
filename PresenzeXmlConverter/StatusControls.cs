using System.Drawing.Drawing2D;

namespace PresenzeXmlConverter;

/// <summary>
/// Barra di stato in fondo alla finestra: a sinistra il messaggio con un pallino colorato, a destra segmenti separati
/// da una linea sottile (aggiornamenti, versione, ora), ognuno con la sua icona. Se manca spazio spariscono prima
/// l'ora e la versione; il messaggio ha sempre un minimo garantito.
/// </summary>
internal sealed class StatusBar : Control
{
    private const int MinimumMessage = 180;
    private const int Gap = 14;
    private const int IconSize = 13;
    private const int IconGap = 6;

    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 30_000 };
    private string _message = StatusBarModel.ReadyMessage;
    private bool _messageWarning;
    private string _updates = "verifica in corso";
    private bool _updatesWarning;
    private string _version = string.Empty;
    private Rectangle _updatesBounds = Rectangle.Empty;
    private bool _updatesHot;

    public StatusBar()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.Canvas;
        Height = 28;
        AccessibleRole = AccessibleRole.StatusBar;
        AccessibleName = "Barra di stato";
        _clock.Tick += (_, _) =>
        {
            if (Visible)
            {
                Invalidate();
            }
        };
        _clock.Start();
    }

    /// <summary>Un clic sul segmento degli aggiornamenti: la finestra verifica subito se c'è una versione nuova.</summary>
    public event EventHandler? UpdatesClicked;

    public void SetMessage(string text, bool isWarning)
    {
        _message = StatusBarModel.CleanMessage(text);
        _messageWarning = isWarning;
        AccessibleDescription = $"{_message}. Aggiornamenti: {_updates}";
        Invalidate();
    }

    public void SetUpdates(string text, bool isWarning)
    {
        _updates = StatusBarModel.CleanUpdates(text);
        _updatesWarning = isWarning;
        AccessibleDescription = $"{_message}. Aggiornamenti: {_updates}";
        Invalidate();
    }

    public void SetVersion(string text)
    {
        _version = text;
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clock.Dispose();
        }
        base.Dispose(disposing);
    }

    private static int TextWidth(string text, Font font) => ModernPalette.Measure(text, font).Width;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Canvas);
        var font = Theme.Fonts.Tiny;
        var pad = 12;

        var clockText = StatusBarModel.ClockText(DateTime.Now);
        var segments = new List<(string Text, Glyph Glyph, Color Ink, bool IsUpdates)>
        {
            (_updates, Glyph.Refresh, _updatesWarning ? Theme.WarningInk : Theme.MutedInk, true)
        };
        if (_version.Length > 0)
        {
            segments.Add((_version, Glyph.Chip, Theme.MutedInk, false));
        }
        segments.Add((clockText, Glyph.Clock, Theme.MutedInk, false));

        var widths = segments.Select(s => IconSize + IconGap + TextWidth(s.Text, font)).ToList();
        var available = Width - (pad * 2);
        var kept = StatusBarModel.SegmentsThatFit(available, MinimumMessage, widths, Gap * 2 + 1);
        // Il primo segmento (gli aggiornamenti) si accorcia con i puntini se ancora non basta.
        var firstMax = Math.Max(60, available - MinimumMessage);
        if (kept == 1 && widths[0] > firstMax)
        {
            widths[0] = firstMax;
        }

        // Disegno da destra a sinistra.
        var x = Width - pad;
        _updatesBounds = Rectangle.Empty;
        for (var index = kept - 1; index >= 0; index--)
        {
            var segment = segments[index];
            var width = widths[index];
            x -= width;
            var box = new Rectangle(x, 0, width, Height);
            var ink = segment.IsUpdates && _updatesHot ? Theme.Ink : segment.Ink;
            Glyphs.Draw(g, new Rectangle(box.Left, (Height - IconSize) / 2, IconSize, IconSize), segment.Glyph, ink, 1.4f);
            TextRenderer.DrawText(
                g,
                segment.Text,
                font,
                new Rectangle(box.Left + IconSize + IconGap, 0, Math.Max(0, width - IconSize - IconGap), Height),
                ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            if (segment.IsUpdates)
            {
                _updatesBounds = Rectangle.Inflate(box, 6, 0);
            }
            if (index > 0)
            {
                x -= Gap;
                using var divider = new Pen(Theme.Divider, 1f);
                g.DrawLine(divider, x, (Height / 2) - 7, x, (Height / 2) + 7);
                x -= Gap;
            }
        }

        // Messaggio a sinistra: pallino disegnato (verde, o giallo se è un avviso) e testo.
        var messageRight = Math.Max(pad, x - Gap);
        var dot = new Rectangle(pad, (Height - 8) / 2, 8, 8);
        using (var dotBrush = new SolidBrush(_messageWarning ? Theme.Warning : Theme.Success))
        {
            g.FillEllipse(dotBrush, dot);
        }
        TextRenderer.DrawText(
            g,
            _message,
            font,
            new Rectangle(dot.Right + 8, 0, Math.Max(0, messageRight - dot.Right - 8), Height),
            _messageWarning ? Theme.WarningInk : Theme.InkSecondary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hot = !_updatesBounds.IsEmpty && _updatesBounds.Contains(e.Location);
        if (hot != _updatesHot)
        {
            _updatesHot = hot;
            Cursor = hot ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_updatesHot)
        {
            _updatesHot = false;
            Cursor = Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (!_updatesBounds.IsEmpty && _updatesBounds.Contains(e.Location))
        {
            UpdatesClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>
/// Blocco «Dati locali» in fondo alla barra laterale: titolo, stato del database con pallino, ultimo backup,
/// azienda in uso e versione, ognuno con la sua icona. Legge i testi dalle etichette di stato della finestra
/// (che restano il punto in cui il resto del codice li aggiorna) e si ridisegna quando cambiano.
/// </summary>
internal sealed class RailStatusCard : Control
{
    private readonly Label _data;
    private readonly Label _backup;
    private readonly Label _company;
    private readonly string _version;

    public RailStatusCard(Label data, Label backup, Label company, string version)
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        _data = data;
        _backup = backup;
        _company = company;
        _version = version;
        BackColor = Theme.Canvas;
        AccessibleRole = AccessibleRole.Grouping;
        AccessibleName = "Dati locali";
        foreach (var label in new[] { data, backup, company })
        {
            label.TextChanged += (_, _) => Invalidate();
            label.ForeColorChanged += (_, _) => Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Canvas);

        var card = new Rectangle(8, 4, Width - 16, Height - 12);
        if (card.Width < 60 || card.Height < 60)
        {
            return;
        }
        using (var path = Theme.RoundedRectangle(card, 10))
        using (var fill = new SolidBrush(Theme.Surface))
        using (var border = new Pen(Theme.Divider, 1f))
        {
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        var left = card.Left + 12;
        var right = card.Right - 10;
        var y = card.Top + 8;
        TextRenderer.DrawText(
            g,
            "DATI LOCALI",
            Theme.Fonts.Section,
            new Rectangle(left, y, right - left, 14),
            Theme.FaintInk,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        y += 20;

        var dataText = StatusBarModel.CleanRailLine(_data.Text);
        var dot = new Rectangle(left, y + 5, 8, 8);
        using (var dotBrush = new SolidBrush(ToneColor(_data.ForeColor)))
        {
            g.FillEllipse(dotBrush, dot);
        }
        TextRenderer.DrawText(
            g,
            dataText,
            Theme.Fonts.SmallBold,
            new Rectangle(dot.Right + 8, y, right - dot.Right - 8, 18),
            _data.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        y += 20;

        foreach (var (glyph, text) in new[]
                 {
                     (Glyph.Database, StatusBarModel.CleanRailLine(_backup.Text)),
                     (Glyph.Building, StatusBarModel.CleanRailLine(_company.Text)),
                     (Glyph.Chip, $"Versione {_version}")
                 })
        {
            Glyphs.Draw(g, new Rectangle(left - 1, y + 2, 13, 13), glyph, Theme.FaintInk, 1.3f);
            TextRenderer.DrawText(
                g,
                text,
                Theme.Fonts.Tiny,
                new Rectangle(left + 18, y, right - left - 18, 17),
                Theme.MutedInk,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            y += 19;
        }
    }

    /// <summary>Il pallino prende il colore pieno corrispondente al testo (verde, giallo, rosso).</summary>
    private static Color ToneColor(Color ink) =>
        ink == Theme.WarningInk ? Theme.Warning :
        ink == Theme.DangerInk ? Theme.Danger :
        Theme.Success;
}

/// <summary>Suggerimento a fine pagina: icona «i» e testo attenuato, con «Suggerimento» in evidenza.</summary>
internal sealed class HintLine : Control
{
    private const string Lead = "Suggerimento:";
    private readonly string _label;
    private readonly string _body;

    public HintLine(string text)
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        if (text.StartsWith(Lead, StringComparison.OrdinalIgnoreCase))
        {
            _label = "Suggerimento";
            _body = text[Lead.Length..].Trim();
        }
        else
        {
            _label = string.Empty;
            _body = text;
        }
        BackColor = Theme.Canvas;
        AccessibleRole = AccessibleRole.StaticText;
        AccessibleName = text;
        Height = 24;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Canvas);
        Glyphs.Draw(g, new Rectangle(2, (Height - 13) / 2, 13, 13), Glyph.Info, Theme.FaintInk, 1.3f);
        var x = 22;
        if (_label.Length > 0)
        {
            var labelWidth = ModernPalette.Measure(_label, Theme.Fonts.SmallBold).Width;
            TextRenderer.DrawText(
                g,
                _label,
                Theme.Fonts.SmallBold,
                new Rectangle(x, 0, labelWidth + 2, Height),
                Theme.MutedInk,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            x += labelWidth + 8;
        }
        TextRenderer.DrawText(
            g,
            _body,
            Theme.Fonts.Tiny,
            new Rectangle(x, 0, Math.Max(0, Width - x - 4), Height),
            Theme.MutedInk,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }
}
