namespace PresenzeXmlConverter.Tests;

using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;

/// <summary>Sonda temporanea di verifica grafica (non è un test di regressione).</summary>
public class UiLayoutProbe
{
    [Fact]
    public void DumpLayoutReport()
    {
        var baseDir = AppContext.BaseDirectory;
        var reportPath = Path.Combine(baseDir, "ui-report.txt");
        var imagePath = Path.Combine(baseDir, "ui-render.png");
        var report = new StringBuilder();

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = CreateMainForm();
                form.Show();
                Application.DoEvents();
                DumpTree(form, report);
                Render(form, imagePath, report);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        File.WriteAllText(reportPath, report.ToString());
        Assert.Null(failure);
    }

    private static Form CreateMainForm()
    {
        var constructor = typeof(MainForm).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        Assert.NotNull(constructor);
        var form = (Form)constructor!.Invoke(null)!;
        // Fuori schermo: la finestra non deve apparire sul desktop dell'utente.
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-4000, -4000);
        return form;
    }

    private static void DumpTree(Control root, StringBuilder report)
    {
        Walk(root, 0, root.PointToScreen(Point.Empty), report);
    }

    private static void Walk(Control control, int depth, Point origin, StringBuilder report)
    {
        var bounds = control.Bounds;
        var fits = true;
        var note = string.Empty;
        if (control is Label label && !label.AutoSize && label.Text.Length > 0)
        {
            var measured = TextRenderer.MeasureText(
                label.Text,
                label.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            if (measured.Width > label.ClientSize.Width + 1 && !label.AutoEllipsis)
            {
                fits = false;
                note = $"TAGLIO ORIZZONTALE: testo {measured.Width}px in {label.ClientSize.Width}px";
            }
            if (measured.Height > label.ClientSize.Height + 1)
            {
                fits = false;
                note = $"TAGLIO VERTICALE: testo {measured.Height}px in {label.ClientSize.Height}px";
            }
        }

        report.AppendLine(
            $"{new string(' ', depth * 2)}{control.GetType().Name,-16} " +
            $"[{bounds.X,5},{bounds.Y,5} {bounds.Width,4}x{bounds.Height,4}] " +
            $"dock={control.Dock,-7} vis={control.Visible,-5} " +
            $"font={control.Font.SizeInPoints:0.#}/{(control.Font.Bold ? "B" : "-")}{(fits ? "" : "  <<< " + note)} " +
            $"{Truncate(control.Text, 42)}");

        foreach (Control child in control.Controls)
        {
            Walk(child, depth + 1, origin, report);
        }
    }

    private static void Render(Form form, string imagePath, StringBuilder report)
    {
        using var bitmap = new Bitmap(form.Width, form.Height, PixelFormat.Format24bppRgb);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(imagePath, ImageFormat.Png);

        var counts = new Dictionary<Color, int>();
        var total = bitmap.Width * bitmap.Height;
        for (var y = 0; y < bitmap.Height; y += 2)
        {
            for (var x = 0; x < bitmap.Width; x += 2)
            {
                var pixel = bitmap.GetPixel(x, y);
                counts[pixel] = counts.TryGetValue(pixel, out var seen) ? seen + 1 : 1;
            }
        }
        var sampled = total / 4;
        report.AppendLine();
        report.AppendLine($"--- Colori prevalenti del rendering ({bitmap.Width}x{bitmap.Height}) ---");
        foreach (var pair in counts.OrderByDescending(pair => pair.Value).Take(14))
        {
            var percent = pair.Value * 100.0 / sampled;
            report.AppendLine($"  {percent,6:0.00}%  RGB({pair.Value > 0 ? pair.Key.R : 0},{pair.Key.G},{pair.Key.B})");
        }

        var systemGray = counts
            .Where(pair => IsSystemGray(pair.Key))
            .Sum(pair => pair.Value);
        report.AppendLine($"  Grigio di sistema (non temizzato): {systemGray * 100.0 / sampled:0.00}%");
    }

    private static bool IsSystemGray(Color color) =>
        color.R is 240 or 241 or 243 or 245 && color.G == color.R && color.B == color.R;

    private static string Truncate(string text, int maxLength)
    {
        var single = text.Replace("\r", " ").Replace("\n", " ");
        return single.Length <= maxLength ? single : single[..maxLength] + "…";
    }
}
