using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace PresenzeXmlConverter.Wpf;

/// <summary>
/// L'icona del tray con un pallino colorato in basso a destra (aggiornamento pronto): si vede a colpo d'occhio anche a
/// finestra chiusa, senza aprire il menu. Disegnata alla misura dell'icona piccola dello schermo, con un anello chiaro
/// attorno al pallino perché risalti su qualunque sfondo della barra.
/// </summary>
internal static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    /// <summary>Misura in pixel dell'icona piccola alla scala indicata (16 a 96 dpi).</summary>
    public static int SideFor(int dpi) => Math.Clamp((int)Math.Round(16 * Math.Max(96, dpi) / 96.0), 16, 64);

    public static Icon WithBadge(Icon source, Color badge, int dpi)
    {
        var side = SideFor(dpi);
        using var sized = new Icon(source, new Size(side, side));
        using var bitmap = new Bitmap(side, side, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawIcon(sized, new Rectangle(0, 0, side, side));
            var diameter = (float)Math.Round(side * 0.5);
            var ring = Math.Max(1.5f, side / 10f);
            var area = new RectangleF(side - diameter - 0.5f, side - diameter - 0.5f, diameter, diameter);
            using var halo = new SolidBrush(Color.White);
            using var fill = new SolidBrush(badge);
            graphics.FillEllipse(halo, RectangleF.Inflate(area, ring, ring));
            graphics.FillEllipse(fill, area);
        }
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
