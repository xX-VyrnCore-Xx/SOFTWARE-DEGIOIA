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
                form.PerformLayout();
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

    [Fact]
    public void DumpDialogsReport()
    {
        var reportPath = Path.Combine(AppContext.BaseDirectory, "ui-dialogs-report.txt");
        var report = new StringBuilder();
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                Render(new CloseBehaviorDialog(), "close-dialog.png", report, "CloseBehaviorDialog");
                Render(
                    new ColumnMappingDialog(
                        new[] { "Nominativo", "Tipo assenza", "Dal", "Al", "Ore" },
                        suggested: null),
                    "mapping-dialog.png",
                    report,
                    "ColumnMappingDialog");
                Render(
                    new MappingEditorDialog(
                        new Dictionary<string, string> { ["Rossi Mario"] = "0012", ["Bianchi Anna"] = "" },
                        new Dictionary<string, string> { ["Ferie"] = "F01", ["Malattia"] = "" },
                        new[] { "Bianchi Anna" },
                        new[] { "Malattia" },
                        new Dictionary<string, string> { ["Malattia"] = "M01" }),
                    "codes-dialog.png",
                    report,
                    "MappingEditorDialog");
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

    private static void Render(Form form, string imageName, StringBuilder report, string title)
    {
        using (form)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-4000, -4000);
            form.Show();
            Application.DoEvents();
            form.PerformLayout();
            Application.DoEvents();

            report.AppendLine($"========== {title} ({form.Width}x{form.Height}) ==========");
            DumpTree(form, report);

            using var bitmap = new Bitmap(form.Width, form.Height, PixelFormat.Format24bppRgb);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, imageName), ImageFormat.Png);

            var total = 0;
            var gray = 0;
            for (var y = 0; y < bitmap.Height; y += 2)
            {
                for (var x = 0; x < bitmap.Width; x += 2)
                {
                    total++;
                    if (IsSystemGray(bitmap.GetPixel(x, y)))
                    {
                        gray++;
                    }
                }
            }
            report.AppendLine($"  Grigio di sistema: {gray * 100.0 / total:0.00}%");
            report.AppendLine();
        }
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
            // Il Label a riga singola spezza comunque il testo: si misura con
            // l'andare a capo per evitare falsi allarmi su etichette larghe.
            var measured = TextRenderer.MeasureText(
                label.Text,
                label.Font,
                new Size(Math.Max(1, label.ClientSize.Width), int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
            if (measured.Height > label.ClientSize.Height + 2)
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
            $"back={control.BackColor.ToArgb() & 0xFFFFFF:X6} " +
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
            report.AppendLine($"  {percent,6:0.00}%  RGB({pair.Key.R},{pair.Key.G},{pair.Key.B})");
        }

        report.AppendLine("--- Scansione orizzontale y=290 (ogni 32px) ---");
        for (var x = 280; x < bitmap.Width; x += 32)
        {
            var color = bitmap.GetPixel(x, 290);
            report.AppendLine($"  x={x,5}  RGB({color.R},{color.G},{color.B})");
        }
        report.AppendLine("--- Scansione verticale x=1000 (ogni 8px) ---");
        for (var y = 240; y < 360; y += 8)
        {
            var color = bitmap.GetPixel(1000, y);
            report.AppendLine($"  y={y,5}  RGB({color.R},{color.G},{color.B})");
        }

        var grayPixels = new List<Point>();
        for (var y = 0; y < bitmap.Height; y += 2)
        {
            for (var x = 0; x < bitmap.Width; x += 2)
            {
                if (IsSystemGray(bitmap.GetPixel(x, y)))
                {
                    grayPixels.Add(new Point(x, y));
                }
            }
        }
        if (grayPixels.Count > 0)
        {
            report.AppendLine($"  Grigio di sistema: {grayPixels.Count * 4 * 100.0 / sampled:0.00}%");
            const int block = 64;
            report.AppendLine("  Mappa dei blocchi con grigio (colonne da 64px, righe da 64px):");
            for (var by = 0; by < bitmap.Height; by += block)
            {
                var line = new StringBuilder($"    y={by,4}  ");
                for (var bx = 0; bx < bitmap.Width; bx += block)
                {
                    var inBlock = grayPixels.Count(point =>
                        point.X >= bx && point.X < bx + block && point.Y >= by && point.Y < by + block);
                    var blockPixels = (block / 2) * (block / 2);
                    var ratio = inBlock * 100.0 / blockPixels;
                    line.Append(ratio switch
                    {
                        > 90 => '#',
                        > 60 => '+',
                        > 30 => '=',
                        > 5 => '.',
                        _ => ' '
                    });
                }
                report.AppendLine(line.ToString());
            }
        }
        else
        {
            report.AppendLine("  Grigio di sistema: assente");
        }
    }

    private static bool IsSystemGray(Color color) =>
        color.R is 240 or 241 or 243 or 245 && color.G == color.R && color.B == color.R;

    private static string Truncate(string text, int maxLength)
    {
        var single = text.Replace("\r", " ").Replace("\n", " ");
        return single.Length <= maxLength ? single : single[..maxLength] + "…";
    }
}
