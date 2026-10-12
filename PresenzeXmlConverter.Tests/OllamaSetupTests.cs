namespace PresenzeXmlConverter.Tests;

/// <summary>
/// L'installazione dell'AI locale è la parte più delicata per l'utente: un
/// download da un gigabyte che riparte da zero, un modello già presente
/// scaricato di nuovo o una barra di avanzamento che torna indietro sono tutti
/// errori concreti. Queste prove verificano la logica che li previene.
/// </summary>
public class OllamaSetupTests
{
    private static readonly Func<string, bool> NothingExists = _ => false;
    private static readonly Func<string, bool> EverythingExists = _ => true;

    [Fact]
    public void CandidatePathsCoverTheDocumentedOllamaLocations()
    {
        var paths = OllamaSetup.CandidateExecutablePaths(@"C:\Users\User\AppData\Local", @"C:\Program Files", @"C:\Program Files (x86)");

        Assert.Equal(4, paths.Count);
        Assert.Contains(Path.Combine(@"C:\Users\User\AppData\Local", "Programs", "Ollama", "ollama.exe"), paths);
        Assert.Contains(Path.Combine(@"C:\Users\User\AppData\Local", "Ollama", "ollama.exe"), paths);
        Assert.Contains(Path.Combine(@"C:\Program Files", "Ollama", "ollama.exe"), paths);
        Assert.Contains(Path.Combine(@"C:\Program Files (x86)", "Ollama", "ollama.exe"), paths);
    }

    [Fact]
    public void CandidatePathsSkipMissingSystemFolders()
    {
        var paths = OllamaSetup.CandidateExecutablePaths(@"C:\Local", null, null);

        // Restano solo le due posizioni per-utente: senza cartelle di sistema
        // note non ha senso inventare percorsi in Program Files.
        Assert.Equal(2, paths.Count);
        Assert.Equal(Path.Combine(@"C:\Local", "Programs", "Ollama", "ollama.exe"), paths[0]);
        Assert.Equal(Path.Combine(@"C:\Local", "Ollama", "ollama.exe"), paths[1]);
    }

    [Fact]
    public void FindExecutableReturnsTheFirstExistingCandidate()
    {
        var candidates = new[] { @"C:\a\ollama.exe", @"C:\b\ollama.exe", @"C:\c\ollama.exe" };

        Assert.Equal(@"C:\b\ollama.exe", OllamaSetup.FindExecutable(candidates, path => path == @"C:\b\ollama.exe"));
        Assert.Null(OllamaSetup.FindExecutable(candidates, NothingExists));
        Assert.Null(OllamaSetup.FindExecutable([], EverythingExists));
    }

    [Fact]
    public void FindInPathListIgnoresQuotesBlankEntriesAndInvalidDirectories()
    {
        var pathVariable = string.Join(';', @"C:\primo", "\"C:\\secondo\"", "   ", "C:\\non\0valido");

        var found = OllamaSetup.FindInPathList(pathVariable, "ollama.exe", candidate => true);
        var missing = OllamaSetup.FindInPathList(pathVariable, "ollama.exe", NothingExists);

        Assert.Equal(Path.Combine(@"C:\primo", "ollama.exe"), found);
        Assert.Null(missing);
        Assert.Null(OllamaSetup.FindInPathList(null, "ollama.exe", EverythingExists));
        Assert.Null(OllamaSetup.FindInPathList(@"C:\primo", string.Empty, EverythingExists));
    }

