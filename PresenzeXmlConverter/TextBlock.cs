namespace PresenzeXmlConverter;

/// <summary>
/// Testo che si misura da solo. Una riga di un layout ha l'altezza del suo testo,
/// non un numero di pixel scritto a mano: con il carattere più grande di quello
/// previsto (scala 125% o 150% di Windows, un altro tipo di carattere) il titolo non
/// si sovrappone più al sottotitolo né perde la metà inferiore delle lettere.
/// Va messo in una riga <see cref="SizeType.AutoSize"/> di una tabella.
/// </summary>
internal sealed class TextBlock : Control
{
    private bool _wrap;
    private ContentAlignment _textAlign = ContentAlignment.TopLeft;

    public TextBlock()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        AutoSize = true;
        TabStop = false;
        AccessibleRole = AccessibleRole.StaticText;
    }

    /// <summary>A capo automatico sulla larghezza disponibile (altrimenti una riga sola, con i puntini).</summary>
    public bool Wrap
    {
        get => _wrap;
        set
        {
            if (_wrap == value)
            {
                return;
            }
            _wrap = value;
            RefreshLayout();
        }
    }

    public ContentAlignment TextAlign
    {
        get => _textAlign;
        set
        {
            _textAlign = value;
            Invalidate();
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = string.IsNullOrEmpty(Text) ? " " : Text;
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        if (_wrap)
        {
            var width = proposedSize.Width > 0
                ? proposedSize.Width
                : Width > 1 ? Width : Math.Max(1, Parent?.ClientSize.Width ?? 400);
            var measured = TextRenderer.MeasureText(
                text,
                Font,
                new Size(Math.Max(1, width - Padding.Horizontal), int.MaxValue),
                flags | TextFormatFlags.WordBreak);
            return new Size(width, measured.Height + Padding.Vertical + 2);
        }
        var line = TextRenderer.MeasureText(text, Font, new Size(int.MaxValue / 2, int.MaxValue), flags | TextFormatFlags.SingleLine);
        return new Size(line.Width + Padding.Horizontal, line.Height + Padding.Vertical + 2);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (var background = new SolidBrush(BackColor))
        {
            e.Graphics.FillRectangle(background, ClientRectangle);
        }
        var bounds = new Rectangle(
            Padding.Left,
            Padding.Top,
            Math.Max(1, ClientSize.Width - Padding.Horizontal),
            Math.Max(1, ClientSize.Height - Padding.Vertical));
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        flags |= _wrap ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        flags |= _textAlign switch
        {
            ContentAlignment.MiddleLeft => TextFormatFlags.VerticalCenter,
            ContentAlignment.MiddleCenter => TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter,
            ContentAlignment.MiddleRight => TextFormatFlags.VerticalCenter | TextFormatFlags.Right,
            ContentAlignment.TopRight => TextFormatFlags.Right,
            ContentAlignment.TopCenter => TextFormatFlags.HorizontalCenter,
            _ => TextFormatFlags.Default
        };
        UiText.Draw(e.Graphics, Text, Font, bounds, Enabled ? ForeColor : Theme.DisabledInk, flags);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        AccessibleName = Text;
        RefreshLayout();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RefreshLayout();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        // Con il testo a capo, un'altra larghezza cambia l'altezza che serve: la tabella ricalcola la riga.
        if (_wrap && IsHandleCreated && Parent is not null && GetPreferredSize(new Size(Width, 0)).Height != Height)
        {
            Parent.PerformLayout(this, nameof(Size));
        }
    }

    private void RefreshLayout()
    {
        Invalidate();
        if (Parent is not null)
        {
            Parent.PerformLayout(this, nameof(Text));
        }
    }
}
