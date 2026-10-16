using System.Drawing.Drawing2D;

namespace PresenzeXmlConverter;

/// <summary>
/// Elenco dei risultati della ricerca globale, mostrato sotto il campo della testata. Non prende mai il fuoco:
/// si scrive sempre nel campo e si naviga con Su, Giù, Invio ed Esc; il mouse sceglie con un clic.
/// </summary>
internal sealed class SearchResultsPopup : Control
{
    public const int RowHeight = 44;

    private IReadOnlyList<SearchHit> _hits = [];
    private int _selected;
    private int _hot = -1;

    public SearchResultsPopup()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        Visible = false;
        BackColor = Theme.Surface;
        AccessibleRole = AccessibleRole.List;
        AccessibleName = "Risultati della ricerca";
    }

    /// <summary>L'utente ha scelto un risultato (clic o Invio).</summary>
    public event Action<SearchHit>? HitChosen;

    public int HitCount => _hits.Count;

    public SearchHit? Current => _selected >= 0 && _selected < _hits.Count ? _hits[_selected] : null;

    public int PreferredHeight => (_hits.Count * RowHeight) + 12;

    public void SetHits(IReadOnlyList<SearchHit> hits)
    {
        _hits = hits;
        _selected = 0;
        _hot = -1;
        AccessibleDescription = hits.Count == 0 ? "Nessun risultato" : $"{hits.Count} risultati";
        Invalidate();
    }

    public void Move(int delta)
    {
        if (_hits.Count == 0)
        {
            return;
        }
        _selected = (_selected + delta + _hits.Count) % _hits.Count;
        Invalidate();
    }

    public void ChooseCurrent()
    {
        if (Current is { } hit)
        {
            HitChosen?.Invoke(hit);
        }
    }

    private Rectangle RowBounds(int index) => new(6, 6 + (index * RowHeight), Width - 12, RowHeight);

    private int RowAt(Point point)
    {
        for (var index = 0; index < _hits.Count; index++)
        {
            if (RowBounds(index).Contains(point))
            {
                return index;
            }
        }
        return -1;
    }

    private static Glyph GlyphFor(SearchKind kind) => kind switch
    {
        SearchKind.Movements => Glyph.Table,
        SearchKind.Company => Glyph.Building,
        SearchKind.Employee => Glyph.Users,
        SearchKind.Policy => Glyph.Code,
        SearchKind.Command => Glyph.Arrow,
        _ => Glyph.File
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Canvas);
        var frame = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Theme.RoundedRectangle(frame, 12))
        using (var fill = new SolidBrush(Theme.Surface))
        using (var border = new Pen(Theme.DividerStrong, 1f))
        {
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        for (var index = 0; index < _hits.Count; index++)
        {
            var row = RowBounds(index);
            var active = index == _selected || index == _hot;
            if (active)
            {
                using var rowPath = Theme.RoundedRectangle(row, 8);
                using var rowFill = new SolidBrush(index == _selected ? Theme.BrandSoft : Theme.Sunken);
                g.FillPath(rowFill, rowPath);
            }
            var hit = _hits[index];
            var chip = new Rectangle(row.Left + 8, row.Top + ((row.Height - 28) / 2), 28, 28);
            using (var chipPath = Theme.RoundedRectangle(chip, 8))
            using (var chipFill = new SolidBrush(index == _selected ? Theme.Brand : Theme.BrandSoft))
            {
                g.FillPath(chipFill, chipPath);
            }
            Glyphs.Draw(g, Rectangle.Inflate(chip, -6, -6), GlyphFor(hit.Kind), index == _selected ? Color.White : Theme.BrandInk, 1.6f);

            var textLeft = chip.Right + 10;
            var textWidth = Math.Max(0, row.Right - textLeft - 8);
            TextRenderer.DrawText(
                g,
                hit.Title,
                Theme.Fonts.SmallBold,
                new Rectangle(textLeft, row.Top + 4, textWidth, (row.Height / 2) - 2),
                Theme.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(
                g,
                hit.Detail,
                Theme.Fonts.Tiny,
                new Rectangle(textLeft, row.Top + (row.Height / 2), textWidth, (row.Height / 2) - 4),
                Theme.MutedInk,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var row = RowAt(e.Location);
        if (row != _hot)
        {
            _hot = row;
            Cursor = row >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = -1;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var row = RowAt(e.Location);
        if (row >= 0)
        {
            _selected = row;
            HitChosen?.Invoke(_hits[row]);
        }
    }
}
