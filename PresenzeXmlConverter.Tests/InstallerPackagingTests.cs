using System.Text.RegularExpressions;

namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Guardie di regressione sull'installer: è il primo contatto dell'utente e non
/// è eseguibile nei test (richiede Windows, Inno Setup e una vera rete), quindi
/// le sue proprietà essenziali vengono verificate leggendo lo script.
/// </summary>
public class InstallerPackagingTests
{
    [Fact]
    public void InstallerShipsNativeSqliteLibraryRequiredForStartup()
    {
        var repositoryRoot = FindRepositoryRoot();
        var script = ReadInstallerScript();
        if (repositoryRoot is null || script is null)
        {
            // Cartella repo non disponibile in questo ambiente: niente da verificare.
            return;
        }

        // La libreria nativa deve essere copiata accanto all'eseguibile:
        // il single-file publish non la include.
        Assert.Contains(@"Source: ""dist\app\e_sqlite3.dll""; DestDir: ""{app}""", script, StringComparison.Ordinal);

        var publishDirectory = Path.Combine(repositoryRoot, "dist", "app");
        if (Directory.Exists(publishDirectory))
        {
            Assert.True(
                File.Exists(Path.Combine(publishDirectory, "e_sqlite3.dll")),
                "Il publish single-file deve produrre dist/app/e_sqlite3.dll: senza quel file l'app installata non si avvia.");
        }
    }

