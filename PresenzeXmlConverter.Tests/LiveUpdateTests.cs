namespace PresenzeXmlConverter.Tests;

public class LiveUpdateTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FirstCheckAlwaysRuns()
    {
        Assert.True(UpdateCadence.ShouldCheck(null, Now, staleOnly: true));
        Assert.True(UpdateCadence.ShouldCheck(null, Now, staleOnly: false));
    }

    [Fact]
    public void CloseChecksAreSkipped()
    {
        Assert.False(UpdateCadence.ShouldCheck(Now.AddSeconds(-30), Now, staleOnly: false));
        Assert.True(UpdateCadence.ShouldCheck(Now.AddMinutes(-3), Now, staleOnly: false));
    }

    [Fact]
    public void ReactivatingNeedsFiveMinutesSinceTheLastCheck()
    {
        Assert.False(UpdateCadence.ShouldCheck(Now.AddMinutes(-4), Now, staleOnly: true));
        Assert.True(UpdateCadence.ShouldCheck(Now.AddMinutes(-6), Now, staleOnly: true));
    }

    [Fact]
    public void ClockGoingBackwardsChecksAgain()
    {
        Assert.True(UpdateCadence.ShouldCheck(Now.AddMinutes(10), Now, staleOnly: false));
    }

    [Fact]
    public void PollingStaysWellUnderTheGitHubAnonymousLimit()
    {
        var perHour = TimeSpan.FromHours(1) / UpdateCadence.PollInterval;
        Assert.True(perHour <= 12, $"{perHour} richieste all'ora");
        Assert.True(UpdateCadence.PollInterval > UpdateCadence.MinimumGap);
        Assert.True(UpdateCadence.RetryInterval >= UpdateCadence.MinimumGap);
    }

    [Theory]
    [InlineData(@"C:\Users\Mario Rossi\AppData\Local\Programs\PresenzeFlow\PresenzeFlow.exe")]
    [InlineData(@"D:\App\PresenzeFlow.exe")]
    public void StartupCommandRoundTripsThePath(string path)
    {
        var command = StartupLaunch.Command(path);
        Assert.Equal(path, StartupLaunch.PathOf(command));
        Assert.True(StartupLaunch.IsCurrent(command, path));
        Assert.EndsWith(" --tray", command, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupCommandFromAnotherPathOrWithoutTheFlagIsNotCurrent()
    {
        Assert.False(StartupLaunch.IsCurrent(StartupLaunch.Command(@"C:\Vecchio\App.exe"), @"C:\Nuovo\App.exe"));
        Assert.False(StartupLaunch.IsCurrent("\"C:\\Nuovo\\App.exe\"", @"C:\Nuovo\App.exe"));
        Assert.False(StartupLaunch.IsCurrent(null, @"C:\Nuovo\App.exe"));
        Assert.Null(StartupLaunch.PathOf("  "));
        Assert.Null(StartupLaunch.PathOf("\"senza chiusura"));
    }

    [Fact]
    public void BackgroundStartIsRecognisedFromArguments()
    {
        Assert.True(StartupLaunch.IsBackgroundStart(["--tray"]));
        Assert.True(StartupLaunch.IsBackgroundStart(["x", "--TRAY"]));
        Assert.False(StartupLaunch.IsBackgroundStart([]));
        Assert.False(StartupLaunch.IsBackgroundStart(["--altro"]));
    }

    [Fact]
    public void InstallerStartupEntryMatchesWhatTheAppWrites()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Installer.iss")))
        {
            directory = directory.Parent;
        }
        if (directory is null)
        {
            return;
        }
        var script = File.ReadAllText(Path.Combine(directory.FullName, "Installer.iss"));
        Assert.Contains($"ValueName: \"{StartupLaunch.RegistryValueName}\"", script, StringComparison.Ordinal);
        Assert.Contains(StartupLaunch.BackgroundFlag, script, StringComparison.Ordinal);
        Assert.Contains("Flags: uninsdeletevalue", script, StringComparison.Ordinal);
        Assert.Contains("Name: \"startup\"", script, StringComparison.Ordinal);
    }
}
