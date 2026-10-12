using System.Text.Json;

namespace PresenzeXmlConverter;

/// <summary>
/// Riconoscimento di un'installazione di Ollama e traduzione dell'avanzamento
/// di un download di modello. È tutta logica pura (nessuna UI, nessuna rete,
/// nessun accesso diretto al registro): le uniche dipendenze esterne sono la
/// funzione che dice se un file esiste e le variabili d'ambiente, così il
/// comportamento è verificabile dai test.
/// </summary>
internal static class OllamaSetup
{
    /// <summary>URL ufficiale dell'installer Windows di Ollama.</summary>
    public const string DownloadUrl = "https://ollama.com/download/OllamaSetup.exe";

    /// <summary>Identificativo del pacchetto winget: se disponibile si usa quello.</summary>
    public const string WingetPackageId = "Ollama.Ollama";

    /// <summary>Nome del file con il modello scelto in fase di installazione.</summary>
    public const string ModelMarkerFileName = "ai-model.txt";

    /// <summary>Spazio libero minimo richiesto prima di scaricare un modello (2,5 GB).</summary>
    public const long MinimumFreeBytes = 2_500L * 1024 * 1024;

    /// <summary>Endpoint locale di Ollama.</summary>
    public const string LocalApiBaseAddress = "http://127.0.0.1:11434/";

    /// <summary>Processi/eseguibili che possono avviare il servizio locale.</summary>
    public static readonly string[] ServerExecutableNames = ["ollama app.exe", "ollama.exe"];

    /// <summary>
    /// Percorsi in cui Windows installa Ollama: per-utente (posizione standard),
    /// per-utente alternativa e installazioni a livello macchina.
    /// </summary>
    public static IReadOnlyList<string> CandidateExecutablePaths(
        string? localAppData,
        string? programFiles,
        string? programFilesX86)
    {
        var candidates = new[]
        {
            Combine(localAppData, "Programs", "Ollama"),
            Combine(localAppData, "Ollama"),
            Combine(programFiles, "Ollama"),
            Combine(programFilesX86, "Ollama")
        };
        var found = new List<string>();
        foreach (var directory in candidates)
        {
            if (directory is null)
            {
                continue;
            }
            var path = Path.Combine(directory, "ollama.exe");
            if (!found.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                found.Add(path);
            }
        }
        return found;
    }

    /// <summary>
    /// Primo candidato che esiste davvero. <paramref name="fileExists"/> viene
    /// passato dal chiamante per rendere la funzione verificabile.
    /// </summary>
    public static string? FindExecutable(
        IEnumerable<string> candidates,
        Func<string, bool> fileExists)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && fileExists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    /// <summary>
    /// Cerca un eseguibile nelle cartelle elencate in una variabile PATH.
    /// Serve perché alcune installazioni di Ollama aggiungono la propria
    /// cartella al PATH invece di usare la posizione standard.
    /// </summary>
    public static string? FindInPathList(
        string? pathVariable,
        string fileName,
        Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(pathVariable) || string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }
        foreach (var rawDirectory in pathVariable.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = rawDirectory.Trim().Trim('"');
            if (directory.Length == 0)
            {
                continue;
            }
            string candidate;
            try
            {
                candidate = Path.Combine(directory, fileName);
            }
            catch (ArgumentException)
            {
                // Voce di PATH non valida (caratteri non ammessi): si prosegue.
                continue;
            }
            if (fileExists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    /// <summary>Comando e argomenti per installare Ollama con winget, se presente.</summary>
    public static IReadOnlyList<string> WingetInstallArguments() =>
    [
        "install",
        "--id", WingetPackageId,
        "--exact",
        "--silent",
        "--disable-interactivity",
        "--accept-package-agreements",
        "--accept-source-agreements"
    ];

    /// <summary>Argomenti dell'installer ufficiale: silenzioso, senza riavvio.</summary>
    public static string InstallerArguments(bool silent) =>
        silent ? "/VERYSILENT /NORESTART /SP-" : string.Empty;

    /// <summary>
    /// Modello preferito: prima la scelta fatta in fase di installazione, poi
    /// quella salvata dall'utente nell'app, infine nessuna preferenza.
    /// </summary>
    public static string? ResolvePreferredModel(string? installerSelection, string? userSelection)
    {
        var fromInstaller = NormalizeModelName(installerSelection);
        if (fromInstaller is not null)
        {
            return fromInstaller;
        }
        return NormalizeModelName(userSelection);
    }

    /// <summary>
    /// Normalizza un nome di modello (" qwen2.5:1.5b " → "qwen2.5:1.5b").
    /// Restituisce null per valori vuoti o evidentemente non validi.
    /// </summary>
    public static string? NormalizeModelName(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 96)
        {
            return null;
        }
        if (trimmed.Any(char.IsWhiteSpace) || trimmed.Any(char.IsControl))
        {
            return null;
        }
        return trimmed;
    }

    /// <summary>
    /// Divide "qwen2.5:1.5b" in repository e tag, come fa la cartella dei
    /// manifest di Ollama. Un nome con registro ("host/library/qwen2.5:1.5b")
    /// viene ridotto al solo nome del modello.
    /// </summary>
    public static (string Repository, string Tag) SplitModelReference(string? model)
    {
        var normalized = NormalizeModelName(model) ?? string.Empty;
        var tag = "latest";
        var name = normalized;
        var colon = name.LastIndexOf(':');
        if (colon >= 0)
        {
            tag = name[(colon + 1)..];
            name = name[..colon];
        }
        var slash = name.LastIndexOf('/');
        if (slash >= 0)
        {
            name = name[(slash + 1)..];
        }
        return (name, tag.Length == 0 ? "latest" : tag);
    }

    /// <summary>
    /// File che, se presente, indica che il modello è già stato scaricato.
    /// Ollama usa due layout storici: "manifests" e "manifests-v2".
    /// </summary>
    public static IReadOnlyList<string> ModelManifestPaths(
        string? userProfile,
        string? environmentModelsDirectory,
        string? model)
    {
        var (repository, tag) = SplitModelReference(model);
        if (repository.Length == 0)
        {
            return [];
        }

        var root = string.IsNullOrWhiteSpace(environmentModelsDirectory)
            ? Combine(userProfile, ".ollama", "models")
            : environmentModelsDirectory.Trim();
        if (root is null || root.Length == 0)
        {
            return [];
        }

        var relative = Path.Combine("library", repository, tag);
        return
        [
            Path.Combine(root, "manifests", "registry.ollama.ai", relative),
            Path.Combine(root, "manifests", "ollama.com", relative),
            Path.Combine(root, "manifests-v2", "ollama.com", relative)
        ];
    }

    /// <summary>Il modello è già scaricato in una delle cartelle note?</summary>
    public static bool IsModelDownloaded(
        string? model,
        string? userProfile,
        string? environmentModelsDirectory,
        Func<string, bool> fileExists) =>
        ModelManifestPaths(userProfile, environmentModelsDirectory, model).Any(fileExists);

    /// <summary>
    /// Interpreta una riga NDJSON di /api/pull e aggiorna l'avanzamento
    /// complessivo del download (Ollama scarica più blob, uno per riga).
    /// </summary>
    public static OllamaPullProgress ParsePullProgress(string? jsonLine)
    {
        if (string.IsNullOrWhiteSpace(jsonLine))
        {
            return OllamaPullProgress.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonLine);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return OllamaPullProgress.Empty;
            }

            var status = root.TryGetProperty("status", out var statusElement) &&
                         statusElement.ValueKind == JsonValueKind.String
                ? statusElement.GetString() ?? string.Empty
                : string.Empty;

            var error = root.TryGetProperty("error", out var errorElement) &&
                        errorElement.ValueKind == JsonValueKind.String
                ? errorElement.GetString()
                : null;

            var digest = root.TryGetProperty("digest", out var digestElement) &&
                         digestElement.ValueKind == JsonValueKind.String
                ? digestElement.GetString()
                : null;

            long? total = root.TryGetProperty("total", out var totalElement) &&
                          totalElement.TryGetInt64(out var totalValue)
                ? totalValue
                : null;
            long? completed = root.TryGetProperty("completed", out var completedElement) &&
                              completedElement.TryGetInt64(out var completedValue)
                ? completedValue
                : null;

            return new OllamaPullProgress(status, error, digest, total, completed);
        }
        catch (JsonException)
        {
            return OllamaPullProgress.Empty;
        }
    }

    private static string? Combine(params string?[] parts)
    {
        if (parts.Any(string.IsNullOrWhiteSpace))
        {
            return null;
        }
        return Path.Combine(parts.Select(part => part!).ToArray());
    }
}

