using System.Drawing.Drawing2D;

namespace PresenzeXmlConverter;

/// <summary>
/// Interruttore in stile macOS al posto della casella di spunta di Windows: l'etichetta a sinistra
/// (a capo se è lunga) e a destra una pillola che scorre fra spento (grigio) e acceso (verde). È
/// una <see cref="CheckBox"/> a tutti gli effetti (Checked, CheckedChanged, tastiera, accessibilità):
/// cambia solo come si disegna. L'altezza della riga segue il testo, a qualunque scala dello schermo.
/// </summary>
internal sealed class ToggleSwitch : CheckBox
{
    private const int LogicalTrackWidth = 42;
    private const int LogicalTrackHeight = 24;
    private const int LogicalGap = 16;
    private const int LogicalVerticalPadding = 9;

    private AnimationClock? _clock;
    private bool _hot;

    public ToggleSwitch()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        AutoSize = true;
        Cursor = Cursors.Hand;
    }

    private int TrackWidth => this.Px(LogicalTrackWidth);

    private int TrackHeight => this.Px(LogicalTrackHeight);

    private int Gap => this.Px(LogicalGap);

    public override Size GetPreferredSize(Size proposedSize)
    {
        var width = proposedSize.Width > 0 ? proposedSize.Width : Width > 1 ? Width : Math.Max(200, Parent?.ClientSize.Width ?? 400);
        var textWidth = Math.Max(1, width - TrackWidth - Gap);
        var measured = TextRenderer.MeasureText(
            string.IsNullOrEmpty(Text) ? " " : Text,
            Font,
            new Size(textWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        var height = Math.Max(TrackHeight, measured.Height + 2) + (this.Px(LogicalVerticalPadding) * 2);
        return new Size(width, height);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (IsHandleCreated && Parent is not null && GetPreferredSize(new Size(Width, 0)).Height != Height)
        {
            Parent.PerformLayout(this, nameof(Size));
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Parent?.PerformLayout(this, nameof(Text));
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        _clock ??= CreateClock();
        _clock.AnimateTo(Checked ? 1f : 0f, Motion.Quick);
    }

    private AnimationClock CreateClock()
    {
        var clock = new AnimationClock(this, Checked ? 1f : 0f);
        clock.Frame += (_, _) => Invalidate();
        return clock;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _clock ??= CreateClock();
        _clock.Jump(Checked ? 1f : 0f);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hot = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = false;
        Invalidate();
    }

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
        using (var background = new SolidBrush(BackColor))
        {
            g.FillRectangle(background, ClientRectangle);
        }

        var track = new Rectangle(
            ClientSize.Width - TrackWidth,
            (ClientSize.Height - TrackHeight) / 2,
            TrackWidth,
            TrackHeight);
        var textBounds = new Rectangle(0, 0, Math.Max(1, track.Left - Gap), ClientSize.Height);
        var textColor = Enabled ? ForeColor : Theme.DisabledInk;
        UiText.Draw(
            g,
            Text,
            Font,
            textBounds,
            textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

        var amount = Math.Clamp(_clock?.Value ?? (Checked ? 1f : 0f), 0f, 1f);
        var off = Enabled ? Theme.DividerStrong : Theme.DisabledFill;
        var on = Enabled ? Theme.Success : Theme.DisabledInk;
        var fill = Mix(off, on, amount);
        if (_hot && Enabled)
        {
            fill = Mix(fill, Color.Black, 0.06f);
        }
        using (var path = Theme.RoundedRectangle(track, track.Height / 2))
        using (var brush = new SolidBrush(fill))
        {
            g.FillPath(brush, path);
        }

        var inset = this.Px(2);
        var knob = track.Height - (inset * 2);
        var travel = track.Width - (inset * 2) - knob;
        var knobBox = new Rectangle(track.Left + inset + (int)Math.Round(travel * amount), track.Top + inset, knob, knob);
        using (var shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
        {
            g.FillEllipse(shadow, new Rectangle(knobBox.Left, knobBox.Top + this.Px(1), knobBox.Width, knobBox.Height));
        }
        using (var knobBrush = new SolidBrush(Color.White))
        {
            g.FillEllipse(knobBrush, knobBox);
        }

        if (Focused && ShowFocusCues)
        {
            using var ring = new Pen(Theme.Brand, Math.Max(1.5f, this.Px(2f)));
            using var ringPath = Theme.RoundedRectangle(Rectangle.Inflate(track, this.Px(3), this.Px(3)), (track.Height / 2) + this.Px(3));
            g.DrawPath(ring, ringPath);
        }
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
            _clock?.Dispose();
            _clock = null;
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Scelta fra poche opzioni affiancate, in stile macOS (controllo segmentato): una pista tenue e, sotto
/// l'opzione scelta, una pillola bianca che scorre da un'opzione all'altra. Sostituisce i pulsanti radio di
/// Windows, piccoli e poco definiti. Frecce sinistra e destra cambiano la scelta.
/// </summary>
internal sealed class SegmentedControl : Control
{
    private const int LogicalHorizontalPadding = 18;
    private const int LogicalVerticalPadding = 9;
    private const int LogicalInset = 3;

    private readonly List<string> _items = new();
    private readonly List<object?> _tags = new();
    private int _selected;
    private int _hover = -1;
    private AnimationClock? _clock;

    public SegmentedControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        AutoSize = true;
        TabStop = true;
        AccessibleRole = AccessibleRole.List;
        Font = Theme.Fonts.Small;
        ForeColor = Theme.Ink;
    }

    public event EventHandler? SelectionChanged;

    /// <summary>Aggiunge un'opzione con il valore che rappresenta.</summary>
    public void AddItem(string text, object? tag = null)
    {
        _items.Add(text);
        _tags.Add(tag);
        PerformParentLayout();
        Invalidate();
    }

    public int ItemCount => _items.Count;

    public int SelectedIndex
    {
        get => _selected;
        set => Select(value, raiseEvent: false);
    }

    /// <summary>Valore dell'opzione scelta.</summary>
    public object? SelectedTag => _selected >= 0 && _selected < _tags.Count ? _tags[_selected] : null;

    /// <summary>Sceglie l'opzione che porta questo valore (se esiste).</summary>
    public void SelectTag(object? tag)
    {
        var index = _tags.FindIndex(candidate => Equals(candidate, tag));
        if (index >= 0)
        {
            SelectedIndex = index;
        }
    }

    private int SegmentWidth
    {
        get
        {
            var widest = 0;
            foreach (var item in _items)
            {
                widest = Math.Max(widest, TextRenderer.MeasureText(item, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width);
            }
            return widest + (this.Px(LogicalHorizontalPadding) * 2);
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var line = TextRenderer.MeasureText("Ag", Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Height;
        var height = line + (this.Px(LogicalVerticalPadding) * 2) + (this.Px(LogicalInset) * 2);
        var width = (SegmentWidth * Math.Max(1, _items.Count)) + (this.Px(LogicalInset) * 2);
        return new Size(width, height);
    }

    private void PerformParentLayout() => Parent?.PerformLayout(this, nameof(Size));

    private RectangleF SegmentBox(int index)
    {
        var inset = this.Px(LogicalInset);
        var count = Math.Max(1, _items.Count);
        var width = (ClientSize.Width - (inset * 2)) / (float)count;
        return new RectangleF(inset + (width * index), inset, width, ClientSize.Height - (inset * 2));
    }

    public void Select(int index, bool raiseEvent = true)
    {
        if (_items.Count == 0)
        {
            return;
        }
        var clamped = Math.Clamp(index, 0, _items.Count - 1);
        if (clamped == _selected)
        {
            return;
        }
        _selected = clamped;
        _clock ??= CreateClock();
        _clock.AnimateTo(clamped, Motion.Standard);
        Invalidate();
        if (raiseEvent)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private AnimationClock CreateClock()
    {
        var clock = new AnimationClock(this, _selected);
        clock.Frame += (_, _) => Invalidate();
        return clock;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _clock ??= CreateClock();
        _clock.Jump(_selected);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var background = new SolidBrush(BackColor))
        {
            g.FillRectangle(background, ClientRectangle);
        }
        if (_items.Count == 0)
        {
            return;
        }
        var track = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        using (var trackPath = Theme.RoundedRectangle(track, this.Px(9)))
        using (var trackFill = new SolidBrush(Theme.Sunken))
        {
            g.FillPath(trackFill, trackPath);
        }

        var position = Math.Clamp(_clock?.Value ?? _selected, 0f, _items.Count - 1);
        var first = SegmentBox(0);
        var thumb = new RectangleF(first.Left + (first.Width * position), first.Top, first.Width, first.Height);
        using (var thumbPath = Theme.RoundedRectangle(Rectangle.Round(thumb), this.Px(7)))
        {
            using (var shadow = new Pen(Color.FromArgb(34, 0, 0, 0), 1f))
            {
                var lifted = Rectangle.Round(thumb);
                lifted.Offset(0, 1);
                using var liftedPath = Theme.RoundedRectangle(lifted, this.Px(7));
                g.DrawPath(shadow, liftedPath);
            }
            using var thumbFill = new SolidBrush(Theme.Surface);
            g.FillPath(thumbFill, thumbPath);
            using var thumbEdge = new Pen(Theme.Divider, 1f);
            g.DrawPath(thumbEdge, thumbPath);
        }

        for (var index = 0; index < _items.Count; index++)
        {
            var box = Rectangle.Round(SegmentBox(index));
            var active = index == _selected;
            var ink = !Enabled ? Theme.DisabledInk : active ? Theme.Ink : (index == _hover ? Theme.Ink : Theme.MutedInk);
            UiText.Draw(
                g,
                _items[index],
                Font,
                box,
                ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        if (Focused && ShowFocusCues)
        {
            using var ring = new Pen(Theme.Brand, Math.Max(1.5f, this.Px(2f)));
            using var ringPath = Theme.RoundedRectangle(track, this.Px(9));
            g.DrawPath(ring, ringPath);
        }
    }

    private int IndexAt(Point point)
    {
        for (var index = 0; index < _items.Count; index++)
        {
            if (SegmentBox(index).Contains(point))
            {
                return index;
            }
        }
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var index = IndexAt(e.Location);
        if (index != _hover)
        {
            _hover = index;
            Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }
        Focus();
        var index = IndexAt(e.Location);
        if (index >= 0)
        {
            Select(index);
        }
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left)
        {
            Select(_selected - 1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Right)
        {
            Select(_selected + 1);
            e.Handled = true;
        }
    }

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clock?.Dispose();
            _clock = null;
        }
        base.Dispose(disposing);
    }
}