    [Fact]
    public void WingetInstallCommandIsNonInteractiveAndPinned()
    {
        var arguments = string.Join(' ', OllamaSetup.WingetInstallArguments());

        Assert.Contains("install", arguments, StringComparison.Ordinal);
        Assert.Contains("--id Ollama.Ollama", arguments, StringComparison.Ordinal);
        Assert.Contains("--exact", arguments, StringComparison.Ordinal);
        Assert.Contains("--silent", arguments, StringComparison.Ordinal);
        Assert.Contains("--disable-interactivity", arguments, StringComparison.Ordinal);
        Assert.Contains("--accept-package-agreements", arguments, StringComparison.Ordinal);
        Assert.Contains("--accept-source-agreements", arguments, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallerRunsSilentlyWithoutRebooting()
    {
        Assert.Equal("/VERYSILENT /NORESTART /SP-", OllamaSetup.InstallerArguments(silent: true));
        Assert.Equal(string.Empty, OllamaSetup.InstallerArguments(silent: false));
    }

    [Fact]
    public void PreferredModelComesFromTheInstallerBeforeTheUser()
    {
        // La scelta dell'installer è quella che ha già scaricato il modello.
        Assert.Equal("qwen2.5:0.5b", OllamaSetup.ResolvePreferredModel("qwen2.5:0.5b", "qwen2.5:3b"));
        // Senza scelta dell'installer vale quella dell'utente.
        Assert.Equal("qwen2.5:3b", OllamaSetup.ResolvePreferredModel("  ", " qwen2.5:3b "));
        // Nessuna preferenza: il chiamante usa il modello predefinito.
        Assert.Null(OllamaSetup.ResolvePreferredModel(null, null));
        Assert.Null(OllamaSetup.ResolvePreferredModel("", ""));
    }

    [Fact]
    public void InvalidModelNamesAreRejectedInsteadOfBeingPassedToOllama()
    {
        Assert.Null(OllamaSetup.NormalizeModelName("qwen 2.5:1.5b"));
        Assert.Null(OllamaSetup.NormalizeModelName("qwen2.5:1.5b\n--verbose"));
        Assert.Null(OllamaSetup.NormalizeModelName(new string('a', 200)));
        Assert.Equal("qwen2.5:1.5b", OllamaSetup.NormalizeModelName("  qwen2.5:1.5b  "));
    }

    [Theory]
    [InlineData("qwen2.5:1.5b", "qwen2.5", "1.5b")]
    [InlineData("qwen2.5", "qwen2.5", "latest")]
    [InlineData("ollama.com/library/qwen2.5:3b", "qwen2.5", "3b")]
    [InlineData(null, "", "latest")]
    public void ModelReferencesAreSplitLikeOllamaDoes(string? model, string repository, string tag)
    {
        var (actualRepository, actualTag) = OllamaSetup.SplitModelReference(model);

        Assert.Equal(repository, actualRepository);
        Assert.Equal(tag, actualTag);
    }

    [Fact]
    public void ManifestPathsRespectTheOllamaModelsEnvironmentVariable()
    {
        var custom = OllamaSetup.ModelManifestPaths(@"C:\Users\User", @"D:\modelli", "qwen2.5:1.5b");

        Assert.Equal(3, custom.Count);
        Assert.All(custom, path => Assert.StartsWith(@"D:\modelli", path, StringComparison.Ordinal));
        // Il layout storico e quelli più recenti devono essere tutti considerati.
        Assert.Contains(custom, path => path.Contains(Path.Combine("manifests", "registry.ollama.ai")));
        Assert.Contains(custom, path => path.Contains(Path.Combine("manifests", "ollama.com")));
        Assert.Contains(custom, path => path.Contains("manifests-v2"));
        Assert.All(custom, path => Assert.EndsWith(Path.Combine("library", "qwen2.5", "1.5b"), path, StringComparison.Ordinal));

        var profile = OllamaSetup.ModelManifestPaths(@"C:\Users\User", null, "qwen2.5:1.5b");
        Assert.All(profile, path => Assert.StartsWith(Path.Combine(@"C:\Users\User", ".ollama", "models"), path, StringComparison.Ordinal));

        Assert.Empty(OllamaSetup.ModelManifestPaths(@"C:\Users\User", null, "  "));
        Assert.Empty(OllamaSetup.ModelManifestPaths(null, null, "qwen2.5:1.5b"));
    }

    [Fact]
    public void AModelIsConsideredDownloadedIfAnyKnownLayoutExists()
    {
        var existing = Path.Combine("D:\\modelli", "manifests-v2", "ollama.com", "library", "qwen2.5", "1.5b");

        Assert.True(OllamaSetup.IsModelDownloaded(
            "qwen2.5:1.5b",
            @"C:\Users\User",
            @"D:\modelli",
            path => string.Equals(path, existing, StringComparison.OrdinalIgnoreCase)));
        Assert.False(OllamaSetup.IsModelDownloaded("qwen2.5:1.5b", @"C:\Users\User", @"D:\modelli", NothingExists));
    }

    [Fact]
    public void PullProgressLinesAreParsedIncludingErrors()
    {
        var downloading = OllamaSetup.ParsePullProgress(
            """{"status":"downloading","digest":"sha256:aaa","total":1000,"completed":250}""");

        Assert.Equal("downloading", downloading.Status);
        Assert.Equal(1000, downloading.Total);
        Assert.Equal(250, downloading.Completed);
        Assert.Equal("sha256:aaa", downloading.Digest);
        Assert.Null(downloading.Error);

        var failure = OllamaSetup.ParsePullProgress("""{"error":"pull access denied"}""");
        Assert.Equal("pull access denied", failure.Error);

        Assert.Equal(OllamaPullProgress.Empty, OllamaSetup.ParsePullProgress("non-json"));
        Assert.Equal(OllamaPullProgress.Empty, OllamaSetup.ParsePullProgress(""));
        Assert.Equal(OllamaPullProgress.Empty, OllamaSetup.ParsePullProgress(null));
    }

    [Fact]
    public void TheProgressBarNeverGoesBackwardsWhenANewBlobStarts()
    {
        var tracker = new OllamaPullTracker();

        tracker.Consume("""{"status":"pulling manifest"}""");
        Assert.Equal(-1, tracker.Percent);
        Assert.Equal("pulling manifest", tracker.Status);

        // Primo blob: 1000 byte, per metà scaricato → metà del totale noto.
        tracker.Consume("""{"status":"downloading","digest":"sha256:aaa","total":1000,"completed":500}""");
        var afterFirstHalf = tracker.Percent;
        Assert.Equal(50, afterFirstHalf); // 500 di 1000 byte noti

        // Inizia un secondo blob: la percentuale complessiva non deve tornare a zero.
        tracker.Consume("""{"status":"downloading","digest":"sha256:bbb","total":1000,"completed":0}""");
        Assert.True(tracker.Percent >= afterFirstHalf, $"atteso >= {afterFirstHalf}, trovato {tracker.Percent}");

        // Il secondo blocco è completo ma il primo è ancora a metà: 1500 di 2000.
        tracker.Consume("""{"status":"verifying sha256 digest","digest":"sha256:bbb","total":1000,"completed":1000}""");
        Assert.Equal(75, tracker.Percent);
        Assert.False(tracker.IsComplete);

        // Tutti i byte scaricati ma il server non ha ancora confermato: sotto il 100%.
        tracker.Consume("""{"status":"downloading","digest":"sha256:aaa","total":1000,"completed":1000}""");
        Assert.Equal(99, tracker.Percent);
        Assert.False(tracker.IsComplete);

        tracker.Consume("""{"status":"success"}""");
        Assert.True(tracker.IsComplete);
        Assert.Equal(100, tracker.Percent);
    }

    [Fact]
    public void PullErrorsAreSurfacedToTheCaller()
    {
        var tracker = new OllamaPullTracker();

        tracker.Consume("""{"status":"downloading","digest":"sha256:aaa","total":10,"completed":5}""");
        tracker.Consume("""{"error":"unexpected EOF"}""");

        Assert.Equal("unexpected EOF", tracker.Error);
    }

    [Theory]
    [InlineData("qwen2.5:3b", "qwen2.5:1.5b", false)]
    [InlineData("qwen2.5:1.5b", "qwen2.5:1.5b", true)]
    [InlineData("qwen2.5:1.5b", " QWEN2.5:1.5B ", true)]
    [InlineData("qwen2.5:latest", "qwen2.5", true)]
    [InlineData("qwen2.5:3b", "qwen2.5", false)]
    [InlineData("llama3.2:1b", "qwen2.5:1.5b", false)]
    public void TheInstalledModelMustMatchExactlyIncludingItsTag(
        string installed,
        string preference,
        bool expected) =>
        Assert.Equal(expected, LocalAiAssistant.MatchesModelReference(installed, preference));

    [Fact]
    public void TheMinimumFreeSpaceGuardIsLargeEnoughForAThreeBillionModel()
    {
        Assert.True(OllamaSetup.MinimumFreeBytes >= 2_000L * 1024 * 1024);
    }
}
