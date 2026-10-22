namespace PresenzeXmlConverter.Tests;

using System.Security.Cryptography;
using System.Windows;
using PresenzeXmlConverter.Wpf;

public class UpdateWorkflowTests
{
    private static UpdateInfo Update(string version = "1.99.0", string? checksumUrl = "https://example.test/sha") =>
        new(new Version(version), $"v{version}", "- Novità di prova della versione", "PresenzeFlow-Setup.exe", "https://example.test/exe", 4, checksumUrl);

    [Theory]
    [InlineData(false, "1.99.0", true, false, "Silent")]
    [InlineData(true, "1.99.0", true, false, "ShowDialog")]
    [InlineData(false, null, false, false, "Notify")]
    [InlineData(false, null, true, true, "Notify")]
    [InlineData(false, null, true, false, "ShowDialog")]
    [InlineData(true, null, false, true, "ShowDialog")]
    public void AnUpdateIsAnnouncedWithoutSurprisesWhileSomeoneIsWorking(bool userInitiated, string? skipped, bool inUse, bool startupDone, string expected) =>
        Assert.Equal(Enum.Parse<UpdateAnnouncement>(expected), UpdateWorkflow.Announce(Update(), userInitiated, skipped, inUse, startupDone));

    [Fact]
    public void WhatsNewUsesTheNotesSavedBeforeTheRestartThenTheReleaseThenNothing()
    {
        var current = new Version(1, 65, 0);

        var stored = UpdateWorkflow.PlanWhatsNew(current, "1.64.0", "1.65.0", "- Novità salvate");
        Assert.Equal(WhatsNewKind.StoredNotes, stored.Kind);
        Assert.Equal("- Novità salvate", stored.Notes);
        Assert.True(stored.ClearPending);

        var outside = UpdateWorkflow.PlanWhatsNew(current, "1.64.0", null, null);
        Assert.Equal(WhatsNewKind.FetchedNotes, outside.Kind);
        Assert.False(outside.ClearPending);

        Assert.Equal(WhatsNewKind.None, UpdateWorkflow.PlanWhatsNew(current, null, null, null).Kind);
        Assert.Equal(WhatsNewKind.None, UpdateWorkflow.PlanWhatsNew(current, "1.65.0", null, null).Kind);
        Assert.Equal(WhatsNewKind.None, UpdateWorkflow.PlanWhatsNew(current, "boh", null, null).Kind);

        // Aggiornamento ancora da fare (versione salvata più nuova di quella in uso): nessuna novità.
        Assert.Equal(WhatsNewKind.None, UpdateWorkflow.PlanWhatsNew(current, "1.65.0", "1.66.0", "- Note").Kind);
        // Aggiornamento fatto ma senza note salvate: si prova con la Release.
        var withoutNotes = UpdateWorkflow.PlanWhatsNew(current, "1.64.0", "1.65.0", " ");
        Assert.Equal(WhatsNewKind.FetchedNotes, withoutNotes.Kind);
        Assert.True(withoutNotes.ClearPending);
    }

    [Fact]
    public void ErrorsAreShortenedAndTokenProblemsRecognised()
    {
        Assert.Equal("Prima riga", UpdateWorkflow.SummarizeError("Prima riga\nSeconda"));
        Assert.Equal(80, UpdateWorkflow.SummarizeError(new string('x', 200)).Length);
        Assert.True(UpdateWorkflow.NeedsTokenAttention("La repository è privata"));
        Assert.True(UpdateWorkflow.NeedsTokenAttention("Token non valido"));
        Assert.False(UpdateWorkflow.NeedsTokenAttention("Rete non raggiungibile"));
    }

    private sealed class RecordingUi : IUpdateInstallUi
    {
        public List<UpdateStep> Steps { get; } = [];

        public bool AllowOtherSigner { get; set; }

        public int Asked { get; private set; }

        public CancellationToken Token => CancellationToken.None;

        public void SetStep(UpdateStep step) => Steps.Add(step);

        public void SetDetail(string text)
        {
        }

        public void SetTransfer(TransferMeter meter)
        {
        }

        public bool ConfirmOtherSigner(string message, string currentSigner, string installerSigner)
        {
            Asked++;
            return AllowOtherSigner;
        }
    }

