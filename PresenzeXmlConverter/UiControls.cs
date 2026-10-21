using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PresenzeXmlConverter;

/// <summary>
/// Griglia con doppio buffering e stile di tema già applicato: senza
/// buffering la griglia sfarfalla durante il ridimensionamento e ogni
/// finestra doveva ricordarsi di ridipingere intestazioni e selezioni.
/// </summary>
internal sealed class ThemedGrid : DataGridView
{
    public ThemedGrid()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToOrderColumns = false;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        BackgroundColor = Theme.Surface;
        BackColor = Theme.Surface;
        BorderStyle = BorderStyle.None;
        GridColor = Theme.Divider;
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        RowHeadersVisible = false;
        // Righe e intestazioni hanno l'altezza decisa dall'app: trascinando il bordo di una riga la si poteva
        // allargare a dismisura (una riga alta mezza schermata) e la tabella non tornava più com'era.
        AllowUserToResizeRows = false;
        RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        SelectionMode = DataGridViewSelectionMode.CellSelect;
        MultiSelect = false;
        EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        EnableHeadersVisualStyles = false;
        ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
        // Intestazione senza le linee scure di sistema che incorniciavano ogni cella:
        // una fascia chiara con il testo di supporto, come nelle app curate.
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        ColumnHeadersDefaultCellStyle.BackColor = Theme.Sunken;
        ColumnHeadersDefaultCellStyle.ForeColor = Theme.MutedInk;
        ColumnHeadersDefaultCellStyle.Font = Theme.Fonts.GridHeader;
        ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 4, 0);
        // Le intestazioni stanno su una riga (con i puntini se serve): a capo spezzerebbero «Codice PAGHE» su due righe.
        ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Sunken;
        ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.InkSecondary;
        ColumnHeadersHeight = 38;
        RowTemplate.Height = 38;
        DefaultCellStyle.Font = Theme.Fonts.Grid;
        DefaultCellStyle.ForeColor = Theme.Ink;
        DefaultCellStyle.BackColor = Theme.Surface;
        DefaultCellStyle.SelectionBackColor = Theme.BrandLite;
        DefaultCellStyle.SelectionForeColor = Theme.Ink;
        DefaultCellStyle.Padding = new Padding(9, 0, 5, 0);
        AlternatingRowsDefaultCellStyle.BackColor = Theme.AltRow;
    }

    /// <summary>
    /// Selezione per riga intera, per le tabelle di sola lettura i cui pulsanti agiscono sulla riga scelta
    /// (aziende, storico, esportazioni): con la selezione di cella <c>SelectedRows</c> resta vuoto e
    /// «Elimina», «Usa», «Apri» non trovano nulla su cui lavorare.
    /// </summary>
    public void UseRowSelection()
    {
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        MultiSelect = false;
    }

    /// <summary>
    /// Testo mostrato al centro quando non c'e' nessuna riga visibile (griglia
    /// ancora vuota, oppure filtro che non trova nulla): senza, l'area di
    /// revisione resta un rettangolo bianco e sembra che il programma non abbia
    /// caricato niente. Non e' un'etichetta ma testo dipinto, quindi non entra
    /// nei controlli del layout.
    /// </summary>
    public string EmptyStateText { get; set; } = string.Empty;

    private static Color HoverRow => Theme.HoverRow;
    private int _hoverRow = -1;

    protected override void OnCellMouseEnter(DataGridViewCellEventArgs e)
    {
        base.OnCellMouseEnter(e);
        SetHoverRow(e.RowIndex);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHoverRow(-1);
    }

    private void SetHoverRow(int row)
    {
        if (row == _hoverRow)
        {
            return;
        }
        var previous = _hoverRow;
        _hoverRow = row;
        if (previous >= 0 && previous < RowCount)
        {
            InvalidateRow(previous);
        }
        if (row >= 0 && row < RowCount)
        {
            InvalidateRow(row);
        }
    }

    protected override void OnCellFormatting(DataGridViewCellFormattingEventArgs e)
    {
        base.OnCellFormatting(e);
        // Solo le celle con i colori di base: le righe evidenziate dai controlli (avvisi, errori)
        // e la selezione conservano il proprio colore.
        if (e.RowIndex == _hoverRow && e.CellStyle is { } style &&
            (style.BackColor.ToArgb() == Theme.Surface.ToArgb() || style.BackColor.ToArgb() == Theme.AltRow.ToArgb()))
        {
            style.BackColor = HoverRow;
        }
    }

    /// <summary>Icona sopra il testo dello stato vuoto: la pagina vuota ha un volto, non solo una riga grigia.</summary>
    public Glyph EmptyGlyph { get; set; } = Glyph.File;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (EmptyStateText.Length == 0 || HasVisibleRows || ClientSize.Width < 80)
        {
            return;
        }
        var top = ColumnHeadersVisible ? ColumnHeadersHeight : 0;
        var bounds = new Rectangle(0, top, ClientSize.Width, Math.Max(1, ClientSize.Height - top));
        var chip = 48;
        var showIcon = bounds.Height >= chip + 70;
        if (showIcon)
        {
            var chipBox = new Rectangle(
                bounds.Left + ((bounds.Width - chip) / 2),
                bounds.Top + ((bounds.Height - (chip + 14 + (Theme.Fonts.Small.Height * 2))) / 2),
                chip,
                chip);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var chipPath = Theme.RoundedRectangle(chipBox, 14))
            using (var chipBrush = new SolidBrush(Theme.BrandSoft))
            {
                e.Graphics.FillPath(chipBrush, chipPath);
            }
            Glyphs.Draw(e.Graphics, Rectangle.Inflate(chipBox, -13, -13), EmptyGlyph, Theme.BrandInk, 1.7f);
            bounds = new Rectangle(bounds.Left + 20, chipBox.Bottom + 8, bounds.Width - 40, Theme.Fonts.Small.Height * 2 + 6);
            UiText.Draw(
                e.Graphics,
                EmptyStateText,
                Theme.Fonts.Small,
                bounds,
                Theme.MutedInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            return;
        }
        UiText.Draw(
            e.Graphics,
            EmptyStateText,
            Theme.Fonts.Small,
            bounds,
            Theme.FaintInk,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
    }

    /// <summary>Almeno una riga visibile: quando il filtro non trova nulla si mostra lo stato vuoto.</summary>
    private bool HasVisibleRows
    {
        get
        {
            foreach (DataGridViewRow row in Rows)
            {
                if (row.Visible)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

/// <summary>
/// Campo di testo con bordo arrotondato disegnato a mano: il TextBox
/// nativo ha un bordo rettangolare fisso e grigio che stona con le card
/// arrotondate. Il testo resta un TextBox reale (selezione, incolla,
/// IME, tastiera) e il placeholder sparisce appena si scrive o si entra.
/// </summary>
internal sealed class RoundedField : Panel
{
    private const int Radius = 8;
    private const int EmSetCueBanner = 0x1501;
    private readonly TextBox _input = new();
    private string _placeholder = string.Empty;
    private bool _onDark;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, string lParam);

    public RoundedField()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Height = 36;
        Padding = new Padding(12, 2, 12, 2);

        _input.BorderStyle = BorderStyle.None;
        _input.Font = Theme.Fonts.Ui;
        _input.BackColor = Theme.FieldFill;
        _input.ForeColor = Theme.Ink;
        _input.Margin = Padding.Empty;
        _input.GotFocus += (_, _) => Invalidate();
        _input.LostFocus += (_, _) => Invalidate();
        // Il testo guida è quello nativo di Windows (cue banner) e non più un'etichetta
        // sovrapposta: l'etichetta stava sopra il campo anche quando era attivo e
        // copriva il cursore lampeggiante. Il cue banner sparisce appena il campo
        // prende il focus e il cursore si vede sempre.
        _input.HandleCreated += (_, _) => ApplyPlaceholder();
        Controls.Add(_input);
    }

    /// <summary>Il TextBox sottostante, per gli attributi non replicati qui.</summary>
    public TextBox Input => _input;

    public string Value
    {
        get => _input.Text;
        set => _input.Text = value;
    }

    public string Placeholder
    {
        get => _placeholder;
        set
        {
            _placeholder = value ?? string.Empty;
            ApplyPlaceholder();
        }
    }

    private void ApplyPlaceholder()
    {
        if (_input.IsHandleCreated)
        {
            SendMessage(_input.Handle, EmSetCueBanner, IntPtr.Zero, _placeholder);
        }
    }

    /// <summary>
    /// Variante per l'intestazione scura: fondo e bordo in tono con la fascia,
    /// testo chiaro e lente di ricerca a sinistra. Un campo bianco su una fascia
    /// blu notte è la prima cosa che fa sembrare l'interfaccia improvvisata.
    /// </summary>
    public bool OnDark
    {
        get => _onDark;
        set
        {
            _onDark = value;
            _input.BackColor = FillColor;
            _input.ForeColor = value ? Theme.HeaderInk : Theme.Ink;
            Padding = value ? new Padding(38, 2, 12, 2) : new Padding(12, 2, 12, 2);
            PerformLayout();
            Invalidate();
        }
    }

    private Color FillColor => _onDark ? Theme.HeaderFieldFill : Theme.FieldFill;

    public int MaxLength
    {
        get => _input.MaxLength;
        set => _input.MaxLength = value;
    }

    public CharacterCasing CharacterCasing
    {
        get => _input.CharacterCasing;
        set => _input.CharacterCasing = value;
    }

    public Font InputFont
    {
        get => _input.Font;
        set => _input.Font = value;
    }

    /// <summary>Riassegna il focus al campo di testo interno.</summary>
    public void FocusInput() => _input.Focus();

    public void Clear() => _input.Clear();

    public new event EventHandler TextChanged
    {
        add => _input.TextChanged += value;
        remove => _input.TextChanged -= value;
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        // Un TextBox monoriga ha un'altezza naturale minore della scatola:
        // lo si centra a mano nel campo.
        var innerHeight = _input.PreferredHeight;
        var top = Math.Max(0, (ClientSize.Height - innerHeight) / 2);
        var width = Math.Max(0, ClientSize.Width - Padding.Horizontal);
        _input.SetBounds(Padding.Left, top, width, innerHeight);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        // Un clic sul bordo del campo (fuori dal TextBox) mette comunque il cursore dentro.
        _input.Focus();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var bounds = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        if (bounds.Width < 2 || bounds.Height < 2)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundedRectangle(bounds, Radius);
        using (var fill = new SolidBrush(FillColor))
        {
            e.Graphics.FillPath(fill, path);
        }
        if (_onDark)
        {
            var lens = new Rectangle(12, (ClientSize.Height - 16) / 2, 16, 16);
            Glyphs.Draw(e.Graphics, lens, Glyph.Search, _input.Focused ? Theme.HeaderInk : Theme.HeaderMuted, 1.6f);
        }
        // Il bordo si accende quando il campo ha il focus, con un alone morbido:
        // l'utente vede subito dove finirà ciò che sta scrivendo.
        if (_input.Focused)
        {
            var ringBounds = Rectangle.Inflate(bounds, -1, -1);
            if (ringBounds.Width > 2 && ringBounds.Height > 2)
            {
                using var ringPath = Theme.RoundedRectangle(ringBounds, Radius - 1);
                using var ring = new Pen(_onDark ? Color.FromArgb(70, Theme.Brand) : Theme.BrandLite, 3f);
                e.Graphics.DrawPath(ring, ringPath);
            }
        }
        var idleBorder = _onDark ? Theme.HeaderFieldBorder : Theme.DividerStrong;
        var activeBorder = Theme.Brand;
        using var pen = new Pen(_input.Focused ? activeBorder : idleBorder, 1f);
        e.Graphics.DrawPath(pen, path);
    }
}

/// <summary>
/// Striscia decorativa con gradiente orizzontale: usata come accento in cima
/// all'header per dare profondità senza ricorrere a immagini esterne.
/// </summary>
/// <summary>
/// Marchio dell'app nell'intestazione: quadrato arrotondato con la sfumatura di marca e una
/// spunta bianca. Sostituisce l'immagine dell'icona, che sul fondo scuro risultava un
/// rettangolo azzurro generico.
/// </summary>
internal sealed class BrandMark : Control
{
    public BrandMark()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        Size = new Size(42, 42);
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = "Vyrn Presence";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var back = new SolidBrush(Parent?.BackColor ?? Theme.HeaderFill))
        {
            graphics.FillRectangle(back, ClientRectangle);
        }
        var box = new Rectangle(0, 0, Width - 1, Height - 1);
        if (box.Width < 12 || box.Height < 12)
        {
            return;
        }
        using var path = Theme.RoundedRectangle(box, 12);
        using (var gradient = new System.Drawing.Drawing2D.LinearGradientBrush(
                   box, Theme.Brand, Theme.Accent, 45f))
        {
            graphics.FillPath(gradient, path);
        }
        // Riflesso morbido in alto e filo di luce sul contorno.
        var state = graphics.Save();
        graphics.SetClip(path, System.Drawing.Drawing2D.CombineMode.Replace);
        using (var shine = new System.Drawing.Drawing2D.LinearGradientBrush(
                   new Rectangle(0, 0, box.Width + 1, box.Height / 2 + 1),
                   Color.FromArgb(70, Color.White),
                   Color.FromArgb(0, Color.White),
                   90f))
        {
            graphics.FillRectangle(shine, 0, 0, box.Width + 1, box.Height / 2 + 1);
        }
        graphics.Restore(state);
        using (var rim = new Pen(Color.FromArgb(60, Color.White), 1f))
        {
            graphics.DrawPath(rim, path);
        }
        Glyphs.Draw(graphics, Rectangle.Inflate(box, -11, -11), Glyph.Check, Color.White, 2.6f);
    }
}

