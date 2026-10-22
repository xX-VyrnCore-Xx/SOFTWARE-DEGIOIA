namespace PresenzeXmlConverter.Tests;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using PresenzeXmlConverter.Wpf;

public class NotificationPolicyTests
{
    [Fact]
    public void AWindowInUseOnlyGetsTheInWindowCard()
    {
        var plan = NotificationPolicy.Decide(true, false, windowInUse: true, inAppEnabled: true, nativeEnabled: true, systemRequested: true);
        Assert.Equal(new NotificationPlan(true, false, false), plan);
    }

    [Fact]
    public void AHiddenWindowGetsTheTaskbarFlashAndTheBalloon()
    {
        var plan = NotificationPolicy.Decide(false, false, windowInUse: false, inAppEnabled: true, nativeEnabled: true, systemRequested: true);
        Assert.Equal(new NotificationPlan(false, true, true), plan);
    }

    [Fact]
    public void WindowsBalloonsCanBeSwitchedOffButTheFlashStays()
    {
        var plan = NotificationPolicy.Decide(true, true, windowInUse: false, inAppEnabled: true, nativeEnabled: false, systemRequested: true);
        Assert.False(plan.Balloon);
        Assert.True(plan.Flash);
        Assert.False(plan.InApp);
    }

    [Fact]
    public void AnAdviceThatDoesNotAskForTheSystemChannelNeverUsesIt()
    {
        var plan = NotificationPolicy.Decide(true, false, windowInUse: false, inAppEnabled: false, nativeEnabled: true, systemRequested: false);
        Assert.Equal(NotificationPlan.None, plan);
    }

    [Fact]
    public void TheTestShowsWhateverIsAskedWhereverTheWindowIs()
    {
        Assert.Equal(new NotificationPlan(true, false, true), NotificationPolicy.Test(true, true));
        Assert.Equal(new NotificationPlan(false, false, true), NotificationPolicy.Test(false, true));
    }

    [Theory]
    [InlineData("", true, false, "Ask")]
    [InlineData(null, true, false, "Ask")]
    [InlineData("tray", true, false, "MinimizeToTray")]
    [InlineData("exit", true, false, "Exit")]
    [InlineData("tray", false, false, "Exit")]
    [InlineData("tray", true, true, "Exit")]
    [InlineData("qualcosa", true, false, "Ask")]
    public void TheCloseButtonFollowsTheRememberedChoice(string? remembered, bool userClosing, bool force, string expected) =>
        Assert.Equal(Enum.Parse<CloseAction>(expected), CloseBehaviorPolicy.Decide(remembered, userClosing, force));

    [Fact]
    public void OnlyAnExplicitRememberIsStored()
    {
        Assert.Equal("tray", CloseBehaviorPolicy.ToSetting(CloseAction.MinimizeToTray, remember: true));
        Assert.Equal("exit", CloseBehaviorPolicy.ToSetting(CloseAction.Exit, remember: true));
        Assert.Equal(string.Empty, CloseBehaviorPolicy.ToSetting(CloseAction.Exit, remember: false));
        Assert.Equal(string.Empty, CloseBehaviorPolicy.ToSetting(CloseAction.Ask, remember: true));
    }

