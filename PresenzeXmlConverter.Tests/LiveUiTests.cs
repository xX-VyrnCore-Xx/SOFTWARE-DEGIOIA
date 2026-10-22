using System.Text.RegularExpressions;
using System.Windows;
using PresenzeXmlConverter.Wpf;

namespace PresenzeXmlConverter.Tests;

[Collection("Wpf")]
public class LiveUiTests
{
    private sealed class QuietUi : IUpdateUi
    {
        public bool ShowUpdateAvailable(UpdateInfo update, Version running) => false;

        public bool ConfirmUnsavedPreview() => true;

        public void ShowInformation(string title, string message)
        {
        }

        public void ShowError(string title, Exception exception)
        {
        }

        public Task RunInstallAsync(UpdateInfo update, LocalDatabase database) => Task.CompletedTask;

        public void ShowWhatsNew(Version version, string notes)
        {
        }

        public void PromptForToken()
        {
        }
    }

    private static ShellWindow NewWindow(out LocalDatabase database)
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        database = new LocalDatabase(Path.Combine(folder, "app.db"));
        var window = new ShellWindow(new ConversionService(database))
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -4000,
            Top = -4000
        };
        window.Show();
        WpfTestHost.Pump(300);
        return window;
    }

    [Fact]
    public void EveryBrushUsedInTheCodeIsDefinedInTheTheme()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "PresenzeXmlConverter", "Wpf")))
        {
            directory = directory.Parent;
        }
        if (directory is null)
        {
            return;
        }
        var keys = Directory.EnumerateFiles(Path.Combine(directory.FullName, "PresenzeXmlConverter", "Wpf"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), "\"(Brush\\.[A-Za-z]+)\"").Select(match => match.Groups[1].Value))
            .Distinct()
            .ToList();
        Assert.NotEmpty(keys);
        WpfTestHost.Run(() =>
        {
            var application = WpfApp.EnsureApplication();
            foreach (var key in keys)
            {
                Assert.True(application.TryFindResource(key) is not null, $"Il pennello «{key}» non è definito: apparirebbe magenta.");
            }
        });
    }

    [Fact]
    public void ChangingTheThemeRebuildsTheWindowKeepingThePage()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var window = NewWindow(out _);
            window.Navigate(1);
            WpfTestHost.Pump(300);
            var before = window.Conversion;
            Assert.NotNull(before);

            window.ApplyTheme();
            WpfTestHost.Pump(500);

            Assert.Equal(1, window.CurrentSection);
            Assert.NotNull(window.Conversion);
            Assert.NotSame(before, window.Conversion);
            Assert.Equal(0, WpfShellTests.CountMagenta(WpfShellTests.Render(window)));
            window.Close();
        });
    }

    [Fact]
    public void TheUpdateIconFollowsTheUpdateCycleInRealTime()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var window = NewWindow(out var database);
            var found = new UpdateInfo(new Version(1, 99, 0), "v1.99.0", "- Novità", "Setup.exe", "https://example.test/exe", 4, null);
            var controller = new UpdateController(
                database,
                window.Notifier,
                new QuietUi(),
                () => false,
                (_, _) => Task.FromResult(new UpdateCheckResult(true, found, null)),
                new Version(1, 64, 0));
            window.AttachUpdates(controller);
            Assert.Equal(UpdateChipState.UpToDate, window.ChipSnapshot.State);

            WpfTestHost.Await(controller.CheckAsync(userInitiated: false));
            WpfTestHost.Pump(100);
            Assert.Equal(UpdateChipState.Available, window.ChipSnapshot.State);
            Assert.Contains("1.99.0", window.ChipSnapshot.Text);

            controller.ReportProgress(UpdateStep.Download, 42, "42% · 3 MB/s");
            WpfTestHost.Pump(100);
            var downloading = window.ChipSnapshot;
            Assert.Equal(UpdateChipState.Downloading, downloading.State);
            Assert.Equal("Download 42%", downloading.Text);
            Assert.Equal(42, downloading.Percent);
            Assert.False(downloading.ButtonEnabled);
            window.Close();
        });
    }

    [Fact]
    public void GridTextEndsWithEllipsisAndNumbersAreRightAligned()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var grid = new System.Windows.Controls.DataGrid();
            var plain = new System.Windows.Controls.DataGridTextColumn { Header = "Testo" };
            var number = new System.Windows.Controls.DataGridTextColumn { Header = "Cifre" }.RightAligned();
            grid.Columns.Add(plain);
            grid.Columns.Add(number);

            GridColumns.Polish(grid);

            Assert.Same(GridColumns.Trimmed, plain.ElementStyle);
            Assert.NotSame(GridColumns.Trimmed, number.ElementStyle);
            Assert.Contains(number.HeaderStyle.Setters.OfType<Setter>(), setter => Equals(setter.Value, HorizontalAlignment.Right));
        });
    }
}