internal sealed class AccentBar : Control
{
    public AccentBar()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.Brand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (ClientSize.Width < 2 || ClientSize.Height < 2)
        {
            base.OnPaint(e);
            return;
        }
        using var brush = new LinearGradientBrush(
            ClientRectangle,
            Theme.Brand,
            Theme.Accent,
            LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}

/// <summary>
/// Barra di avanzamento temizzata (sostituisce il ProgressBar nativo, che
/// ignora il tema e mostra sempre la grigia di sistema). Traccia chiara e
/// riempimento sfumato brand → accento, con angoli arrotondati.
/// </summary>
internal sealed class ProgressStrip : Control
{
    private int _percent;

    public ProgressStrip()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        Height = 16;
        BackColor = Theme.Sunken;
    }

    public int Value
    {
        get => _percent;
        set
        {
            _percent = Math.Clamp(value, 0, 100);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var bounds = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        if (bounds.Width < 4 || bounds.Height < 4)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var radius = Math.Max(4, bounds.Height / 2);
        using (var track = Theme.RoundedRectangle(bounds, radius))
        using (var trackBrush = new SolidBrush(Theme.Sunken))
        {
            e.Graphics.FillPath(trackBrush, track);
        }

        var filledWidth = (int)Math.Round(bounds.Width * (_percent / 100.0));
        if (filledWidth < 4)
        {
            return;
        }
        var fillBounds = new Rectangle(0, 0, filledWidth, bounds.Height);
        using var fillPath = Theme.RoundedRectangle(fillBounds, radius);
        using var brush = new LinearGradientBrush(
            bounds,
            Theme.Brand,
            Theme.Accent,
            LinearGradientMode.Horizontal);
        e.Graphics.FillPath(brush, fillPath);
    }
}

/// <summary>
/// Schede a pagina singola con barra disegnata in casa.
/// La striscia del TabControl nativo viene dipinta dal controllo di Windows
/// con il grigio di sistema ed ignora BackColor: qui barra, testo e
/// sottolineatura di selezione usano la palette del tema.
/// </summary>
internal sealed class ThemedTabControl : Control
{
    private readonly Panel _strip = new() { Dock = DockStyle.Top, Height = 40 };
    private readonly FlowLayoutPanel _flow = new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false,
        Padding = new Padding(12, 0, 0, 0)
    };
    private readonly Panel _content = new() { Dock = DockStyle.Fill };
    private readonly List<TabButton> _tabs = new();
    private readonly List<Control> _pages = new();
    private int _selectedIndex = -1;

