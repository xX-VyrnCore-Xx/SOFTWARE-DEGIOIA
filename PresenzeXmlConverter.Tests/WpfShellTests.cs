namespace PresenzeXmlConverter.Tests;

using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PresenzeXmlConverter.Wpf;

/// <summary>
/// Un solo thread con il suo dispatcher per tutti i test di WPF: in un processo esiste una sola
/// <c>Application</c> e le finestre vivono sul thread che le ha create.
/// </summary>
internal static class WpfTestHost
{
    private static readonly BlockingCollection<(Action Work, TaskCompletionSource Done)> Queue = new();
    private static readonly Thread Worker = Start();

    private static Thread Start()
    {
        var thread = new Thread(() =>
        {
            foreach (var (work, done) in Queue.GetConsumingEnumerable())
            {
                try
                {
                    work();
                    done.SetResult();
                }
                catch (Exception exception)
                {
                    done.SetException(exception);
                }
            }
        })
        { IsBackground = true, Name = "WpfTestHost" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return thread;
    }

    public static void Run(Action work)
    {
        GC.KeepAlive(Worker);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Queue.Add((work, done));
        done.Task.GetAwaiter().GetResult();
    }

    /// <summary>Lascia lavorare il dispatcher (layout, caricamento, animazioni brevi).</summary>
    public static void Pump(int milliseconds = 0)
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        if (milliseconds > 0)
        {
            var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (DateTime.UtcNow < until)
            {
                var step = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => step.Continue = false));
                Dispatcher.PushFrame(step);
                Thread.Sleep(10);
            }
        }
    }
}

[Collection("Wpf")]
public class WpfShellTests
{
    [Fact]
    public void TheStylesParseInEveryPalette()
    {
        WpfTestHost.Run(() =>
        {
            foreach (var palette in new[] { ThemePalette.Light, ThemePalette.Dark, ThemePalette.Glass })
            {
                var styles = UiStyles.Create(new UiPalette(palette));
                Assert.True(styles.Contains("Brush.Ink"));
                Assert.True(styles.Contains("Primary"));
                Assert.True(styles.Contains("Toggle"));
            }
        });
    }

    [Fact]
    public void TheShellShowsEverySectionWithoutErrorsOrMissingResources()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var window = new ShellWindow
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -4000,
                Top = -4000
            };
            window.Show();
            WpfTestHost.Pump();
            Assert.Equal(6, window.SectionCount);
            var directory = Environment.GetEnvironmentVariable("PRESENZEFLOW_SCREENSHOT_DIR");
            for (var section = 0; section < window.SectionCount; section++)
            {
                window.Navigate(section);
                WpfTestHost.Pump(450);
                Assert.Equal(section, window.CurrentSection);
                var bitmap = Render(window);
                Assert.True(CountMagenta(bitmap) == 0, $"Sezione {window.SectionTitle(section)}: ci sono colori mancanti (magenta).");
                Assert.True(DistinctColors(bitmap) > 6, $"Sezione {window.SectionTitle(section)}: la pagina è vuota.");
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(directory, $"wpf-Sezione{section}.png"));
                    encoder.Save(stream);
                }
            }
            window.Close();
        });
    }

    [Fact]
    public void TheNavRailKeepsTheSelectionAndRaisesTheEvent()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var rail = new NavRail();
            rail.AddItem("Uno", Glyph.Dashboard);
            rail.AddItem("Due", Glyph.Table);
            var raised = 0;
            rail.SelectionChanged += (_, _) => raised++;
            rail.Select(1);
            Assert.Equal(1, rail.SelectedIndex);
            Assert.Equal(1, raised);
            rail.Select(1);
            Assert.Equal(1, raised);
            rail.Select(0, raiseEvent: false);
            Assert.Equal(0, rail.SelectedIndex);
            Assert.Equal(1, raised);
        });
    }

    [Fact]
    public void TheModernInterfaceIsOptInAndFallsBackToClassic()
    {
        var file = Path.Combine(Path.GetTempPath(), $"ui-{Guid.NewGuid():N}.txt");
        try
        {
            Assert.Equal(UiKind.Classic, UiMode.Choose(Array.Empty<string>(), file));
            UiMode.Save(UiKind.Modern, file);
            Assert.Equal(UiKind.Modern, UiMode.Choose(Array.Empty<string>(), file));
            Assert.Equal(UiKind.Classic, UiMode.Choose(new[] { "--classic" }, file));
            UiMode.Save(UiKind.Classic, file);
            Assert.Equal(UiKind.Modern, UiMode.Choose(new[] { "--WPF" }, file));
            Assert.Equal(UiKind.Classic, UiMode.Load(file));
            File.WriteAllText(file, "qualsiasi cosa");
            Assert.Equal(UiKind.Classic, UiMode.Load(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static BitmapSource Render(Window window)
    {
        var content = (FrameworkElement)window.Content;
        var width = (int)Math.Ceiling(content.ActualWidth);
        var height = (int)Math.Ceiling(content.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        return bitmap;
    }

    private static int CountMagenta(BitmapSource bitmap)
    {
        var pixels = Pixels(bitmap);
        var count = 0;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            if (pixels[i] == 255 && pixels[i + 1] == 0 && pixels[i + 2] == 255)
            {
                count++;
            }
        }
        return count;
    }

    private static int DistinctColors(BitmapSource bitmap)
    {
        var pixels = Pixels(bitmap);
        var seen = new HashSet<int>();
        for (var i = 0; i + 3 < pixels.Length && seen.Count < 64; i += 4)
        {
            seen.Add(pixels[i] | (pixels[i + 1] << 8) | (pixels[i + 2] << 16));
        }
        return seen.Count;
    }

    private static byte[] Pixels(BitmapSource bitmap)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        return pixels;
    }
}
