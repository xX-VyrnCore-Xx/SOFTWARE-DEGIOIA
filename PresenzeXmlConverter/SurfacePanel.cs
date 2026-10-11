namespace PresenzeXmlConverter;

using System.Drawing.Drawing2D;

internal sealed class SurfacePanel : Panel
{
    private const int Radius = 12;

    public SurfacePanel()
    {
        BackColor = Color.White;
        DoubleBuffered = true;
        ResizeRedraw = true;
        UpdateRoundedRegion();
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
        using var path = CreateRoundedRectangle(
            new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1),
            Radius);
        using var pen = new Pen(Color.FromArgb(224, 231, 239));
        eventArgs.Graphics.DrawPath(pen, path);
    }

    private void UpdateRoundedRegion()
    {
        if (ClientSize.Width < 2 || ClientSize.Height < 2)
        {
            return;
        }
        using var path = CreateRoundedRectangle(
            new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
            Radius);
        Region = new Region(path);
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
