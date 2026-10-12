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

    /// <summary>
    /// La versione minima di Inno Setup dichiarata dal workflow deve bastare per
    /// ogni funzionalità realmente usata dallo script. Non è un dettaglio: la
    /// 1.11.0 è rimasta non pubblicata perché la compilazione si fermava su un
    /// tipo inesistente, e la guardia di versione era tarata su 6.1 mentre lo
    /// script richiede la 6.3 (ShowBaseNameInsteadOfUrl e x64compatible).
    /// </summary>
    [Fact]
    public void ReleaseWorkflowMinimumInnoVersionCoversEveryFeatureTheScriptUses()
    {
        var workflow = ReadReleaseWorkflow();
        var script = ReadInstallerScript();
        if (workflow is null || script is null)
        {
            return;
        }

        var match = Regex.Match(workflow, @"\$minor\s+-lt\s+(\d+)");
        Assert.True(match.Success, "Il workflow non dichiara una versione minima di Inno Setup ($minor -lt N).");
        var requiredMinor = int.Parse(match.Groups[1].Value);

        // Funzionalità dello script -> minor di Inno Setup 6 che le introduce.
        // CreateDownloadPage è della 6.1; ShowBaseNameInsteadOfUrl e
        // ArchitecturesAllowed=x64compatible sono della 6.3.
        if (script.Contains("CreateDownloadPage", StringComparison.Ordinal))
        {
            Assert.True(requiredMinor >= 1, "CreateDownloadPage richiede Inno Setup 6.1 o successivo.");
        }
        if (script.Contains("ShowBaseNameInsteadOfUrl", StringComparison.Ordinal))
        {
            Assert.True(
                requiredMinor >= 3,
                "ShowBaseNameInsteadOfUrl esiste da Inno Setup 6.3: la versione minima del workflow deve essere almeno 6.3.");
        }
        if (script.Contains("ArchitecturesAllowed=x64compatible", StringComparison.Ordinal))
        {
            Assert.True(
                requiredMinor >= 3,
                "ArchitecturesAllowed=x64compatible esiste da Inno Setup 6.3: la versione minima del workflow deve essere almeno 6.3.");
        }
    }

    /// <summary>
    /// In Inno Setup 6 'String' e' Unicode e 'AnsiString' e' un tipo distinto. Per un
    /// parametro 'var' il compilatore NON applica la conversione implicita, quindi
    /// LoadStringFromFile(FileName, var S: AnsiString) rifiuta una variabile String
    /// con "Type mismatch" e la compilazione dell'installer si ferma (è accaduto
    /// davvero, e la Release non veniva pubblicata). Non avendo qui un compilatore
    /// Inno, è questa guardia a verificare i tipi dei parametri AnsiString.
    /// </summary>
    [Fact]
    public void AnsiStringFileFunctionsReceiveAnsiStringArguments()
    {
        var script = ReadInstallerScript();
        if (script is null)
        {
            return;
        }

        // I commenti non fanno parte del codice: vanno tolti prima di cercare le chiamate.
        var code = Regex.Replace(script, @"\{[^}]*\}", " ");
        code = Regex.Replace(code, @"//[^\n]*", " ");

        // funzione di Inno Setup -> indice (0-based) del parametro AnsiString.
        // LoadStringFromFile/LoadStringFromLockedFile/RegQueryBinaryValue lo hanno 'var'
        // (nessuna conversione possibile); SaveStringToFile lo ha 'const', ma qui si
        // preferisce comunque la conversione esplicita UTF8Encode.
        var ansiParameters = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["LoadStringFromFile"] = 1,
            ["LoadStringFromLockedFile"] = 1,
            ["RegQueryBinaryValue"] = 3,
            ["SaveStringToFile"] = 1,
        };

        foreach (var (functionName, index) in ansiParameters)
        {
            foreach (Match call in Regex.Matches(code, $@"\b{functionName}\s*\(", RegexOptions.IgnoreCase))
            {
                var arguments = SplitArguments(code, call.Index + call.Length);
                Assert.True(
                    arguments.Count > index,
                    $"{functionName}: manca l'argomento {index + 1} nella chiamata.");

                var argument = arguments[index].Trim();
                if (Regex.IsMatch(argument, @"^UTF8(Encode|Decode)\(", RegexOptions.IgnoreCase))
                {
                    continue; // conversione esplicita: i tipi sono a posto
                }

                Assert.Matches(@"^[A-Za-z_][A-Za-z0-9_]*$", argument);
                var declared = DeclaredTypesInEnclosingRoutine(code, call.Index, argument);
                Assert.True(
                    declared.Contains("AnsiString"),
                    $"{functionName}: '{argument}' è dichiarato {string.Join("/", declared.Length == 0 ? ["non trovato"] : declared)} " +
                    "ma il parametro attende AnsiString. In Inno Setup 6 'String' e 'AnsiString' sono tipi distinti e per un parametro var " +
                    "non c'è conversione implicita: dichiara la variabile AnsiString e converti con UTF8Decode/UTF8Encode.");
            }
        }
    }

    /// <summary>
    /// I commenti del Pascal Script NON si annidano: il commento finisce alla prima
    /// graffa chiusa. Un commento che cita una costante fra graffe (per esempio la
    /// cartella temporanea) chiude il commento in anticipo e il resto del testo
    /// diventa codice: la compilazione dell'installer si ferma con "'BEGIN' expected".
    /// È accaduto davvero (due commenti della 1.10.0) e nessuno se ne era accorto
    /// perché la release si fermava prima su un altro errore.
    /// </summary>
    [Fact]
    public void InstallerCodeCommentsDoNotNest()
    {
        var script = ReadInstallerScript();
        if (script is null)
        {
            return;
        }

        var codeStart = script.IndexOf("[Code]", StringComparison.Ordinal);
        var code = codeStart >= 0 ? script.Substring(codeStart) : script;
        // I numeri di riga devono riferirsi al file, non al frammento [Code].
        var lineOffset = codeStart > 0 ? script.Substring(0, codeStart).Count(c => c == '\n') : 0;
        var problems = FindNestedComments(code, lineOffset);
        Assert.True(
            problems.Count == 0,
            "Commenti annidati nella sezione [Code]:" + Environment.NewLine +
            string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// Tipi del Pascal Script di Inno Setup 6, ricavati dalle fonti ufficiali della
    /// 6.7 (tipi, enumerazioni e classi registrati dal compilatore). 'HKEY' NON è fra
    /// questi: in Inno Setup 6 le chiavi di registro sono costanti Integer e un
    /// parametro dichiarato 'HKEY' fa fallire la compilazione dell'installer con
    /// "Unknown type 'HKEY'" (è già successo: da allora la release non veniva
    /// pubblicata). Il tipo esiste solo da Inno Setup 7.
    /// </summary>
    private static readonly string[] InnoSetupSixTypes =
    [
        "AnsiString", "BOOL", "Boolean", "Byte",
        "Cardinal", "Char", "Currency", "DWORD",
        "DWORD_PTR", "Double", "Extended", "HRESULT",
        "INT_PTR", "Int64", "IntPtr", "Integer",
        "LongBool", "LongInt", "LongWord", "NativeInt",
        "NativeUInt", "Pointer", "Real", "ShortInt",
        "Single", "SmallInt", "String", "TArrayOfBoolean",
        "TArrayOfChar", "TArrayOfGraphic", "TArrayOfInteger", "TArrayOfString",
        "TBitmapButton", "TBitmapImage", "TComponent", "TCustomFolderTreeView",
        "TDotNetVersion", "TDownloadWizardPage", "TExecOutput", "TExecWait",
        "TExtractionWizardPage", "TFileTime", "TFindRec", "TFolderTreeView",
        "THandle", "TInputDirWizardPage", "TInputFileWizardPage", "TInputOptionWizardPage",
        "TInputQueryWizardPage", "TMsgBoxType", "TNewButton", "TNewCheckBox",
        "TNewCheckListBox", "TNewComboBox", "TNewEdit", "TNewLinkLabel",
        "TNewListBox", "TNewMemo", "TNewNotebook", "TNewNotebookPage",
        "TNewPathEdit", "TNewProgressBar", "TNewRadioButton", "TNewStaticText",
        "TObject", "TOnDownloadProgress", "TOnExtractionProgress", "TOnLog",
        "TOutputMarqueeProgressWizardPage", "TOutputMsgMemoWizardPage", "TOutputMsgWizardPage", "TOutputProgressWizardPage",
        "TPasswordEdit", "TPngImage", "TRichEditViewer", "TSetupForm",
        "TSetupMessageID", "TSetupProcessorArchitecture", "TSetupStep", "TSplitType",
        "TStartMenuFolderTreeView", "TStringList", "TStrings", "TUIStateForm",
        "TUninstallProgressForm", "TUninstallStep", "TWindowsVersion", "TWizardForm",
        "TWizardPage", "UINT", "UINT_PTR", "UInt64",
        "Variant", "WideChar", "Word"
    ];

    /// <summary>
    /// Guardia: ogni tipo usato nella sezione [Code] deve esistere nel Pascal Script
    /// di Inno Setup 6. Qui non c'è un compilatore Inno, quindi è questa verifica a
    /// intercettare subito un tipo inventato invece che al primo run della release.
    /// </summary>
    [Fact]
    public void InstallerCodeUsesOnlyTypesThatInnoSetupSixKnows()
    {
        var script = ReadInstallerScript();
        if (script is null)
        {
            // Cartella repo non disponibile in questo ambiente: niente da verificare.
            return;
        }

        var codeStart = script.IndexOf("[Code]", StringComparison.Ordinal);
        Assert.True(codeStart >= 0, "Sezione [Code] non trovata in Installer.iss.");
        var code = script[codeStart..];

        var used = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        // Funzioni e procedure: tipo di ritorno e tipo di ogni parametro.
        foreach (Match match in Regex.Matches(
                     code,
                     @"^[ \t]*(?:function|procedure)\s+\w+\s*(?:\(([^)]*)\))?\s*(?::\s*([A-Za-z_]\w*))?\s*;",
                     RegexOptions.Multiline))
        {
            AddUsedType(used, match.Groups[2].Value);
            foreach (var parameter in match.Groups[1].Value.Split(';'))
            {
                var separator = parameter.IndexOf(':');
                if (separator >= 0)
                {
                    AddUsedType(used, parameter[(separator + 1)..]);
                }
            }
        }
        // Variabili e costanti tipizzate dentro le sezioni var.
        foreach (Match match in Regex.Matches(code, @"^[ \t]*\w+\s*:\s*([A-Za-z_][\w ]*?)\s*;", RegexOptions.Multiline))
        {
            AddUsedType(used, match.Groups[1].Value);
        }

        var unknown = used
            .Where(type => !InnoSetupSixTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        Assert.True(
            unknown.Length == 0,
            $"Tipi non riconosciuti dal Pascal Script di Inno Setup 6: {string.Join(", ", unknown)}. " +
            "Usa i tipi realmente registrati dal compilatore (per le chiavi di registro: Integer).");
    }

    /// <summary>Registra il tipo usato, ignorando valori di default e 'array of'.</summary>
    private static void AddUsedType(ISet<string> used, string type)
    {
        var value = type;
        var defaultValue = value.IndexOf('=');
        if (defaultValue >= 0)
        {
            value = value[..defaultValue];
        }
        value = value.Trim();
        if (value.StartsWith("array of", StringComparison.OrdinalIgnoreCase))
        {
            value = value["array of".Length..].Trim();
        }
        if (Regex.IsMatch(value, @"^[A-Za-z_]\w*$"))
        {
            used.Add(value);
        }
    }

    private enum CommentScanState
    {
        Code,
        BraceComment,
        ParenComment,
        LineComment,
        StringLiteral,
    }

    /// <summary>
    /// Percorre il testo tenendo traccia di stringhe e commenti e restituisce i
    /// commenti in cui ne compare un altro (graffe o "(*" dentro un commento).
    /// </summary>
    private static List<string> FindNestedComments(string text, int lineOffset)
    {
        var problems = new List<string>();
        var state = CommentScanState.Code;
        var line = 1 + lineOffset;
        var startLine = line;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            switch (state)
            {
                case CommentScanState.Code:
                    if (c == '{')
                    {
                        state = CommentScanState.BraceComment;
                        startLine = line;
                    }
                    else if (c == '/' && next == '/')
                    {
                        state = CommentScanState.LineComment;
                        i++;
                    }
                    else if (c == '(' && next == '*')
                    {
                        state = CommentScanState.ParenComment;
                        startLine = line;
                        i++;
                    }
                    else if (c == '\'')
                    {
                        state = CommentScanState.StringLiteral;
                    }
                    break;

                case CommentScanState.BraceComment:
                    if (c == '}')
                    {
                        state = CommentScanState.Code;
                    }
                    else if (c == '{')
                    {
                        problems.Add($"riga {line}: graffa aperta dentro il commento iniziato a riga {startLine} -> {Snippet(text, i)}");
                    }
                    break;

                case CommentScanState.ParenComment:
                    if (c == '*' && next == ')')
                    {
                        state = CommentScanState.Code;
                        i++;
                    }
                    else if (c == '(' && next == '*')
                    {
                        problems.Add($"riga {line}: commento (* annidato dentro quello iniziato a riga {startLine} -> {Snippet(text, i)}");
                    }
                    break;

                case CommentScanState.LineComment:
                    if (c == '\n')
                    {
                        state = CommentScanState.Code;
                    }
                    break;

                case CommentScanState.StringLiteral:
                    if (c == '\'')
                    {
                        if (next == '\'')
                        {
                            i++;
                        }
                        else
                        {
                            state = CommentScanState.Code;
                        }
                    }
                    break;
            }

            if (c == '\n')
            {
                line++;
            }
        }

        return problems;
    }

    private static string Snippet(string text, int index)
    {
        var end = text.IndexOf('\n', index);
        if (end < 0)
        {
            end = text.Length;
        }
        var snippet = text.Substring(index, end - index).Trim();
        return snippet.Length > 70 ? snippet.Substring(0, 70) : snippet;
    }

    /// <summary>
    /// Divide gli argomenti di una chiamata a partire dal carattere dopo la parentesi
    /// aperta, rispettando parentesi, quadre e stringhe annidate.
    /// </summary>
    private static List<string> SplitArguments(string text, int start)
    {
        var arguments = new List<string>();
        var current = new System.Text.StringBuilder();
        var depth = 1;
        var inString = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\'')
            {
                inString = !inString;
            }
            if (!inString)
            {
                if (c == '(' || c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                }
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }
                else if (c == ',' && depth == 1)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                    continue;
                }
            }
            current.Append(c);
        }

        var last = current.ToString().Trim();
        if (last.Length > 0)
        {
            arguments.Add(last);
        }
        return arguments;
    }

    /// <summary>
    /// Tipi con cui è dichiarata una variabile nella routine che contiene la
    /// posizione indicata (o fra le dichiarazioni globali se non è una variabile
    /// locale): la ricerca per nome e basta confonderebbe omonimi in routine diverse.
    /// </summary>
    private static string[] DeclaredTypesInEnclosingRoutine(string script, int position, string variable)
    {
        var routineStarts = Regex.Matches(script, @"(?m)^(function|procedure)\s+", RegexOptions.IgnoreCase)
            .Select(match => match.Index)
            .ToList();

        var bodyStart = 0;
        var bodyEnd = script.Length;
        foreach (var routineStart in routineStarts)
        {
            if (routineStart <= position)
            {
                bodyStart = routineStart;
                bodyEnd = script.Length;
            }
            else
            {
                bodyEnd = routineStart;
                break;
            }
        }

        var local = DeclaredTypes(script.Substring(bodyStart, bodyEnd - bodyStart), variable);
        if (local.Length > 0)
        {
            return local;
        }

        var globals = routineStarts.Count > 0 ? script.Substring(0, routineStarts[0]) : script;
        return DeclaredTypes(globals, variable);
    }

    private static string[] DeclaredTypes(string text, string variable) =>
        Regex.Matches(text, $@"(?m)^\s*{Regex.Escape(variable)}\s*:\s*([A-Za-z_][A-Za-z0-9_]*)\s*;")
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToArray();

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