    [Fact]
    public void NotificationChannelsAreOnUnlessSwitchedOff()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var database = new LocalDatabase(Path.Combine(folder, "app.db"));
            Assert.True(NotificationSettings.InAppEnabled(database));
            Assert.True(NotificationSettings.NativeEnabled(database));
            database.SaveSetting(SettingsService.InAppNotificationsSetting, "false");
            database.SaveSetting(SettingsService.NativeNotificationsSetting, "false");
            Assert.False(NotificationSettings.InAppEnabled(database));
            Assert.False(NotificationSettings.NativeEnabled(database));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

[Collection("Wpf")]
public class WpfNotificationTests
{
    private static ShellWindow NewShell(out LocalDatabase database)
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
    public void TheCardsAppearDedupeAndAreCappedAtFour()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var layer = new ToastLayer();
            var window = new Window { Content = layer, Width = 600, Height = 400, WindowStartupLocation = WindowStartupLocation.Manual, Left = -4000, Top = -4000 };
            window.Show();
            layer.Show(ToastKind.Success, "Conversione completata", "35 movimenti pronti.");
            layer.Show(ToastKind.Success, "Conversione completata", "35 movimenti pronti.");
            Assert.Single(layer.Visible);
            for (var index = 0; index < 6; index++)
            {
                layer.Show(ToastKind.Info, $"Avviso {index}", "Testo");
            }
            Assert.Equal(4, layer.Visible.Count);
            Assert.Equal("Avviso 5", layer.Visible[^1].Title);
            layer.Clear();
            Assert.Empty(layer.Visible);
            window.Close();
        });
    }

    [Fact]
    public void TheNotifierUsesTheChannelsThePolicyChooses()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var window = NewShell(out var database);
            var notifier = window.Notifier;

            // Finestra in uso: cartellino, niente altro.
            notifier.WindowStateOverride = (true, false, true);
            Assert.Equal(new NotificationPlan(true, false, false), notifier.Plan(systemNotification: true));
            notifier.Notify(ToastKind.Success, "File salvato", "consegna.xml · clic per aprirlo.");
            Assert.Contains(window.VisibleToasts, toast => toast.Title == "File salvato");

            // Finestra nel tray: nessun cartellino, ma lampeggio e fumetto (se il fumetto è attivo).
            notifier.WindowStateOverride = (false, false, false);
            Assert.Equal(new NotificationPlan(false, true, true), notifier.Plan(systemNotification: true));
            database.SaveSetting(SettingsService.NativeNotificationsSetting, "false");
            Assert.Equal(new NotificationPlan(false, true, false), notifier.Plan(systemNotification: true));

            // Cartellini spenti dall'utente: nessun cartellino nemmeno a finestra in uso.
            database.SaveSetting(SettingsService.InAppNotificationsSetting, "false");
            notifier.WindowStateOverride = (true, false, true);
            window.ClearToasts();
            notifier.Notify(ToastKind.Info, "Spento", "Non si vede.");
            Assert.Empty(window.VisibleToasts);
            window.Close();
        });
    }

    [Fact]
    public void TheTestNotificationShowsTheCardEvenWithTheWindowOutOfUse()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var window = NewShell(out _);
            window.Notifier.WindowStateOverride = (false, true, false);
            window.Notifier.Test(inApp: true, system: false);
            Assert.Contains(window.VisibleToasts, toast => toast.Title == "Notifiche attive");
            window.Close();
        });
    }

    [Fact]
    public void TheTrayIconHidesAndRestoresTheWindowAndShowsThePendingUpdate()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var window = NewShell(out var database);
            using var tray = new TrayController(database, "Vyrn Presence 1.64.0", System.Drawing.SystemIcons.Application, 96);
            window.AttachTray(tray);

            window.MinimizeToTray();
            Assert.False(window.IsVisible);
            Assert.True(tray.Visible);

            window.RestoreFromTray();
            WpfTestHost.Pump(100);
            Assert.True(window.IsVisible);
            Assert.False(tray.Visible);

            Assert.False(tray.UpdateItem.Visible);
            tray.SetPendingUpdate(new Version(1, 65, 0));
            Assert.True(tray.UpdateItem.Visible);
            Assert.Contains("1.65.0", tray.UpdateItem.Text);
            tray.SetPendingUpdate(null);
            Assert.False(tray.UpdateItem.Visible);
            window.Close();
        });
    }

    [Fact]
    public void TheCloseChoiceDialogReturnsTrayOrExitAndTheRememberFlag()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();

            var tray = CloseChoiceWindow.Create(out var remember, out var trayResult);
            tray.WindowStartupLocation = WindowStartupLocation.Manual;
            tray.Left = -4000;
            tray.Top = -4000;
            tray.Show();
            WpfTestHost.Pump(300);
            Assert.Null(trayResult());
            remember.IsChecked = true;
            tray.Confirm();
            Assert.True(tray.Accepted);
            Assert.Equal(CloseAction.MinimizeToTray, trayResult());

            var exit = CloseChoiceWindow.Create(out _, out var exitResult);
            exit.WindowStartupLocation = WindowStartupLocation.Manual;
            exit.Left = -4000;
            exit.Top = -4000;
            exit.Show();
            WpfTestHost.Pump(300);
            var choice = exit.AddedChoices.Single();
            choice.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.True(exit.Accepted);
            Assert.Equal(CloseAction.Exit, exitResult());
        });
    }

    [Fact]
    public void TheCardsAreDrawnOverThePage()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var window = NewShell(out _);
            window.Notifier.WindowStateOverride = (true, false, true);
            window.Notifier.Notify(ToastKind.Success, "Conversione completata", "35 movimenti pronti. Controlla l'anteprima ed esporta l'XML.");
            window.Notifier.Notify(ToastKind.Warning, "Da controllare", "2 giorni con ore oltre la giornata. Clic per aprire il calendario.");
            WpfTestHost.Pump(600);
            var bitmap = WpfShellTests.Render(window);
            Assert.True(WpfShellTests.CountMagenta(bitmap) == 0, "Avvisi: colori mancanti (magenta).");
            var directory = Environment.GetEnvironmentVariable("PRESENZEFLOW_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(directory, "wpf-Avvisi.png"));
                encoder.Save(stream);
            }
            window.Close();
        });
    }
}
