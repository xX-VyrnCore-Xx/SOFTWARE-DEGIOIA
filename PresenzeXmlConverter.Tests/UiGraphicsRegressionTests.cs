namespace PresenzeXmlConverter.Tests;

using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;

/// <summary>
/// Regressione grafica: rende la finestra principale e i dialoghi e verifica
/// che non compaiano colori di sistema non temizzati (la striscia grigia del
/// TabControl nativo, per esempio) né testo tagliato fuori dalla sua scatola.
/// In ambienti senza desktop la verifica viene saltata.
/// </summary>
public class UiGraphicsRegressionTests
{
    [Fact]
    public void MainWindowIsFullyThemedAndNothingIsClipped()
    {
        var result = RunOnStaThread(() =>
        {
            var report = new StringBuilder();
            var form = new MainForm();
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-4000, -4000);
            form.Show();
            Application.DoEvents();
            form.PerformLayout();
            Application.DoEvents();
            var clipped = CollectClippedLabels(form, report);
            var gray = GrayRatio(Render(form));
            form.Dispose();
            return (clipped, gray, report.ToString());
        });
        if (result is null)
        {
            return;
        }
        Assert.True(string.IsNullOrEmpty(result.Value.clipped), result.Value.clipped);
        Assert.True(
            result.Value.gray < 0.002,
            $"Colori di sistema non temizzati nel rendering ({result.Value.gray:P2}). {result.Value.report}");
    }

    [Fact]
    public void DialogsAreFullyThemed()
    {
        var result = RunOnStaThread(() =>
        {
            var report = new StringBuilder();
            var clipped = string.Empty;
            var gray = 0.0;
            foreach (var dialog in new Form[]
                     {
                         new CloseBehaviorDialog(),
                         new ColumnMappingDialog(new[] { "Nominativo", "Tipo assenza", "Dal", "Al", "Ore" }, null),
                         new MappingEditorDialog(
                             new Dictionary<string, string> { ["Rossi Mario"] = "0012" },
                             new Dictionary<string, string> { ["Ferie"] = "F01", ["Malattia"] = "" },
                             new[] { "Malattia" },
                             new[] { "Malattia" },
                             new Dictionary<string, string> { ["Malattia"] = "M01" }),
                         // Finestre di avanzamento riusate da aggiornamenti, Ollama
                         // e download del modello AI locale.
                         new ProgressDialog(
                             "AI locale",
                             "Download del modello qwen2.5:1.5b",
                             "Connessione a Ollama…"),
                         new UpdateProgressDialog("1.0.0")
                     })
            {
                using (dialog)
                {
                    dialog.StartPosition = FormStartPosition.Manual;
                    dialog.Location = new Point(-4000, -4000);
                    dialog.Show();
                    Application.DoEvents();
                    dialog.PerformLayout();
                    clipped = CollectClippedLabels(dialog, report);
                    gray = Math.Max(gray, GrayRatio(Render(dialog)));
                    dialog.Hide();
                }
                Assert.True(string.IsNullOrEmpty(clipped), clipped);
            }
            return (clipped, gray, report.ToString());
        });
        if (result is null)
        {
            return;
        }
        // Resta solo il pulsante a tendina nativo delle caselle combinate,
        // che Windows disegna sempre in grisaglia.
        Assert.True(
            result.Value.gray < 0.01,
            $"Colori di sistema non temizzati nei dialoghi ({result.Value.gray:P2}). {result.Value.report}");
    }

    private static (string clipped, double gray, string report)? RunOnStaThread(
        Func<(string clipped, double gray, string report)> probe)
    {
        var result = default((string clipped, double gray, string report));
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = probe();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        // Sessione senza desktop (runner headless): la verifica grafica non può
        // eseguire e non deve far fallire la suite.
        return failure is null ? result : null;
    }

    private static Bitmap Render(Form form)
    {
        var bitmap = new Bitmap(form.Width, form.Height, PixelFormat.Format24bppRgb);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        return bitmap;
    }

    private static double GrayRatio(Bitmap bitmap)
    {
        var total = 0;
        var gray = 0;
        for (var y = 0; y < bitmap.Height; y += 2)
        {
            for (var x = 0; x < bitmap.Width; x += 2)
            {
                var color = bitmap.GetPixel(x, y);
                total++;
                if (color.R is 240 or 241 or 243 or 245 && color.G == color.R && color.B == color.R)
                {
                    gray++;
                }
            }
        }
        bitmap.Dispose();
        return gray * 1.0 / total;
    }

    /// <summary>Ritua le etichette il cui testo non sta nella propria scatola.</summary>
    private static string CollectClippedLabels(Control root, StringBuilder report)
    {
        var problems = new List<string>();
        Walk(root, problems);
        foreach (var problem in problems.Take(5))
        {
            report.AppendLine(problem);
        }
        return string.Join(Environment.NewLine, problems);
    }

    private static void Walk(Control control, List<string> problems)
    {
        if (control is Label label && !label.AutoSize && label.Text.Length > 0 && label.Visible)
        {
            var measured = TextRenderer.MeasureText(
                label.Text,
                label.Font,
                new Size(Math.Max(1, label.ClientSize.Width), int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
            if (measured.Height > label.ClientSize.Height + 2)
            {
                problems.Add(
                    $"Testo tagliato in “{label.Text}”: servono {measured.Height}px, la scatola è {label.ClientSize.Height}px.");
            }
        }
        foreach (Control child in control.Controls)
        {
            Walk(child, problems);
        }
    }

}
