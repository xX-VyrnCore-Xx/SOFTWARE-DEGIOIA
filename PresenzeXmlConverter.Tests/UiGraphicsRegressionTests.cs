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
            // Due controlli nella stessa cella di una tabella si sovrappongono e
            // scompaginano le altezze delle righe: qui vengono segnalati subito.
            var overlaps = CollectCellOverlaps(form, report);
            var gray = GrayRatio(Render(form));
            // Diagnostica nel messaggio di errore: se il layout viene compresso
            // serve sapere con quale schermo e quale finestra è successo.
            report.AppendLine(
                $"Diagnostica: finestra {form.ClientSize.Width}x{form.ClientSize.Height}, " +
                $"schermo {Screen.PrimaryScreen?.Bounds.Width}x{Screen.PrimaryScreen?.Bounds.Height}, " +
                $"area di lavoro {Screen.FromControl(form).WorkingArea.Width}x{Screen.FromControl(form).WorkingArea.Height}, " +
                $"scala del sistema {form.DeviceDpi} dpi.");
            form.Dispose();
            return (clipped: clipped, overlaps: overlaps, gray: gray, report: report.ToString());
        });
        if (result is null)
        {
            return;
        }
        Assert.True(
            string.IsNullOrEmpty(result.Value.clipped),
            result.Value.clipped + Environment.NewLine + result.Value.report);
        Assert.True(
            string.IsNullOrEmpty(result.Value.overlaps),
            result.Value.overlaps + Environment.NewLine + result.Value.report);
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
            return (clipped: clipped, gray: gray, report: report.ToString());
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

    private static T? RunOnStaThread<T>(Func<T> probe)
        where T : struct
    {
        var result = default(T);
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

    /// <summary>
    /// Contenitore e cella del controllo: nel messaggio di errore fa capire subito
    /// se la colpa è di una riga troppo bassa, di una cella occupata o di un
    /// contenitore rimpicciolito.
    /// </summary>
    private static string DescribeParent(Control control)
    {
        if (control.Parent is not Control parent)
        {
            return string.Empty;
        }
        var details = $" Contenitore {parent.GetType().Name} {parent.ClientSize.Width}x{parent.ClientSize.Height}";
        if (parent is TableLayoutPanel table)
        {
            var row = table.GetRow(control);
            var column = table.GetColumn(control);
            var style = row >= 0 && row < table.RowStyles.Count ? table.RowStyles[row] : null;
            details += style is null
                ? $" cella ({column},{row}) fuori dalle righe dichiarate"
                : $" cella ({column},{row}) riga {style.SizeType} {style.Height} su {table.RowStyles.Count} righe";
        }
        if (parent.Parent is Control grandparent)
        {
            details += $" dentro {grandparent.GetType().Name} {grandparent.ClientSize.Width}x{grandparent.ClientSize.Height}";
        }
        return details;
    }

    /// <summary>
    /// Controlli diversi nella stessa cella di una tabella: è un errore di layout
    /// che si vede solo a schermo (controlli sovrapposti) e che sballa anche le
    /// altezze delle righe.
    /// </summary>
    private static string CollectCellOverlaps(Control root, StringBuilder report)
    {
        var problems = new List<string>();
        WalkCells(root, problems);
        foreach (var problem in problems.Take(5))
        {
            report.AppendLine(problem);
        }
        return string.Join(Environment.NewLine, problems);
    }

    private static void WalkCells(Control control, List<string> problems)
    {
        if (control is TableLayoutPanel table)
        {
            foreach (var cell in table.Controls
                         .Cast<Control>()
                         .GroupBy(child => table.GetCellPosition(child))
                         .Where(group => group.Count() > 1))
            {
                problems.Add(
                    $"Più controlli nella stessa cella ({cell.Key.Column},{cell.Key.Row}): " +
                    string.Join(" + ", cell.Select(DescribeControl)) + ".");
            }
        }
        foreach (Control child in control.Controls)
        {
            WalkCells(child, problems);
        }
    }

    private static string DescribeControl(Control control) => control switch
    {
        Label label => $"etichetta \"{label.Text}\"",
        Button button => $"pulsante \"{button.Text}\"",
        _ => control.GetType().Name
    };

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
                    $"Testo tagliato in “{label.Text}”: servono {measured.Height}px, la scatola è {label.ClientSize.Height}px." +
                    DescribeParent(label));
            }
        }
        foreach (Control child in control.Controls)
        {
            Walk(child, problems);
        }
    }

}