    private static (UpdateInstallerEnvironment Environment, string Folder, List<string> Started) NewEnvironment(
        byte[] installer,
        string? checksum,
        int signatureResult = 0,
        string? runningSigner = null,
        string? fileSigner = null)
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        var started = new List<string>();
        var environment = new UpdateInstallerEnvironment
        {
            Directory = () => folder,
            Download = (_, destination, _, _) =>
            {
                File.WriteAllBytes(destination, installer);
                return Task.CompletedTask;
            },
            GetChecksum = _ => Task.FromResult(checksum),
            VerifySha256 = (path, expected) => string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), expected, StringComparison.OrdinalIgnoreCase),
            VerifySignature = _ => signatureResult,
            RunningSigner = () => runningSigner,
            FileSigner = _ => fileSigner,
            DescribeCurrent = () => "attuale",
            DescribeFile = _ => "installer",
            StartInstaller = started.Add
        };
        return (environment, folder, started);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    [Fact]
    public async Task AVerifiedInstallerIsStartedAndTheNotesAreKeptForAfterTheRestart()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var (environment, folder, started) = NewEnvironment(bytes, Hash(bytes));
        var database = new LocalDatabase(Path.Combine(folder, "app.db"));
        try
        {
            var ui = new RecordingUi();
            await UpdateInstaller.InstallAsync(Update(), database, ui, environment);

            Assert.Equal(new[] { UpdateStep.Download, UpdateStep.Checksum, UpdateStep.Signature, UpdateStep.Install }, ui.Steps);
            var path = Assert.Single(started);
            Assert.True(File.Exists(path));
            Assert.StartsWith(folder, path);
            Assert.Equal("1.99.0", database.GetSetting(UpdateWorkflow.PendingUpdateVersionSetting));
            Assert.Contains("Novità di prova", database.GetSetting(UpdateWorkflow.PendingUpdateNotesSetting));
            Assert.Contains(database.GetActivities(), entry => entry.Kind == ActivityKind.Update);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task AWrongChecksumStopsTheUpdateAndRemovesTheFile()
    {
        var (environment, folder, started) = NewEnvironment([1, 2, 3, 4], new string('0', 64));
        var database = new LocalDatabase(Path.Combine(folder, "app.db"));
        try
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => UpdateInstaller.InstallAsync(Update(), database, new RecordingUi(), environment));
            Assert.Contains("checksum", error.Message);
            Assert.Empty(started);
            Assert.Empty(Directory.GetFiles(folder, "*.exe"));
            Assert.Null(database.GetSetting(UpdateWorkflow.PendingUpdateVersionSetting));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task ADeclaredChecksumThatCannotBeReadStopsTheUpdate()
    {
        var (environment, folder, started) = NewEnvironment([1, 2, 3, 4], checksum: null);
        var database = new LocalDatabase(Path.Combine(folder, "app.db"));
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => UpdateInstaller.InstallAsync(Update(checksumUrl: "https://example.test/sha"), database, new RecordingUi(), environment));
            Assert.Empty(started);
            Assert.Empty(Directory.GetFiles(folder, "*.exe"));

            // Senza checksum dichiarato dalla Release l'installazione prosegue (build di sviluppo: nessuna firma).
            await UpdateInstaller.InstallAsync(Update(checksumUrl: null), database, new RecordingUi(), environment);
            Assert.Single(started);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task AnUnsignedOrTamperedInstallerIsRefusedWhenTheAppIsSigned()
    {
        var bytes = new byte[] { 9, 9 };
        var (unsigned, folder1, started1) = NewEnvironment(bytes, Hash(bytes), runningSigner: "AAAA", fileSigner: null);
        var (tampered, folder2, started2) = NewEnvironment(bytes, Hash(bytes), signatureResult: UpdateSecurity.BadDigest, runningSigner: "AAAA", fileSigner: "AAAA");
        try
        {
            var db1 = new LocalDatabase(Path.Combine(folder1, "app.db"));
            await Assert.ThrowsAsync<InvalidDataException>(() => UpdateInstaller.InstallAsync(Update(), db1, new RecordingUi(), unsigned));
            Assert.Empty(started1);
            var db2 = new LocalDatabase(Path.Combine(folder2, "app.db"));
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => UpdateInstaller.InstallAsync(Update(), db2, new RecordingUi(), tampered));
            Assert.Contains("modificato", error.Message);
            Assert.Empty(started2);
        }
        finally
        {
            Directory.Delete(folder1, recursive: true);
            Directory.Delete(folder2, recursive: true);
        }
    }

    [Fact]
    public async Task ADifferentSignerNeedsTheUsersConfirmation()
    {
        var bytes = new byte[] { 7 };
        var (environment, folder, started) = NewEnvironment(bytes, Hash(bytes), runningSigner: "AAAA", fileSigner: "BBBB");
        var database = new LocalDatabase(Path.Combine(folder, "app.db"));
        try
        {
            var refused = new RecordingUi { AllowOtherSigner = false };
            await Assert.ThrowsAsync<InvalidDataException>(() => UpdateInstaller.InstallAsync(Update(), database, refused, environment));
            Assert.Equal(1, refused.Asked);
            Assert.Empty(started);
            Assert.Empty(Directory.GetFiles(folder, "*.exe"));

            var accepted = new RecordingUi { AllowOtherSigner = true };
            await UpdateInstaller.InstallAsync(Update(), database, accepted, environment);
            Assert.Equal(1, accepted.Asked);
            Assert.Single(started);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task ACancelledDownloadLeavesNothingBehind()
    {
        var (environment, folder, started) = NewEnvironment([1], null);
        var cancelling = new UpdateInstallerEnvironment
        {
            Directory = () => folder,
            Download = (_, destination, _, _) =>
            {
                File.WriteAllBytes(destination, [1]);
                throw new OperationCanceledException();
            }
        };
        var database = new LocalDatabase(Path.Combine(folder, "app.db"));
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => UpdateInstaller.InstallAsync(Update(), database, new RecordingUi(), cancelling));
            Assert.Empty(started);
            Assert.Empty(Directory.GetFiles(folder, "*.exe"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

[Collection("Wpf")]
public class WpfUpdateTests
{
    private sealed class FakeUi : IUpdateUi
    {
        public bool AnswerUpdate { get; set; } = true;

        public bool AnswerUnsaved { get; set; } = true;

        public Exception? InstallError { get; set; }

        public List<string> Log { get; } = [];

        public bool ShowUpdateAvailable(UpdateInfo update, Version running)
        {
            Log.Add("novità " + update.Version);
            return AnswerUpdate;
        }

        public bool ConfirmUnsavedPreview()
        {
            Log.Add("anteprima non salvata");
            return AnswerUnsaved;
        }

        public void ShowInformation(string title, string message) => Log.Add("info: " + message);

        public void ShowError(string title, Exception exception) => Log.Add("errore: " + exception.Message);

        public Task RunInstallAsync(UpdateInfo update, LocalDatabase database)
        {
            Log.Add("installa " + update.Version);
            return InstallError is null ? Task.CompletedTask : Task.FromException(InstallError);
        }

        public void ShowWhatsNew(Version version, string notes) => Log.Add("novità installata " + version + ": " + notes);

        public void PromptForToken() => Log.Add("token");
    }

    private static UpdateInfo Update() =>
        new(new Version(1, 99, 0), "v1.99.0", "- Una novità importante", "Setup.exe", "https://example.test/exe", 4, null);

    private static (ShellWindow Window, UpdateController Controller, FakeUi Ui, LocalDatabase Database) Setup(
        Func<Version, string?, Task<UpdateCheckResult>> check,
        bool hasUnexportedPreview = false)
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        var database = new LocalDatabase(Path.Combine(folder, "app.db"));
        var window = new ShellWindow(new ConversionService(database))
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -4000,
            Top = -4000
        };
        window.Show();
        WpfTestHost.Pump(300);
        var ui = new FakeUi();
        var controller = new UpdateController(database, window.Notifier, ui, () => hasUnexportedPreview, check, new Version(1, 64, 0));
        window.AttachUpdates(controller);
        return (window, controller, ui, database);
    }

    [Fact]
    public void ANewVersionIsAnnouncedWithACardAndTheSidebarBannerAndNeverWithASurpriseWindow()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var (window, controller, ui, database) = Setup((_, _) => Task.FromResult(new UpdateCheckResult(true, Update(), null)));
            window.Notifier.WindowStateOverride = (true, false, true);

            // Primo controllo a finestra in uso: la finestra con le novità, che l'utente rimanda.
            ui.AnswerUpdate = false;
            WpfTestHost.Await(controller.CheckAsync(userInitiated: false));
            Assert.Equal(UpdateAnnouncement.ShowDialog, controller.LastAnnouncement);
            Assert.Contains("novità 1.99.0", ui.Log);
            Assert.Equal("1.99.0", database.GetSetting(UpdateWorkflow.SkippedVersionSetting));
            Assert.True(window.UpdateBannerState.Visible);
            Assert.Contains("1.99.0", window.UpdateBannerState.Text);

            // Controlli successivi: la versione rimandata non si riproporre.
            ui.Log.Clear();
            WpfTestHost.Await(controller.CheckAsync(userInitiated: false));
            Assert.Equal(UpdateAnnouncement.Silent, controller.LastAnnouncement);
            Assert.Empty(ui.Log);

            // Una versione nuova dopo il primo controllo: solo l'avviso, mai la finestra.
            database.SaveSetting(UpdateWorkflow.SkippedVersionSetting, string.Empty);
            WpfTestHost.Await(controller.CheckAsync(userInitiated: false));
            Assert.Equal(UpdateAnnouncement.Notify, controller.LastAnnouncement);
            Assert.DoesNotContain("novità 1.99.0", ui.Log);
            Assert.Contains(window.VisibleToasts, toast => toast.Title.Contains("1.99.0"));
            CloseQuietly(window);
        });
    }

    [Fact]
    public void AUserCheckReportsUpToDateAndErrorsAndAsksForTheTokenWhenNeeded()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var results = new Queue<UpdateCheckResult>([
                UpdateCheckResult.UpToDate,
                UpdateCheckResult.Failed("Rete non raggiungibile", transient: true),
                UpdateCheckResult.Failed("La repository è privata: serve un token")]);
            var (window, controller, ui, _) = Setup((_, _) => Task.FromResult(results.Dequeue()));

            WpfTestHost.Await(controller.CheckAsync(userInitiated: true));
            Assert.Contains("aggiornato alla 1.64.0", controller.StatusText);
            Assert.Contains(ui.Log, line => line.StartsWith("info:") && line.Contains("1.64.0"));
            Assert.False(window.UpdateBannerState.Visible);

            ui.Log.Clear();
            WpfTestHost.Await(controller.CheckAsync(userInitiated: true));
            Assert.True(controller.StatusIsWarning);
            Assert.Contains(ui.Log, line => line.Contains("Rete non raggiungibile"));
            Assert.DoesNotContain("token", ui.Log);

            ui.Log.Clear();
            WpfTestHost.Await(controller.CheckAsync(userInitiated: true));
            Assert.Contains("token", ui.Log);
            CloseQuietly(window);
        });
    }

    [Fact]
    public void AcceptingTheUpdateInstallsAndAsksToCloseAndAFailureIsShownInstead()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var (window, controller, ui, _) = Setup((_, _) => Task.FromResult(new UpdateCheckResult(true, Update(), null)), hasUnexportedPreview: true);
            window.Notifier.WindowStateOverride = (true, false, true);
            var exits = 0;
            controller.ExitRequested += () => exits++;

            // Anteprima non esportata e utente che rinuncia: nessuna installazione.
            ui.AnswerUnsaved = false;
            WpfTestHost.Await(controller.CheckAsync(userInitiated: true));
            Assert.Contains("anteprima non salvata", ui.Log);
            Assert.DoesNotContain("installa 1.99.0", ui.Log);
            Assert.Equal(0, exits);

            // Conferma: l'installer parte e il programma deve chiudersi.
            ui.Log.Clear();
            ui.AnswerUnsaved = true;
            WpfTestHost.Await(controller.CheckAsync(userInitiated: true));
            Assert.Contains("installa 1.99.0", ui.Log);
            Assert.Equal(1, exits);

            // Errore dell'installazione: lo si mostra e il programma resta aperto.
            ui.Log.Clear();
            ui.InstallError = new InvalidDataException("checksum diverso");
            WpfTestHost.Await(controller.CheckAsync(userInitiated: true));
            Assert.Contains(ui.Log, line => line.StartsWith("errore:") && line.Contains("checksum"));
            Assert.Equal(1, exits);

            // Annullato dall'utente: nessun errore.
            ui.Log.Clear();
            ui.InstallError = new OperationCanceledException();
            WpfTestHost.Await(controller.CheckAsync(userInitiated: true));
            Assert.DoesNotContain(ui.Log, line => line.StartsWith("errore:"));
            Assert.Contains("annullato", controller.StatusText);
            CloseQuietly(window);
        });
    }

    [Fact]
    public void AfterAnUpdateTheStoredNotesAreShownOnceAndClearedFromTheDatabase()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var (window, controller, ui, database) = Setup((_, _) => Task.FromResult(UpdateCheckResult.UpToDate));
            database.SaveSetting(UpdateWorkflow.LastRunVersionSetting, "1.63.0");
            database.SaveSetting(UpdateWorkflow.PendingUpdateVersionSetting, "1.64.0");
            database.SaveSetting(UpdateWorkflow.PendingUpdateNotesSetting, "- Le novità della 1.64");

            WpfTestHost.Await(controller.ShowWhatsNewIfNeededAsync());
            Assert.Contains(ui.Log, line => line.StartsWith("novità installata 1.64.0") && line.Contains("Le novità della 1.64"));
            Assert.Equal(string.Empty, database.GetSetting(UpdateWorkflow.PendingUpdateVersionSetting));
            Assert.Equal("1.64.0", database.GetSetting(UpdateWorkflow.LastRunVersionSetting));

            ui.Log.Clear();
            WpfTestHost.Await(controller.ShowWhatsNewIfNeededAsync());
            Assert.Empty(ui.Log);
            CloseQuietly(window);
        });
    }

    [Fact]
    public void TheNotesAndTheProgressWindowsRender()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var notes = ReleaseNotesWindow.Create(
                "Nuova versione 1.99.0 disponibile",
                "La versione installata è la 1.64.0. Aggiorna ora per ricevere le novità.",
                "## Novità\n- Prima voce\n- Seconda voce con un testo piuttosto lungo che va a capo nella finestra delle note\n\nUn paragrafo finale.",
                showUpdateButton: true);
            Show(notes, "Aggiornamento-Novita");
            Assert.Equal("Più tardi", notes.CancelText);

            var progress = new UpdateProgressWindow("1.99.0");
            progress.WindowStartupLocation = WindowStartupLocation.Manual;
            progress.Left = -4000;
            progress.Top = -4000;
            progress.Show();
            WpfTestHost.Pump(300);
            progress.SetStep(UpdateStep.Checksum);
            Assert.Equal(UpdateStep.Checksum, progress.State.Step);
            Assert.Equal(100, progress.State.Percent);
            progress.SetStep(UpdateStep.Install);
            Assert.False(progress.State.CanCancel);
            var bitmap = WpfShellTests.Render(progress);
            Assert.True(WpfShellTests.CountMagenta(bitmap) == 0, "Avanzamento: colori mancanti (magenta).");
            progress.Close();
        });
    }

    // L'uscita chiesta dal controller chiude la finestra di prova durante l'attesa: chiuderla due volte non è un errore.
    private static void CloseQuietly(Window window)
    {
        try
        {
            window.Close();
        }
        catch (InvalidOperationException)
        {
            // Già chiusa.
        }
    }

    private static void Show(Window window, string name)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -4000;
        window.Top = -4000;
        window.Show();
        WpfTestHost.Pump(450);
        var bitmap = WpfShellTests.Render(window);
        Assert.True(WpfShellTests.CountMagenta(bitmap) == 0, $"{name}: colori mancanti (magenta).");
        var directory = Environment.GetEnvironmentVariable("PRESENZEFLOW_SCREENSHOT_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(directory, $"wpf-{name}.png"));
            encoder.Save(stream);
        }
        window.Close();
    }
}