    public ThemedTabControl()
    {
        BackColor = Theme.Surface;
        AccessibleRole = AccessibleRole.PageTabList;
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        _strip.BackColor = Theme.Surface;
        _flow.BackColor = Theme.Surface;
        _content.BackColor = Theme.Surface;
        _strip.Controls.Add(_flow);
        _strip.Controls.Add(new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = Theme.Divider
        });
        Controls.Add(_content);
        Controls.Add(_strip);
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SelectTab(value);
    }

    /// <summary>Numero di schede aggiunte: il test grafico le disegna tutte.</summary>
    public int PageCount => _pages.Count;

    public void AddPage(string title, Control page)
    {
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        _content.Controls.Add(page);
        _pages.Add(page);

        var tab = new TabButton(title, _tabs.Count);
        tab.Click += (_, _) => SelectTab(tab.Index);
        // Il nome accessibile della voce resta allineato alla sua scritta: la
        // barra è disegnata a mano e senza questo un lettore di schermo
        // annuncerebbe solo "pulsante".
        tab.AccessibleName = title;
        tab.AccessibleRole = AccessibleRole.PageTab;
        tab.AccessibleDescription = $"Mostra la scheda \u201c{title}\u201d.";
        _tabs.Add(tab);
        _flow.Controls.Add(tab);
    }

    public void SetPageText(int index, string text)
    {
        if (index >= 0 && index < _tabs.Count)
        {
            _tabs[index].Text = text;
            _tabs[index].AccessibleName = text;
            _tabs[index].AccessibleDescription = $"Mostra la scheda \u201c{text}\u201d.";
        }
    }

    private void SelectTab(int index)
    {
        if (_pages.Count == 0)
        {
            return;
        }
        _selectedIndex = Math.Clamp(index, 0, _pages.Count - 1);
        for (var position = 0; position < _tabs.Count; position++)
        {
            var active = position == _selectedIndex;
            _tabs[position].Active = active;
            _tabs[position].ForeColor = active ? Theme.BrandInk : Theme.MutedInk;
            _tabs[position].Font = active ? Theme.Fonts.TabBold : Theme.Fonts.Tab;
            _tabs[position].Invalidate();
        }
        for (var position = 0; position < _pages.Count; position++)
        {
            _pages[position].Visible = position == _selectedIndex;
        }
    }

    /// <summary>
    /// Voce di barra in stile macOS: pillola arrotondata azzurra per la scheda attiva, velo
    /// grigio al passaggio del mouse, niente sottolineatura. È dipinta per intero, così il
    /// pulsante di sistema non disegna nulla sotto.
    /// </summary>
    private sealed class TabButton : Button
    {
        private bool _hover;

        public int Index { get; }
        public bool Active { get; set; }

        public TabButton(string text, int index)
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            Text = text;
            Index = index;
            AutoSize = true;
            Height = 40;
            Padding = new Padding(16, 0, 16, 0);
            Margin = Padding.Empty;
            TextAlign = ContentAlignment.MiddleCenter;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Theme.Surface;
            ForeColor = Theme.MutedInk;
            Font = Theme.Fonts.Tab;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Surface);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var pill = new Rectangle(2, 6, Width - 5, Height - 13);
            if ((Active || _hover) && pill.Width > 8 && pill.Height > 8)
            {
                using var path = Theme.RoundedRectangle(pill, 8);
                using var fill = new SolidBrush(Active ? Theme.BrandSoft : Theme.Sunken);
                e.Graphics.FillPath(fill, path);
            }
            UiText.Draw(
                e.Graphics,
                Text,
                Font,
                new Rectangle(0, 0, Width, Height),
                Active ? Theme.BrandInk : Theme.MutedInk,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }
}

