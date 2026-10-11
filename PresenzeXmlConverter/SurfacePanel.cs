namespace PresenzeXmlConverter;

using System.Drawing.Drawing2D;

/// <summary>
/// Card arrotondata: il pannello base di tutte le sezioni della finestra.
/// Riempimento, bordo e raggio sono configurabili perché header, card chiare
/// e fasce colorate condividano esattamente lo stesso raggio d'angolo e la
/// stessa palette, invece di ritoccare i valori a mano in ogni punto.
/// </summary>
internal sealed class SurfacePanel : Panel
{
    private int _radius = 12;
    private Color _fill = Theme.Surface;
    private Color _border = Theme.Divider;

    public SurfacePanel()
    {
        BackColor = Theme.Surface;
        DoubleBuffered = true;
        ResizeRedraw = true;
        UpdateRoundedRegion();
    }

    /// <summary>Raggio d'angolo in pixel.</summary>
    public int Radius
    {
        get => _radius;
        set
        {
            _radius = value;
            UpdateRoundedRegion();
            Invalidate();
        }
    }

    /// <summary>Colore di riempimento (anche il BackColor, per i figli).</summary>
    public Color SurfaceFill
    {
        get => _fill;
        set
        {
            _fill = value;
            BackColor = value;
        }
    }

    /// <summary>Colore del bordo; per nasconderlo usa lo stesso tono del fill.</summary>
    public Color SurfaceBorder
    {
        get => _border;
        set
        {
            _border = value;
            Invalidate();
        }
    }

    protected override void OnResize(EventArgs eventArgs)
    {
        base.OnResize(eventArgs);
        UpdateRoundedRegion();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        if (ClientSize.Width < 2 || ClientSize.Height < 2)
        {
            return;
        }

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundedRectangle(
            new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1),
            _radius);
        using var pen = new Pen(_border);
        eventArgs.Graphics.DrawPath(pen, path);
    }

    private void UpdateRoundedRegion()
    {
        if (ClientSize.Width < 2 || ClientSize.Height < 2)
        {
            return;
        }
        using var path = Theme.RoundedRectangle(
            new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
            _radius);
        Region = new Region(path);
    }
}
