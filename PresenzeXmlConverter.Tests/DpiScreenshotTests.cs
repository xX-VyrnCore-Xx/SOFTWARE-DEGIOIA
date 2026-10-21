namespace PresenzeXmlConverter.Tests;

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

/// <summary>
/// Schermate dell'app a una scala DPI diversa da 96 (125%, 150%): il runner gira
/// sempre a 96 dpi, ma gli utenti no, e a scale maggiori i font crescono mentre le
/// misure in pixel scritte a mano restano ferme. Il test finge lo spostamento della
/// finestra su uno schermo scalato (WM_DPICHANGED), salva le schermate e scrive in
/// <c>dpi-report.txt</c> i testi tagliati: serve a vedere il problema, non a fermare
/// la CI. Gira solo quando PRESENZEFLOW_DPI_SHOTS indica la scala (per esempio 125).
/// </summary>
public class DpiScreenshotTests
{
    private const int WmDpiChanged = 0x02E0;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [Fact]
    public void RenderTheAppAtAScaledDpi()
    {
        var setting = Environment.GetEnvironmentVariable("PRESENZEFLOW_DPI_SHOTS");
        var directory = Environment.GetEnvironmentVariable("PRESENZEFLOW_SCREENSHOT_DIR");
        if (!int.TryParse(setting, out var percent) || percent <= 100 || string.IsNullOrWhiteSpace(directory))
        {
            return;
        }
        var dpi = 96 * percent / 100;
        Directory.CreateDirectory(directory);
        var report = new StringBuilder();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                RenderMainForm(directory, dpi, percent, report);
                RenderDialogs(directory, dpi, percent, report);
            }
            catch (Exception exception)
            {
                failure = exception;
                report.AppendLine(exception.ToString());
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        File.WriteAllText(Path.Combine(directory, $"dpi{percent}-report.txt"), report.ToString());
        // Il resoconto va salvato comunque; l'eccezione, se c'è, non deve nascondersi.
        Assert.Null(failure);
    }

    private static void RenderMainForm(string directory, int dpi, int percent, StringBuilder report)
    {
        var form = new MainForm
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-4000, -4000),
            Size = new Size(1280, 800)
        };
        form.Show();
        Application.DoEvents();
        EmulateDpi(form, dpi);
        report.AppendLine($"MainForm: DeviceDpi={form.DeviceDpi}, finestra {form.Width}x{form.Height}.");
        for (var section = 0; section < form.SectionCount; section++)
        {
            form.ShowSection(section);
            Application.DoEvents();
            form.PerformLayout();
            Application.DoEvents();
            Save(form, directory, $"dpi{percent}-Sezione{section}");
            Inspect(form, $"Sezione {section}", report);
        }
        form.Dispose();
    }

    private static void RenderDialogs(string directory, int dpi, int percent, StringBuilder report)
    {
        using var settings = new SettingsDialog(GenerationOptions.Default, DateTime.Now.Year, true)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-4000, -4000)
        };
        settings.Show();
        Application.DoEvents();
        EmulateDpi(settings, dpi);
        var tabs = new List<ThemedTabControl>();
        CollectTabs(settings, tabs);
        foreach (var tab in tabs)
        {
            for (var index = 0; index < tab.PageCount; index++)
            {
                tab.SelectedIndex = index;
                Application.DoEvents();
                settings.PerformLayout();
                Application.DoEvents();
                Save(settings, directory, $"dpi{percent}-Impostazioni{index + 1}");
                Inspect(settings, $"Impostazioni, scheda {index + 1}", report);
            }
        }
        settings.Hide();
    }

    /// <summary>Come quando la finestra passa su uno schermo con un'altra scala.</summary>
    private static void EmulateDpi(Form form, int dpi)
    {
        var scale = dpi / 96.0;
        var rect = new Rect
        {
            Left = form.Left,
            Top = form.Top,
            Right = form.Left + (int)Math.Round(form.Width * scale),
            Bottom = form.Top + (int)Math.Round(form.Height * scale)
        };
        var memory = Marshal.AllocHGlobal(Marshal.SizeOf<Rect>());
        try
        {
            Marshal.StructureToPtr(rect, memory, false);
            SendMessage(form.Handle, WmDpiChanged, (IntPtr)((dpi << 16) | dpi), memory);
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
        Application.DoEvents();
        form.PerformLayout();
        Application.DoEvents();
    }

    private static void Save(Form form, string directory, string name)
    {
        using var bitmap = new Bitmap(form.Width, form.Height, PixelFormat.Format24bppRgb);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(directory, $"{name}.png"), ImageFormat.Png);
    }

    /// <summary>Testi che non stanno nella loro scatola, con misure e contenitore.</summary>
    private static void Inspect(Control root, string what, StringBuilder report)
    {
        foreach (var control in Flatten(root))
        {
            if (control is Label { AutoSize: false, Visible: true, Text.Length: > 0 } label)
            {
                var measured = TextRenderer.MeasureText(
                    label.Text,
                    label.Font,
                    new Size(Math.Max(1, label.ClientSize.Width), int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
                if (measured.Height > label.ClientSize.Height + 2)
                {
                    report.AppendLine(
                        $"[{what}] tagliato «{Short(label.Text)}»: servono {measured.Height}px, ci sono {label.ClientSize.Height}px " +
                        $"({label.Parent?.GetType().Name} {label.Parent?.ClientSize.Width}x{label.Parent?.ClientSize.Height}).");
                }
            }
        }
    }

    private static string Short(string text) => text.Length <= 48 ? text : text[..48] + "…";

    private static IEnumerable<Control> Flatten(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
        {
            foreach (var inner in Flatten(child))
            {
                yield return inner;
            }
        }
    }

    private static void CollectTabs(Control root, List<ThemedTabControl> found)
    {
        if (root is ThemedTabControl tab)
        {
            found.Add(tab);
        }
        foreach (Control child in root.Controls)
        {
            CollectTabs(child, found);
        }
    }
}
