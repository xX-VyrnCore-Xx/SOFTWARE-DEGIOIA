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

        _input.Dock = DockStyle.Fill;
        _input.BorderStyle = BorderStyle.None;
        _input.Font = Theme.Fonts.Ui;
        _input.BackColor = Theme.FieldFill;
        _input.ForeColor = Theme.Ink;
        _input.Margin = Padding.Empty;
        _input.TextChanged += (_, _) => UpdatePlaceholder();
        _input.GotFocus += (_, _) => Invalidate();
        _input.LostFocus += (_, _) => Invalidate();
        Controls.Add(_input);

        _placeholder.Dock = DockStyle.Fill;
        _placeholder.TextAlign = ContentAlignment.MiddleLeft;
        _placeholder.ForeColor = Theme.FaintInk;
        _placeholder.Font = _input.Font;
        _placeholder.BackColor = Theme.FieldFill;
        _placeholder.Padding = Padding;
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
