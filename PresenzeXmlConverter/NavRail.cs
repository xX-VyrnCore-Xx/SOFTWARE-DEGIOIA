using System.Drawing.Drawing2D;

namespace PresenzeXmlConverter;

/// <summary>Icona disegnata dalla barra di navigazione (nessuna immagine esterna).</summary>
internal enum NavIcon
{
    Dashboard,
    Convert,
    Company,
    Codes,
    History,
    Activity
}

/// <summary>
/// Barra di navigazione verticale disegnata con la palette dell'app: le cinque
/// sezioni della finestra (Dashboard, Nuova conversione, Aziende, Gestione codici,
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
    // Posizione (in pixel) della pillola della voce attiva: scorre da una voce all'altra invece di saltare.
    private AnimationClock? _pillClock;

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

    private readonly Dictionary<int, string> _badges = new();

    /// <summary>
    /// Contatore a destra di una voce (per esempio il numero di aziende salvate): si
    /// vede a colpo d'occhio che cosa c'è in ogni sezione. Testo vuoto = nessun contatore.
    /// </summary>
    public void SetBadge(int index, string? text)
    {
        if (index < 0 || index >= _items.Count)
        {
            return;
        }
        var changed = string.IsNullOrEmpty(text)
            ? _badges.Remove(index)
            : !_badges.TryGetValue(index, out var current) || current != text;
        if (!string.IsNullOrEmpty(text))
        {
            _badges[index] = text;
        }
        if (changed)
        {
            Invalidate();
        }
    }

    /// <summary>Testo del contatore di una voce (per i test).</summary>
    public string BadgeText(int index) => _badges.TryGetValue(index, out var text) ? text : string.Empty;

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
        var first = _selectedIndex < 0;
        _selectedIndex = clamped;
        MovePill(animate: !first);
        Invalidate();
        if (raiseEvent)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void MovePill(bool animate)
    {
        var target = ItemBox(_selectedIndex).Top;
        _pillClock ??= CreatePillClock(target);
        if (animate)
        {
            _pillClock.AnimateTo(target, Motion.Standard);
        }
        else
        {
            _pillClock.Jump(target);
        }
    }

    private AnimationClock CreatePillClock(float initial)
    {
        var clock = new AnimationClock(this, initial);
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
            _pillClock?.Dispose();
            _pillClock = null;
        }
        base.Dispose(disposing);
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

        // Stile Windows 11: la voce attiva è una pillola tenue del colore di marca, con una barretta
        // sul bordo sinistro e testo e icona nel colore di marca; il passaggio del mouse un velo grigio.
        // La pillola scorre fra le voci; il testo di ogni voce vira al colore di marca man mano che la copre.
        var pillTop = _selectedIndex < 0
            ? 0f
            : _pillClock?.Value ?? ItemBox(_selectedIndex).Top;
        if (_selectedIndex >= 0)
        {
            var pillBox = ItemBox(_selectedIndex);
            pillBox.Y = pillTop;
            using var pillPath = Theme.RoundedRectangle(Rectangle.Round(pillBox), 8);
            if (Theme.IsGlass)
            {
                // Vetro: pillola viola vivace sfumata, come nel riferimento, con testo e icona bianchi.
                using var glassFill = new LinearGradientBrush(
                    Rectangle.Round(pillBox),
                    Theme.BrandHover,
                    Theme.BrandPressed,
                    0f);
                e.Graphics.FillPath(glassFill, pillPath);
                using var sheen = new Pen(Color.FromArgb(70, 255, 255, 255), 1f);
                e.Graphics.DrawPath(sheen, pillPath);
            }
            else
            {
                using var pillFill = new SolidBrush(Theme.BrandSoft);
                e.Graphics.FillPath(pillFill, pillPath);
            }
            var barHeight = Math.Max(16, (int)Math.Round(pillBox.Height * 0.42f));
            var bar = new Rectangle(
                (int)pillBox.Left + 2,
                (int)Math.Round(pillBox.Top + ((pillBox.Height - barHeight) / 2f)),
                AccentWidth,
                barHeight);
            if (!Theme.IsGlass)
            {
                using var barPath = Theme.RoundedRectangle(bar, 2);
                using var barFill = new SolidBrush(Theme.Brand);
                e.Graphics.FillPath(barFill, barPath);
            }
        }

        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            var box = ItemBox(index);
            var active = index == _selectedIndex;
            var covered = _selectedIndex < 0
                ? 0f
                : Math.Clamp(1f - (Math.Abs(pillTop - box.Top) / ItemHeight), 0f, 1f);
            var hovered = index == _hoverIndex && !active && covered < 0.5f;

            if (hovered)
            {
                using var path = Theme.RoundedRectangle(
                    Rectangle.Round(box),
                    8);
                using var fill = new SolidBrush(Theme.Sunken);
                e.Graphics.FillPath(fill, path);
            }

            var ink = Mix(Theme.InkSecondary, Theme.IsGlass ? Color.White : Theme.BrandInk, covered);
            var iconBox = new Rectangle(
                (int)box.Left + 14,
                (int)(box.Top + (box.Height - 18) / 2),
                18,
                18);
            Glyphs.Draw(e.Graphics, iconBox, GlyphFor(item.Icon), ink, 1.7f);

            var badgeWidth = 0;
            if (_badges.TryGetValue(index, out var badge) && badge.Length > 0)
            {
                var badgeSize = TextRenderer.MeasureText(
                    badge,
                    Theme.Fonts.Tiny,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                badgeWidth = Math.Max(22, badgeSize.Width + 12);
                var pill = new Rectangle(
                    (int)box.Right - badgeWidth - 10,
                    (int)(box.Top + (box.Height - 20) / 2),
                    badgeWidth,
                    20);
                using var pillPath = Theme.RoundedRectangle(pill, 10);
                using var badgeFill = new SolidBrush(covered > 0.5f ? (Theme.IsGlass ? Color.FromArgb(70, Color.White) : Theme.BrandLite) : Theme.Sunken);
                e.Graphics.FillPath(badgeFill, pillPath);
                TextRenderer.DrawText(
                    e.Graphics,
                    badge,
                    Theme.Fonts.Tiny,
                    pill,
                    ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                badgeWidth += 8;
            }

            var textBounds = new Rectangle(
                iconBox.Right + 10,
                (int)box.Top,
                Math.Max(1, (int)box.Right - iconBox.Right - 14 - badgeWidth),
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

    /// <summary>L'icona della libreria che rappresenta una voce della barra.</summary>
    private static Glyph GlyphFor(NavIcon icon) => icon switch
    {
        NavIcon.Dashboard => Glyph.Dashboard,
        NavIcon.Convert => Glyph.Sheet,
        NavIcon.Company => Glyph.Building,
        NavIcon.Codes => Glyph.List,
        NavIcon.Activity => Glyph.File,
        _ => Glyph.Clock
    };

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