    /// <summary>
    /// Il download di Ollama è passato da un comando PowerShell silenzioso (nessuna
    /// barra di avanzamento, nessun annullamento, nessun secondo tentativo) alla
    /// pagina di download nativa di Inno Setup.
    /// </summary>
    [Fact]
    public void OllamaIsDownloadedWithAProgressPageAndRetries()
    {
        var script = ReadInstallerScript();
        if (script is null)
        {
            return;
        }

        Assert.Contains("CreateDownloadPage", script, StringComparison.Ordinal);
        Assert.Contains("DownloadPage.Download", script, StringComparison.Ordinal);
        Assert.Contains("DownloadPage.AbortedByUser", script, StringComparison.Ordinal);
        Assert.Contains("function TryDownloadOllama", script, StringComparison.Ordinal);
        Assert.Contains("for Attempt := 1 to 3 do", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-WebRequest", script, StringComparison.Ordinal);
    }

    [Fact]
    public void OllamaIsDetectedAlsoWhenInstalledInACustomFolder()
    {
        var script = ReadInstallerScript();
        if (script is null)
        {
            return;
        }

        Assert.Contains("RegGetSubkeyNames", script, StringComparison.Ordinal);
        Assert.Contains(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", script, StringComparison.Ordinal);
        Assert.Contains("WOW6432Node", script, StringComparison.Ordinal);
        Assert.Contains("InstallLocation", script, StringComparison.Ordinal);
        Assert.Contains("FileSearch('ollama.exe', GetEnv('Path'))", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAiModelIsChosenByTheUserAndNotDownloadedTwice()
    {
        var script = ReadInstallerScript();
        if (script is null)
        {
            return;
        }

        Assert.Contains("TNewRadioButton", script, StringComparison.Ordinal);
        Assert.Contains("ModelFast", script, StringComparison.Ordinal);
        Assert.Contains("ModelBalanced", script, StringComparison.Ordinal);
        Assert.Contains("ModelQuality", script, StringComparison.Ordinal);
        Assert.Contains("ModelAlreadyDownloaded", script, StringComparison.Ordinal);
        Assert.Contains("manifests-v2", script, StringComparison.Ordinal);
        Assert.Contains("ai-model.txt", script, StringComparison.Ordinal);
        // Il modello preferito viene riproposto: un aggiornamento non lo cambia.
        Assert.Contains("InstalledModelFromMarker", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// Il token delle Release è l'unica cosa che l'installazione riceve
    /// dall'esterno: deve poter arrivare dal parametro /GITHUBTOKEN=... o da un
    /// file accanto al setup, e non deve mai cancellare un token già presente.
    /// </summary>
    [Fact]
    public void TheReleaseTokenCanBeProvidedAtInstallTime()
    {
        var script = ReadInstallerScript();
        if (script is null)
        {
            return;
        }

        Assert.Contains("GITHUBTOKEN", script, StringComparison.Ordinal);
        Assert.Contains("github.token", script, StringComparison.Ordinal);
        Assert.Contains("procedure ProvisionGithubToken", script, StringComparison.Ordinal);
        // Se non c'è nulla da salvare si esce subito: un token esistente non
        // viene mai sovrascritto con un valore vuoto.
        Assert.Contains("if Token = '' then", script, StringComparison.Ordinal);
        // Viene salvato anche in installazione silenziosa (distribuzione aziendale).
        Assert.Contains("ProvisionGithubToken();", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// L'installer deve consigliare il modello che l'app preferisce: se le due
    /// scelte divergono, un PC nuovo scarica un modello che l'app non usa.
    /// </summary>
    [Fact]
    public void TheRecommendedModelMatchesTheApplicationPreference()
    {
        var script = ReadInstallerScript();
        if (script is null)
        {
            return;
        }

        var quality = Regex.Match(script, "#define ModelQuality \"([^\"]+)\"")
            .Groups[1].Value;
        Assert.Equal(LocalAiAssistant.DefaultModel, quality);
        Assert.Contains("Consigliato — {#ModelQuality}", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Consigliato — {#ModelBalanced}", script, StringComparison.Ordinal);
        Assert.Contains("AiQualityRadio.Checked := True", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TheInstallerRequiresASupportedWindowsAndLogsEveryInstallation()
    {
        var script = ReadInstallerScript();
        if (script is null)
        {
            return;
        }

        Assert.Contains("MinVersion=", script, StringComparison.Ordinal);
        Assert.Contains("SetupLogging=yes", script, StringComparison.Ordinal);
        Assert.Contains("LZMAUseSeparateProcess=yes", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// Il workflow rifiuta di pubblicare se le versioni non coincidono; questo
    /// test lo segnala prima, direttamente sul PC dello sviluppatore.
    /// </summary>
    [Fact]
    public void TheInstallerVersionMatchesTheApplicationVersion()
    {
        var repositoryRoot = FindRepositoryRoot();
        if (repositoryRoot is null)
        {
            return;
        }
        var project = File.ReadAllText(Path.Combine(repositoryRoot, "PresenzeXmlConverter", "PresenzeXmlConverter.csproj"));
        var script = File.ReadAllText(Path.Combine(repositoryRoot, "Installer.iss"));

        var projectVersion = Regex.Match(project, @"<Version>([^<]+)</Version>").Groups[1].Value;
        var installerVersion = Regex.Match(script, "#define AppVersion \"([^\"]+)\"").Groups[1].Value;

        Assert.False(string.IsNullOrEmpty(projectVersion), "Versione non trovata in PresenzeXmlConverter.csproj.");
        Assert.Equal(projectVersion, installerVersion);
    }

    /// <summary>
    /// Le costanti ISPP referenziate con {#Nome} devono esistere: un refuso qui
    /// compila lo script con un valore sbagliato (o non compila affatto in CI).
    /// </summary>
    [Fact]
    public void EveryPreprocessorConstantUsedIsDefined()
    {
        var script = ReadInstallerScript();
        if (script is null)
        {
            return;
        }

        var defined = Regex.Matches(script, @"^\s*#define\s+(\w+)", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        var used = Regex.Matches(script, @"\{#(\w+)\}")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal);

        Assert.Empty(used.Where(name => !defined.Contains(name)));
    }

    /// <summary>
    /// Il Pascal dello script non è compilabile in questo ambiente: un controllo
    /// di bilanciamento dei blocchi intercetta gli errori più comuni (un begin
    /// senza end) prima che rompano la Release.
    /// </summary>
    [Fact]
    public void ThePascalCodeSectionHasBalancedBlocks()
    {
        var script = ReadInstallerScript();
        if (script is null)
        {
            return;
        }
        var code = script[script.IndexOf("\n[Code]\n", StringComparison.Ordinal)..];
        var stripped = StripCommentsAndStrings(code);

        var opens = CountWords(stripped, "begin") + CountWords(stripped, "try") +
                    CountWords(stripped, "case") + CountWords(stripped, "record");
        var closes = CountWords(stripped, "end");

        Assert.Equal(opens, closes);
        Assert.Equal(0, stripped.Count(character => character == '(') - stripped.Count(character => character == ')'));
    }

    private static int CountWords(string text, string word) =>
        Regex.Matches(text, $@"\b{word}\b").Count;

    private static string StripCommentsAndStrings(string code)
    {
        var builder = new System.Text.StringBuilder();
        for (var index = 0; index < code.Length; index++)
        {
            var character = code[index];
            if (character == '{')
            {
                var closing = code.IndexOf('}', index);
                index = closing < 0 ? code.Length : closing;
                continue;
            }
            if (character == '/' && index + 1 < code.Length && code[index + 1] == '/')
            {
                var newline = code.IndexOf('\n', index);
                index = newline < 0 ? code.Length : newline;
                continue;
            }
            if (character == '\'')
            {
                index++;
                while (index < code.Length)
                {
                    if (code[index] != '\'')
                    {
                        index++;
                        continue;
                    }
                    if (index + 1 < code.Length && code[index + 1] == '\'')
                    {
                        index += 2;
                        continue;
                    }
                    break;
                }
                builder.Append("''");
                continue;
            }
            builder.Append(character);
        }
        return builder.ToString();
    }

    /// <summary>
    /// ISCC.exe non dichiara la versione (le risorse del file dicono 0.0.0.0):
    /// il workflow deve leggerla dal registro delle installazioni. Se si fida
    /// delle risorse del file, il controllo "Inno Setup 6.1 o successivo" vede
    /// una versione 0.0.0.0, blocca la release e non viene pubblicato nulla
    /// (è già successo: la build si è fermata prima di compilare l'installer).
    /// </summary>
    [Fact]
    public void ReleaseWorkflowReadsTheInnoSetupVersionWhereItActuallyIs()
    {
        var workflow = ReadReleaseWorkflow();
        if (workflow is null)
        {
            // Cartella repo non disponibile in questo ambiente: niente da verificare.
            return;
        }

        Assert.Contains("Inno Setup 6_is1", workflow, StringComparison.Ordinal);
        Assert.Contains("DisplayVersion", workflow, StringComparison.Ordinal);
        // Una versione non determinabile non deve bloccare la pubblicazione:
        // a mancare CreateDownloadPage lo segnala ISCC durante la compilazione.
        Assert.Contains("non determinabile", workflow, StringComparison.Ordinal);
    }

    private static string? ReadReleaseWorkflow()
    {
        var path = Path.Combine(FindRepositoryRoot() ?? string.Empty, ".github", "workflows", "release.yml");
        return File.Exists(path) ? NormalizeLineEndings(File.ReadAllText(path)) : null;
    }

    private static string? ReadInstallerScript()
    {
        var path = Path.Combine(FindRepositoryRoot() ?? string.Empty, "Installer.iss");
        return File.Exists(path) ? NormalizeLineEndings(File.ReadAllText(path)) : null;
    }

    /// <summary>
    /// Il checkout su Windows può avere fine riga CRLF mentre le verifiche
    /// confrontano testo su più righe ("\n[Code]\n"): qui si normalizza a LF.
    /// </summary>
    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n").Replace("\r", "\n");

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Installer.iss")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return null;
    }
}