/// <summary>Cerchino numerato del flusso di lavoro (1 · 2).</summary>
internal sealed class StepBadge : Control
{
    private readonly string _caption;
    private readonly bool _active;

    public StepBadge(string caption, bool active)
    {
        _caption = caption;
        _active = active;
        Size = new Size(22, 22);
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var fill = new SolidBrush(_active ? Theme.Brand : Theme.BrandLite);
        e.Graphics.FillEllipse(fill, bounds);
        UiText.Draw(
            e.Graphics,
            _caption,
            Theme.Fonts.Step,
            bounds,
            _active ? Color.White : Theme.BrandInk,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>
/// Badge di stato nell'header: rettangolo arrotondato con testo centrato,
/// aggiornabile senza toccare BackColor e colori del testo a mano.
/// </summary>
internal sealed class StatusPill : Control
{
    private string _caption = string.Empty;
    private Color _fill = Theme.PillIdleFill;
    private Color _ink = Theme.PillIdleInk;

    public StatusPill()
    {
        Height = 34;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    public void SetStatus(string caption, Color fill, Color ink)
    {
        _caption = caption;
        _fill = fill;
        _ink = ink;
        // La larghezza segue il testo (anche con un DPI alto): la pillola non lo taglia.
        if (AutoSize)
        {
            PerformLayout();
        }
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = ModernPalette.Measure(_caption, Theme.Fonts.Pill).Width;
        return new Size(text + 30, Math.Max(26, Height));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        if (bounds.Width < 2 || bounds.Height < 2)
        {
            return;
        }
        using var path = Theme.RoundedRectangle(bounds, Math.Max(4, Height / 2 - 1));
        using (var fill = new SolidBrush(_fill))
        {
            e.Graphics.FillPath(fill, path);
        }
        var textBounds = new Rectangle(0, 0, Width, Height);
        UiText.Draw(
            e.Graphics,
            _caption,
            Theme.Fonts.Pill,
            textBounds,
            _ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>
/// Testo lungo scorrevole disegnato con la palette dell'app: le novità di una
/// Release possono essere lunghe e la barra di scorrimento nativa di Windows
/// resta grigia anche dentro una finestra dipinta a mano. Il testo viene
/// impaginato alla larghezza disponibile e si può scorrere con la rotellina,
/// trascinando il pollice o cliccando sulla pista; il pollice ha i colori del
/// tema. Il testo resta esposto all'accessibilità tramite <see cref="Control.Text"/>.
/// </summary>
internal sealed class NoteTextView : Control
{
    private const int TextPadding = 4;
    private static readonly Font HeadingFont = new("Segoe UI", 10.5f, FontStyle.Bold);
    private IReadOnlyList<NoteBlock> _blocks = Array.Empty<NoteBlock>();
    private string _text = string.Empty;
    private ScrollBarLayout _layout;
    private int _requestedOffset;
    private bool _dragging;
    private bool _pointerOnThumb;
    private int _grabOffset;

    public NoteTextView()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.FieldFill;
        ForeColor = Theme.InkSecondary;
        Font = Theme.Fonts.Ui;
        TabStop = false;
        AccessibleRole = AccessibleRole.StaticText;
    }

    /// <summary>Testo da mostrare.</summary>
    public string Value
    {
        get => _text;
        set
        {
            _blocks = ReleaseNotesDocument.Parse(value);
            // Con note senza una struttura riconoscibile si mostra il testo com'è.
            if (_blocks.Count == 0 && !string.IsNullOrWhiteSpace(value))
            {
                _blocks = new[] { new NoteBlock(NoteBlockKind.Paragraph, value.Trim()) };
            }
            _text = ReleaseNotesDocument.ToPlainText(_blocks);
            Text = _text;
            _requestedOffset = 0;
            Recalculate();
        }
    }

    /// <summary>Altezza che serve al testo impaginato alla larghezza attuale.</summary>
    public int TextHeight { get; private set; }

    /// <summary>Il testo non entra tutto: c'è la barra da disegnare.</summary>
    public bool NeedsScroll => _layout.NeedsScroll;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Recalculate();
    }

    /// <summary>
    /// Ricalcola l'impaginazione: prima lasciando la fascia alla barra, poi (se
    /// il testo entrava) con tutta la larghezza. Il conto si ferma qui, così la
    /// barra non compare e sparisce una volta per ogni ridimensionamento.
    /// </summary>
    public void Recalculate()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }
        var height = MeasureHeight(ClientSize.Width - ScrollBarLayout.TrackWidth);
        _layout = ScrollBarLayout.Compute(ClientSize.Width, ClientSize.Height, height, _requestedOffset);
        if (!_layout.NeedsScroll)
        {
            height = MeasureHeight(ClientSize.Width);
            _layout = ScrollBarLayout.Compute(ClientSize.Width, ClientSize.Height, height, _requestedOffset);
        }
        _requestedOffset = _layout.Offset;
        TextHeight = height;
        Invalidate();
    }

    /// <summary>Porta il testo alla posizione indicata (viene riportata dentro i limiti).</summary>
    public void ScrollTo(int offset)
    {
        _requestedOffset = offset;
        _layout = ScrollBarLayout.Compute(ClientSize.Width, ClientSize.Height, TextHeight, offset);
        _requestedOffset = _layout.Offset;
        Invalidate();
    }

    private float DpiScale => DeviceDpi / 96f;

    private int Px(int logical) => (int)Math.Round(logical * DpiScale);

    private const TextFormatFlags BodyFlags =
        TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.Left | TextFormatFlags.Top;

    /// <summary>
    /// Posizione di ogni blocco per la larghezza data e altezza totale: stessa
    /// funzione per misurare e per disegnare, così le due cose non si separano.
    /// </summary>
    private List<(NoteBlock Block, Rectangle Bounds)> Arrange(int width, out int height)
    {
        var placed = new List<(NoteBlock, Rectangle)>(_blocks.Count);
        var y = Px(TextPadding);
        var left = Px(TextPadding + 2);
        var right = Math.Max(left + 20, width - Px(TextPadding + 2));
        var indent = Px(18);
        for (var index = 0; index < _blocks.Count; index++)
        {
            var block = _blocks[index];
            var isHeading = block.Kind == NoteBlockKind.Heading;
            var x = block.Kind == NoteBlockKind.Bullet ? left + indent : left;
            var font = isHeading ? HeadingFont : Font;
            if (isHeading && index > 0)
            {
                y += Px(14);
            }
            var measured = TextRenderer.MeasureText(
                block.Text,
                font,
                new Size(Math.Max(1, right - x), int.MaxValue),
                BodyFlags).Height;
            placed.Add((block, new Rectangle(x, y, Math.Max(1, right - x), measured)));
            y += measured + Px(isHeading ? 6 : 5);
        }
        height = _blocks.Count == 0 ? 0 : y + Px(TextPadding);
        return placed;
    }

    private int MeasureHeight(int width)
    {
        Arrange(width, out var height);
        return height;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using (var background = new SolidBrush(BackColor))
        {
            e.Graphics.FillRectangle(background, ClientRectangle);
        }
        if (_blocks.Count > 0)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var (block, bounds) in Arrange(_layout.ContentWidth, out _))
            {
                var area = new Rectangle(bounds.X, bounds.Y - _layout.Offset, bounds.Width, bounds.Height);
                if (area.Bottom < 0 || area.Top > ClientSize.Height)
                {
                    continue;
                }
                switch (block.Kind)
                {
                    case NoteBlockKind.Heading:
                        // Barretta d'accento a sinistra del titolo di sezione.
                        using (var accent = new SolidBrush(Theme.Brand))
                        {
                            e.Graphics.FillRectangle(accent, area.X - Px(7), area.Y + Px(2), Px(3), Math.Max(4, area.Height - Px(4)));
                        }
                        UiText.Draw(e.Graphics, block.Text, HeadingFont, area, Theme.BrandInk, BodyFlags);
                        break;
                    case NoteBlockKind.Bullet:
                        var dot = Px(6);
                        using (var dotBrush = new SolidBrush(Theme.Brand))
                        {
                            e.Graphics.FillEllipse(
                                dotBrush,
                                area.X - Px(14),
                                area.Y + ((Font.Height - dot) / 2) + 1,
                                dot,
                                dot);
                        }
                        UiText.Draw(e.Graphics, block.Text, Font, area, Theme.Ink, BodyFlags);
                        break;
                    default:
                        UiText.Draw(e.Graphics, block.Text, Font, area, Theme.InkSecondary, BodyFlags);
                        break;
                }
            }
        }
        DrawThumb(e.Graphics);
    }

    private void DrawThumb(Graphics graphics)
    {
        if (!_layout.NeedsScroll)
        {
            return;
        }
        // ThumbLeft è relativo alla pista: la pista sta a destra, dopo il testo.
        var bounds = new Rectangle(
            _layout.ContentWidth + _layout.ThumbLeft,
            _layout.ThumbTop + 1,
            ScrollBarLayout.ThumbWidth,
            Math.Max(4, _layout.ThumbHeight - 2));
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundedRectangle(bounds, ScrollBarLayout.ThumbWidth / 2);
        using var brush = new SolidBrush(_dragging || _pointerOnThumb ? Theme.MutedInk : Theme.DividerStrong);
        graphics.FillPath(brush, path);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (_layout.NeedsScroll)
        {
            ScrollTo(_layout.OffsetAfterWheel(e.Delta));
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_layout.HitsTrack(e.X, ClientSize.Width))
        {
            return;
        }
        if (_layout.HitsThumb(e.Y))
        {
            _dragging = true;
            _grabOffset = e.Y - _layout.ThumbTop;
            Capture = true;
        }
        else
        {
            ScrollTo(_layout.OffsetAfterTrackClick(e.Y));
        }
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging)
        {
            ScrollTo(_layout.OffsetForThumbTop(e.Y - _grabOffset));
            return;
        }
        var onThumb = _layout.HitsTrack(e.X, ClientSize.Width) && _layout.HitsThumb(e.Y);
        if (onThumb == _pointerOnThumb)
        {
            return;
        }
        _pointerOnThumb = onThumb;
        Cursor = onThumb ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        Capture = false;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_pointerOnThumb)
        {
            return;
        }
        _pointerOnThumb = false;
        Cursor = Cursors.Default;
        Invalidate();
    }
}
