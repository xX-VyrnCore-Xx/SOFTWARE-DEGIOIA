using System.Drawing.Drawing2D;

namespace PresenzeXmlConverter;

/// <summary>Icona disegnata dalla barra di navigazione (nessuna immagine esterna).</summary>
internal enum NavIcon
{
    Dashboard,
    Convert,
    Codes,
    History
}

/// <summary>
/// Barra di navigazione verticale disegnata con la palette dell'app: le quattro
/// sezioni della finestra (Dashboard, Nuova conversione, Gestione codici,
/// Storico esportazioni). È il controllo che sostituisce la fila di schede in
/// alto: le voci sono più alte, il bersaglio del clic è più grande e la voce
/// attiva si legge a colpo d'occhio (fondo chiaro, testo e icona nel colore di
/// marca, barretta laterale).
///
/// Il disegno è fatto a mano per lo stesso motivo delle schede: i controlli
/// nativi di Windows restano grigi anche quando il resto della finestra ha il
/// tema dell'applicazione.
/// </summary>
internal sealed class NavRail : Control
{
    /// <summary>Altezza di una voce: un bersaglio comodo anche con il mouse.</summary>
    public const int ItemHeight = 46;

    /// <summary>Spazio fra due voci.</summary>
    public const int ItemGap = 4;

    /// <summary>Margine orizzontale della barra.</summary>
    public const int RailPadding = 10;

    /// <summary>Larghezza della barretta che segna la voce attiva.</summary>
    private const int AccentWidth = 3;

    private readonly List<Item> _items = new();
    private int _selectedIndex = -1;
    private int _hoverIndex = -1;

