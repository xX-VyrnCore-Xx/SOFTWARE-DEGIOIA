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
    private int _radius = 14;
    private Color _fill = Theme.Surface;
    private Color _border = Theme.Divider;

    public SurfacePanel()
    {
        BackColor = Theme.Surface;
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    /// <summary>Raggio d'angolo in pixel.</summary>
    public int Radius
    {
        get => _radius;
        set
        {
            _radius = value;
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

    /// <summary>
    /// Gli angoli non si ritagliano con una regione (il bordo di una regione è a scalini, senza
    /// antialiasing): lo sfondo prende il colore di ciò che sta dietro e la card si dipinge sopra,
    /// con gli angoli lisci.
    /// </summary>
    // Il raggio segue la scala dello schermo come tutto il resto.
    private int R => this.Px(_radius);

    protected override void OnPaintBackground(PaintEventArgs eventArgs)
    {
        using var behind = new SolidBrush(Parent?.BackColor ?? Theme.Canvas);
        eventArgs.Graphics.FillRectangle(behind, ClientRectangle);
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
            R);
        if (!(Theme.IsGlass && _fill.ToArgb() == Theme.Surface.ToArgb()))
        {
            using var body = new SolidBrush(_fill);
            eventArgs.Graphics.FillPath(body, path);
        }
        if (Theme.IsGlass && _fill.ToArgb() == Theme.Surface.ToArgb())
        {
            PaintGlass(eventArgs.Graphics, path);
            return;
        }
        using var pen = new Pen(_border);
        eventArgs.Graphics.DrawPath(pen, path);
        // Sulle card di superficie, un rilievo morbido sul bordo inferiore (tre strati sempre più tenui): l'effetto
        // di un'ombra vera, che resta dentro i bordi del pannello. Nel tema scuro, invece, un filo di luce in alto.
        if (_fill.ToArgb() == Theme.Surface.ToArgb() && R >= 8 && ClientSize.Width > R * 2 + 4)
        {
            var left = R;
            var right = ClientSize.Width - R - 1;
            if (Theme.IsDark)
            {
                using var light = new Pen(Color.FromArgb(16, 255, 255, 255), 1f);
                eventArgs.Graphics.DrawLine(light, left, 1.5f, right, 1.5f);
            }
        }
    }

    /// <summary>
    /// Tema «Vetro»: sfumatura leggera dall'alto in basso, bordo che si schiarisce verso l'angolo in alto a sinistra
    /// come un vetro colpito dalla luce, e un filo luminoso appena sotto il bordo alto.
    /// </summary>
    private void PaintGlass(Graphics graphics, GraphicsPath path)
    {
        var area = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        // Interno uniforme: i controlli dentro la card hanno lo stesso fondo e non si vede nessun riquadro più scuro.
        using (var fill = new SolidBrush(Theme.Surface))
        {
            graphics.FillPath(fill, path);
        }
        // Un velo di luce sul bordo alto, dentro la card, come il riflesso sul vetro.
        var oldClip = graphics.Clip;
        using (var clip = new Region(path))
        {
            graphics.SetClip(clip, CombineMode.Replace);
            using var sheen = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, ClientSize.Width), 18),
                Color.FromArgb(26, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                90f);
            graphics.FillRectangle(sheen, 0, 0, ClientSize.Width, 18);
        }
        graphics.Clip = oldClip;
        using (var edge = new LinearGradientBrush(area, Theme.GlassEdge, Theme.GlassEdgeDim, 40f))
        using (var pen = new Pen(edge, 1f))
        {
            graphics.DrawPath(pen, path);
        }
        if (ClientSize.Width > (R * 2) + 4)
        {
            using var highlight = new Pen(Color.FromArgb(46, 255, 255, 255), 1f);
            graphics.DrawLine(highlight, R, 1.5f, ClientSize.Width - R - 1, 1.5f);
        }
    }
}
