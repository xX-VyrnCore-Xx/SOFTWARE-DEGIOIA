using System.Drawing.Drawing2D;

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
        SelectionMode = DataGridViewSelectionMode.CellSelect;
        MultiSelect = false;
        EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        EnableHeadersVisualStyles = false;
        ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
        ColumnHeadersDefaultCellStyle.BackColor = Theme.Sunken;
        ColumnHeadersDefaultCellStyle.ForeColor = Theme.InkSecondary;
        ColumnHeadersDefaultCellStyle.Font = Theme.Fonts.GridHeader;
        ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 4, 0);
        ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Sunken;
        ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.InkSecondary;
        ColumnHeadersHeight = 42;
        RowTemplate.Height = 38;
        DefaultCellStyle.Font = Theme.Fonts.Grid;
        DefaultCellStyle.ForeColor = Theme.Ink;
        DefaultCellStyle.BackColor = Theme.Surface;
        DefaultCellStyle.SelectionBackColor = Theme.BrandLite;
        DefaultCellStyle.SelectionForeColor = Theme.Ink;
        DefaultCellStyle.Padding = new Padding(9, 0, 5, 0);
        AlternatingRowsDefaultCellStyle.BackColor = Theme.AltRow;
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
    private readonly TextBox _input = new();
    private readonly Label _placeholder = new();

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
        _input.TextChanged += (_, _) => UpdatePlaceholder();
        _input.GotFocus += (_, _) => Invalidate();
        _input.LostFocus += (_, _) => Invalidate();
        Controls.Add(_input);

        _placeholder.TextAlign = ContentAlignment.MiddleLeft;
        _placeholder.ForeColor = Theme.FaintInk;
        _placeholder.Font = _input.Font;
        _placeholder.BackColor = Theme.FieldFill;
        _placeholder.AutoEllipsis = true;
        // Un clic sul testo guida deve mettere il cursore nel campo.
        _placeholder.MouseDown += (_, _) => _input.Focus();
        Controls.Add(_placeholder);

        UpdatePlaceholder();
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
        get => _placeholder.Text;
        set => _placeholder.Text = value;
    }

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
        set
        {
            _input.Font = value;
            _placeholder.Font = value;
        }
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
        // allineando a mano testo e placeholder sulla stessa riga non c'è lo
        // scarto di qualche pixel quando il campo passa da vuoto a compilato.
        var innerHeight = _input.PreferredHeight;
        var top = Math.Max(0, (ClientSize.Height - innerHeight) / 2);
        var width = Math.Max(0, ClientSize.Width - Padding.Horizontal);
        _input.SetBounds(Padding.Left, top, width, innerHeight);
        _placeholder.SetBounds(Padding.Left, top, width, innerHeight);
    }

    private void UpdatePlaceholder()
    {
        _placeholder.Visible = _input.Text.Length == 0 && !_input.Focused;
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
        using (var fill = new SolidBrush(Theme.FieldFill))
        {
            e.Graphics.FillPath(fill, path);
        }
        // Il bordo si accende quando il campo ha il focus: l'utente vede
        // subito dove finirà ciò che sta scrivendo.
        using var pen = new Pen(_input.Focused ? Theme.Brand : Theme.DividerStrong, 1f);
        e.Graphics.DrawPath(pen, path);
    }
}

/// <summary>
/// Striscia decorativa con gradiente orizzontale: usata come accento in cima
/// all'header per dare profondità senza ricorrere a immagini esterne.
/// </summary>
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

    public void AddPage(string title, Control page)
    {
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        _content.Controls.Add(page);
        _pages.Add(page);

        var tab = new TabButton(title, _tabs.Count);
        tab.Click += (_, _) => SelectTab(tab.Index);
        _tabs.Add(tab);
        _flow.Controls.Add(tab);
    }

    public void SetPageText(int index, string text)
    {
        if (index >= 0 && index < _tabs.Count)
        {
            _tabs[index].Text = text;
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

    /// <summary>Voce di barra: testo centrato verticalmente e sottolineatura attiva.</summary>
    private sealed class TabButton : Button
    {
        public int Index { get; }
        public bool Active { get; set; }

        public TabButton(string text, int index)
        {
            Text = text;
            Index = index;
            AutoSize = true;
            Height = 39;
            Padding = new Padding(16, 0, 16, 0);
            Margin = Padding.Empty;
            TextAlign = ContentAlignment.MiddleCenter;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Theme.Sunken;
            FlatAppearance.MouseDownBackColor = Theme.BrandLite;
            BackColor = Theme.Surface;
            ForeColor = Theme.MutedInk;
            Font = Theme.Fonts.Tab;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Active)
            {
                using var underline = new SolidBrush(Theme.Brand);
                e.Graphics.FillRectangle(underline, 12, Height - 3, Width - 24, 3);
            }
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
        TextRenderer.DrawText(
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
        Invalidate();
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
        TextRenderer.DrawText(
            e.Graphics,
            _caption,
            Theme.Fonts.Pill,
            textBounds,
            _ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