    public NavRail()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        BackColor = Theme.Canvas;
        TabStop = true;
        AccessibleRole = AccessibleRole.List;
        AccessibleName = "Sezioni dell'applicazione";
    }

    /// <summary>Numero di voci: il test grafico le disegna tutte.</summary>
    public int ItemCount => _items.Count;

    /// <summary>Voce selezionata (-1 se la barra è vuota).</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => Select(value, raiseEvent: false);
    }

    public event EventHandler? SelectionChanged;

    public void AddItem(string title, NavIcon icon, string? shortTitle = null)
    {
        _items.Add(new Item(title, icon, string.IsNullOrWhiteSpace(shortTitle) ? title : shortTitle));
        Invalidate();
    }

    private bool _useShortTitles;

    /// <summary>
    /// Con la finestra stretta la barra si restringe e usa le etichette brevi
    /// ("Conversione" invece di "Nuova conversione"): meglio un nome corto e intero
    /// che un nome lungo tagliato con i puntini.
    /// </summary>
    public bool UseShortTitles
    {
        get => _useShortTitles;
        set
        {
            if (_useShortTitles == value)
            {
                return;
            }
            _useShortTitles = value;
            Invalidate();
        }
    }

    /// <summary>Seleziona una voce e avvisa la finestra del cambio di sezione.</summary>
    public void Select(int index, bool raiseEvent = true)
    {
        if (_items.Count == 0)
        {
            return;
        }
        var clamped = Math.Clamp(index, 0, _items.Count - 1);
        if (clamped == _selectedIndex)
        {
            return;
        }
        _selectedIndex = clamped;
        Invalidate();
        if (raiseEvent)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Etichetta di una voce (per l'accessibilità e i test).</summary>
    public string ItemTitle(int index) =>
        index >= 0 && index < _items.Count ? _items[index].Title : string.Empty;

    /// <summary>Rettangolo pieno di una voce, con l'altezza reale della barra divisa fra le voci.</summary>
    private RectangleF ItemBox(int index)
    {
        var top = index * (ItemHeight + ItemGap);
        return new RectangleF(RailPadding, top, Math.Max(0, ClientSize.Width - (RailPadding * 2)), ItemHeight);
    }

    /// <summary>Indice della voce sotto il puntatore (-1 se nessuna).</summary>
    private int IndexAt(Point point)
    {
        for (var index = 0; index < _items.Count; index++)
        {
            var box = ItemBox(index);
            if (point.Y >= box.Top && point.Y < box.Bottom &&
                point.X >= box.Left - RailPadding && point.X < box.Right + RailPadding)
            {
                return index;
            }
        }
        return -1;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var background = new SolidBrush(BackColor))
        {
            e.Graphics.FillRectangle(background, ClientRectangle);
        }

        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            var box = ItemBox(index);
            var active = index == _selectedIndex;
            var hovered = index == _hoverIndex && !active;

            if (active || hovered)
            {
                using var path = Theme.RoundedRectangle(
                    Rectangle.Round(box),
                    9);
                using var fill = new SolidBrush(active ? Theme.BrandSoft : Theme.Sunken);
                e.Graphics.FillPath(fill, path);
            }
            if (active)
            {
                var accent = new Rectangle((int)box.Left, (int)box.Top + 10, AccentWidth, (int)box.Height - 20);
                using var path = Theme.RoundedRectangle(accent, AccentWidth / 2);
                using var brush = new SolidBrush(Theme.Brand);
                e.Graphics.FillPath(brush, path);
            }

            var ink = active ? Theme.BrandInk : Theme.MutedInk;
            var iconBox = new Rectangle(
                (int)box.Left + 14,
                (int)(box.Top + (box.Height - 18) / 2),
                18,
                18);
            using (var iconPen = new Pen(ink, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                DrawIcon(e.Graphics, iconBox, item.Icon, ink, iconPen);
            }

            var textBounds = new Rectangle(
                iconBox.Right + 10,
                (int)box.Top,
                Math.Max(1, (int)box.Right - iconBox.Right - 14),
                (int)box.Height);
            TextRenderer.DrawText(
                e.Graphics,
                _useShortTitles ? item.ShortTitle : item.Title,
                active ? Theme.Fonts.TabBold : Theme.Fonts.Tab,
                textBounds,
                ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>
    /// Icone disegnate con qualche tratto: quadrati per la dashboard, un foglio
    /// per la conversione, un elenco per i codici, un orologio per lo storico.
    /// Restano leggibili a 18 pixel e non richiedono file esterni.
    /// </summary>
    private static void DrawIcon(Graphics graphics, Rectangle box, NavIcon icon, Color ink, Pen pen)
    {
        switch (icon)
        {
            case NavIcon.Dashboard:
                var cell = box.Width / 2 - 2;
                foreach (var (x, y) in new[]
                         {
                             (box.Left, box.Top), (box.Left + cell + 3, box.Top),
                             (box.Left, box.Top + cell + 3), (box.Left + cell + 3, box.Top + cell + 3)
                         })
                {
                    using var path = Theme.RoundedRectangle(new Rectangle(x, y, cell, cell), 2);
                    using var brush = new SolidBrush(ink);
                    graphics.FillPath(brush, path);
                }
                break;

            case NavIcon.Convert:
                var page = new Rectangle(box.Left + 2, box.Top, box.Width - 5, box.Height);
                using (var path = Theme.RoundedRectangle(page, 2))
                {
                    graphics.DrawPath(pen, path);
                }
                graphics.DrawLine(pen, box.Left + 5, box.Top + 6, box.Right - 6, box.Top + 6);
                graphics.DrawLine(pen, box.Left + 5, box.Top + 10, box.Right - 6, box.Top + 10);
                graphics.DrawLine(pen, box.Left + 5, box.Top + 14, box.Left + 9, box.Top + 14);
                break;

            case NavIcon.Codes:
                for (var row = 0; row < 3; row++)
                {
                    var y = box.Top + 3 + (row * 6);
                    using var dot = new SolidBrush(ink);
                    graphics.FillEllipse(dot, box.Left + 1, y, 3, 3);
                    graphics.DrawLine(pen, box.Left + 7, y + 1, box.Right - 1, y + 1);
                }
                break;

            default:
                graphics.DrawEllipse(pen, box.Left + 1, box.Top + 1, box.Width - 2, box.Height - 2);
                graphics.DrawLine(pen, box.Left + box.Width / 2, box.Top + 5, box.Left + box.Width / 2, box.Top + box.Height / 2);
                graphics.DrawLine(
                    pen,
                    box.Left + box.Width / 2,
                    box.Top + box.Height / 2,
                    box.Right - 4,
                    box.Top + box.Height / 2 + 3);
                break;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var index = IndexAt(e.Location);
        if (index == _hoverIndex)
        {
            return;
        }
        _hoverIndex = index;
        Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex < 0)
        {
            return;
        }
        _hoverIndex = -1;
        Cursor = Cursors.Default;
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
        keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Down:
                Select(Math.Min(_selectedIndex + 1, _items.Count - 1));
                break;
            case Keys.Up:
                Select(Math.Max(_selectedIndex - 1, 0));
                break;
            case Keys.Home:
                Select(0);
                break;
            case Keys.End:
                Select(_items.Count - 1);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        Invalidate();
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        if (_hoverIndex >= 0)
        {
            _hoverIndex = -1;
        }
        Invalidate();
    }

    /// <summary>Altezza che serve a tutte le voci: la colonna la usa per restare compatta.</summary>
    public int PreferredRailHeight => (_items.Count * ItemHeight) + (Math.Max(0, _items.Count - 1) * ItemGap);

    private sealed record Item(string Title, NavIcon Icon, string ShortTitle);
}