/// <summary>Una riga di avanzamento di /api/pull.</summary>
internal sealed record OllamaPullProgress(
    string Status,
    string? Error,
    string? Digest,
    long? Total,
    long? Completed)
{
    public static OllamaPullProgress Empty { get; } = new(string.Empty, null, null, null, null);
}

/// <summary>
/// Segue l'avanzamento di un "ollama pull": Ollama scarica più blob, ognuno con
/// il proprio contatore total/completed, quindi la percentuale va calcolata
/// sulla somma dei byte noti e non sulla singola riga (altrimenti la barra
/// tornerebbe indietro all'inizio di ogni blob).
/// </summary>
internal sealed class OllamaPullTracker
{
    private readonly Dictionary<string, (long Total, long Completed)> _blobs = new(StringComparer.Ordinal);

    /// <summary>Percentuale complessiva (0-100) oppure -1 se non ancora nota.</summary>
    public int Percent { get; private set; } = -1;

    /// <summary>Ultimo stato leggibile ricevuto dal server.</summary>
    public string Status { get; private set; } = "Preparazione…";

    /// <summary>Vero quando il server ha confermato il completamento.</summary>
    public bool IsComplete { get; private set; }

    /// <summary>Ultimo errore riportato dal server, se presente.</summary>
    public string? Error { get; private set; }

    public void Consume(string? jsonLine)
    {
        var progress = OllamaSetup.ParsePullProgress(jsonLine);
        if (progress == OllamaPullProgress.Empty)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(progress.Status))
        {
            Status = progress.Status;
        }
        if (!string.IsNullOrWhiteSpace(progress.Error))
        {
            Error = progress.Error;
        }
        if (string.Equals(progress.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            IsComplete = true;
            Percent = 100;
            return;
        }
        if (progress.Digest is null)
        {
            return;
        }

        if (progress.Total is > 0)
        {
            var completed = Math.Clamp(progress.Completed ?? 0, 0, progress.Total.Value);
            _blobs[progress.Digest] = (progress.Total.Value, completed);
        }

        var total = _blobs.Values.Sum(blob => blob.Total);
        if (total <= 0)
        {
            return;
        }
        var done = _blobs.Values.Sum(blob => blob.Completed);
        // La percentuale non supera il 99% finché il server non dichiara
        // "success": resta visibile che c'è ancora lavoro in corso.
        var computed = (int)Math.Clamp(done * 100 / total, 0, 99);
        // Ollama scarica i blocchi in parallelo e li annuncia man mano: quando
        // un nuovo blocco entra nel conteggio il denominatore cresce. Meglio
        // tenere il valore più alto già mostrato che far tornare indietro una
        // barra che l'utente sta guardando.
        Percent = Math.Max(Percent, computed);
    }
}
