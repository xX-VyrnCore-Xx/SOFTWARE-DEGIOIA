using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PresenzeXmlConverter;

/// <summary>Informazioni su una Release pubblicata sul repository GitHub.</summary>
internal sealed record UpdateInfo(
    Version Version,
    string TagName,
    string Notes,
    string InstallerName,
    string InstallerApiUrl,
    long InstallerSize,
    string? ChecksumApiUrl);

/// <summary>Esito della verifica di un token GitHub: esito e messaggio per l'utente.</summary>
internal sealed record TokenCheckResult(bool Succeeded, string Message);

/// <summary>Esito di un controllo aggiornamenti: nessun errore, nessun aggiornamento o errore leggibile.</summary>
internal sealed record UpdateCheckResult(bool Succeeded, UpdateInfo? Update, string? Error, bool IsTransient = false)
{
    public static UpdateCheckResult UpToDate { get; } = new(true, null, null);

    /// <param name="transient">Il problema passa da solo (rete assente, GitHub lento, limite di richieste): conviene riprovare presto.</param>
    public static UpdateCheckResult Failed(string error, bool transient = false) => new(false, null, error, transient);
}

/// <summary>
/// Legge le Release dal repository GitHub. La repo è **privata**, quindi ogni
/// chiamata deve essere autenticata. I token si provano in quest'ordine: l'ultimo
/// che ha funzionato, la variabile d'ambiente PRESENZEFLOW_GITHUB_TOKEN, quello
/// incollato in Impostazioni, il file github.token nella cartella dati e infine
/// quello **iniettato nella build** dalla CI (vedi <see cref="EmbeddedUpdateToken"/>):
/// se uno è scaduto o revocato si passa al successivo, quindi gli aggiornamenti
/// restano collegati. Le richieste di rete ripetono da sole i tentativi falliti
/// per un problema transitorio (rete instabile, errori 5xx).
/// </summary>
internal static class UpdateService
{
    public const string DefaultOwner = "xX-VyrnCore-Xx";
    public const string DefaultRepository = "SOFTWARE-DEGIOIA";
    public const string TokenEnvironmentVariable = "PRESENZEFLOW_GITHUB_TOKEN";

    private const string UserAgent = "PresenzeFlow-Updater";

    /// <summary>Attesa di una richiesta all'API (non vale per lo scaricamento dell'installer).</summary>
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Pause fra i tentativi di una richiesta fallita per un problema transitorio.</summary>
    internal static TimeSpan[] RetryDelays { get; set; } = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)];

    /// <summary>
    /// Un solo client per tutta la vita del programma: il token viaggia nella
    /// singola richiesta, quindi cambiare token non richiede un client nuovo e il
    /// download di un installer non si interrompe se la richiesta successiva ne
    /// crea un altro.
    /// </summary>
    private static readonly HttpClient SharedClient = CreateClient(null);

    /// <summary>Ultimo token che GitHub ha accettato: viene riprovato per primo.</summary>
    private static string? _workingToken;

    /// <summary>
    /// Token impostato in questa sessione dal dialogo Impostazioni: ha la
    /// precedenza sul file, così il collegamento funziona subito dopo averlo
    /// incollato, senza riavviare il programma.
    /// </summary>
    public static string? SessionToken { get; set; }

    public static string TokenFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeGioia",
        "PresenzeXmlConverter",
        "github.token");

    /// <summary>
    /// Token di sola lettura da usare per primo (null se non c'è nulla di
    /// configurato né di incorporato). Per l'elenco completo vedi
    /// <see cref="ResolveTokens"/>.
    /// </summary>
    public static string? ResolveToken() => ResolveTokens().FirstOrDefault();

    /// <summary>
    /// Tutti i token disponibili, nell'ordine in cui vanno provati, senza
    /// doppioni: l'ultimo che ha funzionato, poi quelli dell'utente (ambiente,
    /// sessione, file) e per ultimo quello incorporato nella build.
    /// </summary>
    public static IReadOnlyList<string> ResolveTokens()
    {
        var tokens = new List<string>();
        void Add(string? value)
        {
            if (!string.IsNullOrEmpty(value) && !tokens.Contains(value, StringComparer.Ordinal))
            {
                tokens.Add(value);
            }
        }

        Add(_workingToken);
        Add(ResolveUserToken());
        Add(ReadFileToken());
        Add(EmbeddedUpdateToken.Resolve());
        return tokens;
    }

    /// <summary>
    /// Token scelto dall'utente: prima la variabile d'ambiente, poi quello della
    /// sessione e infine il file locale. Non comprende quello incorporato nella
    /// build, che l'utente non deve vedere né modificare.
    /// </summary>
    public static string? ResolveUserToken()
    {
        var fromEnvironment = SanitizeToken(Environment.GetEnvironmentVariable(TokenEnvironmentVariable));
        if (fromEnvironment.Length > 0)
        {
            return fromEnvironment;
        }
        var fromSession = SanitizeToken(SessionToken);
        if (fromSession.Length > 0)
        {
            return fromSession;
        }
        return ReadFileToken();
    }

    private static string? ReadFileToken()
    {
        try
        {
            if (File.Exists(TokenFilePath))
            {
                var value = SanitizeToken(File.ReadAllText(TokenFilePath));
                if (value.Length > 0)
                {
                    return value;
                }
            }
        }
        catch (IOException)
        {
            // Token illeggibile: si prosegue senza, come se non fosse configurato.
        }
        catch (UnauthorizedAccessException)
        {
        }
        return null;
    }

    /// <summary>
    /// Ripulisce un token incollato: toglie spazi, a capo, virgolette e il
    /// prefisso "Bearer"/"token" che a volte viene copiato per errore. Un incolla
    /// con un a capo in fondo funziona comunque.
    /// </summary>
    public static string SanitizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        var token = value.Trim().Trim('"', '\'', '`').Trim();
        token = Regex.Replace(token, @"^(?:bearer|token)\s+", string.Empty, RegexOptions.IgnoreCase);
        token = Regex.Replace(token, @"\s+", string.Empty);
        return token;
    }

    /// <summary>Da dove arriva il token in uso: serve solo per informare l'utente.</summary>
    public static string DescribeTokenSource()
    {
        if (SanitizeToken(Environment.GetEnvironmentVariable(TokenEnvironmentVariable)).Length > 0)
        {
            return $"variabile d'ambiente {TokenEnvironmentVariable}";
        }
        if (SanitizeToken(SessionToken).Length > 0)
        {
            return "impostato in questa sessione";
        }
        try
        {
            if (File.Exists(TokenFilePath) && SanitizeToken(File.ReadAllText(TokenFilePath)).Length > 0)
            {
                return "file di configurazione locale";
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        return EmbeddedUpdateToken.IsPresent ? "incluso nell'app" : "non configurato";
    }

    /// <summary>
    /// Salva il token nel file locale (permessi ristretti all'utente corrente) e
    /// lo attiva subito per la sessione. Non viene mai salvato nel database.
    /// </summary>
    public static bool TrySaveToken(string? value, out string error)
    {
        error = string.Empty;
        var token = SanitizeToken(value);
        if (token.Length == 0)
        {
            error = "Il token inserito è vuoto.";
            return false;
        }
        if (token.Length > 400)
        {
            error = "Il valore inserito non sembra un token GitHub (troppo lungo).";
            return false;
        }
        try
        {
            var directory = Path.GetDirectoryName(TokenFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(TokenFilePath, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"Impossibile salvare il token: {exception.Message}";
            return false;
        }
        SessionToken = token;
        _workingToken = null;
        return true;
    }

    /// <summary>Dimentica il token (sessione e file locale).</summary>
    public static bool TryClearToken(out string error)
    {
        error = string.Empty;
        SessionToken = null;
        _workingToken = null;
        try
        {
            if (File.Exists(TokenFilePath))
            {
                File.Delete(TokenFilePath);
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"Impossibile cancellare il token salvato: {exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// Verifica subito un token (quello appena inserito oppure quello configurato)
    /// interrogando il repository: 200 = accesso completo, 401 scaduto, 403 senza
    /// permessi, 404 repo privata non visibile al token.
    /// </summary>
    public static async Task<TokenCheckResult> ValidateTokenAsync(
        string? candidate = null,
        CancellationToken cancellationToken = default)
    {
        var typed = SanitizeToken(candidate);
        var token = typed.Length > 0 ? typed : ResolveToken();
        var endpoint = $"https://api.github.com/repos/{DefaultOwner}/{DefaultRepository}";
        try
        {
            using var client = CreateClient(token);
            using var response = await client.GetAsync(endpoint, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(cancellationToken));
                var name = ReadString(document.RootElement, "full_name") ?? $"{DefaultOwner}/{DefaultRepository}";
                var isPrivate = document.RootElement.TryGetProperty("private", out var privateFlag) &&
                                privateFlag.ValueKind == JsonValueKind.True;
                return new TokenCheckResult(
                    true,
                    $"Collegato a {name} ({(isPrivate ? "privato" : "pubblico")}). " +
                    $"Token da {DescribeTokenSource()}: puoi ricevere gli aggiornamenti pubblicati in Release.");
            }
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new TokenCheckResult(
                    false,
                    "Token non valido o revocato (401). Creane uno nuovo con permesso Contents → Read-only."),
                HttpStatusCode.Forbidden => new TokenCheckResult(
                    false,
                    "Token senza i permessi necessari (403): serve il permesso Contents → Read-only sul repository."),
                HttpStatusCode.NotFound => new TokenCheckResult(
                    false,
                    token is null
                        ? "Il repository è privato: senza token GitHub l'app non può leggere le Release."
                        : "Token valido ma senza accesso a questo repository (404). Controlla che sia selezionato il repository giusto."),
                _ => new TokenCheckResult(
                    false,
                    $"GitHub ha risposto con un codice inatteso ({(int)response.StatusCode}). Riprova più tardi.")
            };
        }
        catch (HttpRequestException exception)
        {
            return new TokenCheckResult(false, $"Impossibile contattare GitHub: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new TokenCheckResult(false, "GitHub non ha risposto in tempo. Controlla la connessione e riprova.");
        }
        catch (JsonException)
        {
            return new TokenCheckResult(false, "La risposta di GitHub non è interpretabile.");
        }
    }

    /// <summary>Estrae "1.7.0" da tag come "v1.7.0" o "release-1.7.0".</summary>
    public static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }
        var match = Regex.Match(tag.Trim().TrimStart('v', 'V'), @"^\d+(\.\d+){0,3}");
        if (!match.Success)
        {
            return null;
        }
        var parts = match.Value.Split('.');
        while (parts.Length < 4)
        {
            parts = [.. parts, "0"];
        }
        return Version.TryParse(string.Join('.', parts), out var version) ? version : null;
    }

    /// <summary>Confronto normalizzato a quattro componenti (1.8 = 1.8.0.0).</summary>
    public static bool IsNewer(Version candidate, Version current) =>
        Normalize(candidate) > Normalize(current);

    private static Version Normalize(Version version) => new(
        version.Major,
        version.Minor,
        Math.Max(version.Build, 0),
        Math.Max(version.Revision, 0));

    /// <summary>
    /// Cerca l'ultima Release. Restituisce <see cref="UpdateCheckResult.UpToDate"/>
    /// quando la versione installata è già la più recente.
    /// </summary>
    public static async Task<UpdateCheckResult> CheckAsync(
        Version currentVersion,
        string? endpointOverride,
        CancellationToken cancellationToken = default)
    {
        var hasToken = ResolveTokens().Count > 0;
        var endpoint = string.IsNullOrWhiteSpace(endpointOverride)
            ? $"https://api.github.com/repos/{DefaultOwner}/{DefaultRepository}/releases/latest"
            : endpointOverride.Trim();
        try
        {
            using var response = await SendWithFallbackAsync(
                () => new HttpRequestMessage(HttpMethod.Get, endpoint),
                HttpCompletionOption.ResponseContentRead,
                ApiTimeout,
                cancellationToken);
            if (IsRateLimited(response))
            {
                return UpdateCheckResult.Failed(
                    "GitHub ha temporaneamente limitato le richieste: il controllo riprenderà da solo fra poco.",
                    transient: true);
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return !hasToken
                    ? UpdateCheckResult.Failed(
                        "Il repository delle Release è privato: per controllare gli aggiornamenti serve un token di sola lettura. " +
                        $"Imposta la variabile d'ambiente {TokenEnvironmentVariable} oppure il file {TokenFilePath}.")
                    : UpdateCheckResult.Failed("Nessuna Release pubblicata trovata sul repository.");
            }
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return UpdateCheckResult.Failed(
                    "Nessun token GitHub accettato: è non valido, scaduto o senza permesso di lettura dei contenuti (Contents: Read).");
            }
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var tag = ReadString(root, "tag_name") ?? string.Empty;
            var version = ParseVersion(tag);
            if (version is null)
            {
                return UpdateCheckResult.Failed("La Release pubblicata non ha un numero di versione riconoscibile.");
            }
            if (!IsNewer(version, currentVersion))
            {
                return UpdateCheckResult.UpToDate;
            }

            var notes = ReadString(root, "body") ?? string.Empty;
            var releaseName = ReadString(root, "name");
            var installerName = string.Empty;
            string? installerUrl = null;
            string? checksumUrl = null;
            long installerSize = 0;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = ReadString(asset, "name") ?? string.Empty;
                    var url = ReadString(asset, "url");
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        continue;
                    }
                    if (name.EndsWith(".exe.sha256", StringComparison.OrdinalIgnoreCase))
                    {
                        checksumUrl = url;
                    }
                    else if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && installerUrl is null)
                    {
                        installerName = name;
                        installerUrl = url;
                        installerSize = asset.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes)
                            ? bytes
                            : 0;
                    }
                }
            }
            if (installerUrl is null)
            {
                return UpdateCheckResult.Failed("La Release non contiene un installer eseguibile da scaricare.");
            }

            var summary = string.IsNullOrWhiteSpace(releaseName) ? tag : $"{releaseName} ({tag})";
            var fullNotes = notes.Trim().Length == 0
                ? summary
                : $"Vyrn Presence {version.ToString(3)} — {summary}{Environment.NewLine}{Environment.NewLine}{notes.Trim()}";
            return new UpdateCheckResult(
                true,
                new UpdateInfo(version, tag, fullNotes, installerName, installerUrl, installerSize, checksumUrl),
                null);
        }
        catch (HttpRequestException exception)
        {
            return UpdateCheckResult.Failed($"Impossibile contattare GitHub: {exception.Message}", transient: true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UpdateCheckResult.Failed("Il controllo degli aggiornamenti non ha ricevuto risposta in tempo.", transient: true);
        }
        catch (JsonException)
        {
            return UpdateCheckResult.Failed("La risposta di GitHub non è interpretabile.");
        }
    }

    /// <summary>
    /// Scarica l'installer dell'aggiornamento. Gli asset di una repo privata
    /// devono essere richiesti tramite l'API con "Accept: application/octet-stream".
    /// </summary>
    public static async Task DownloadAsync(
        UpdateInfo update,
        string destinationPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default,
        IProgress<(long Received, long Total)>? transfer = null)
    {
        // Una rete instabile può interrompere il trasferimento a metà: si riprova
        // da capo (al massimo tre volte) prima di arrendersi.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await DownloadOnceAsync(update, destinationPath, progress, cancellationToken, transfer);
                return;
            }
            catch (Exception exception) when (
                attempt < 3 &&
                !cancellationToken.IsCancellationRequested &&
                exception is HttpRequestException or IOException)
            {
                progress?.Report(0);
                transfer?.Report((0, 0));
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
            }
        }
    }

    private static async Task DownloadOnceAsync(
        UpdateInfo update,
        string destinationPath,
        IProgress<int>? progress,
        CancellationToken cancellationToken,
        IProgress<(long Received, long Total)>? transfer = null)
    {
        using var response = await SendWithFallbackAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, update.InstallerApiUrl);
                request.Headers.Accept.ParseAdd("application/octet-stream");
                return request;
            },
            HttpCompletionOption.ResponseHeadersRead,
            null,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? update.InstallerSize;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            if (total > 0)
            {
                progress?.Report((int)Math.Min(100, received * 100 / total));
            }
            transfer?.Report((received, total));
        }
        await destination.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Note di una Release specifica, cercata per tag ("v1.8.0" e "1.8.0").
    /// Usata quando l'aggiornamento è avvenuto fuori dall'app e serve comunque
    /// raccontare che cosa è cambiato. È un tentativo: in caso di problemi
    /// restituisce null senza disturbare l'utente.
    /// </summary>
    public static async Task<string?> TryGetReleaseNotesAsync(
        Version version,
        CancellationToken cancellationToken = default)
    {
        var baseEndpoint = $"https://api.github.com/repos/{DefaultOwner}/{DefaultRepository}/releases";
        try
        {
            foreach (var tag in new[] { $"v{version.ToString(3)}", version.ToString(3) })
            {
                using var response = await SendWithFallbackAsync(
                    () => new HttpRequestMessage(HttpMethod.Get, $"{baseEndpoint}/tags/{tag}"),
                    HttpCompletionOption.ResponseContentRead,
                    ApiTimeout,
                    cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }
                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(cancellationToken));
                var notes = ReadString(document.RootElement, "body");
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    return notes;
                }
            }
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException)
        {
        }
        catch (JsonException)
        {
        }
        return null;
    }

    /// <summary>Legge il checksum SHA-256 pubblicato accanto all'installer (se presente).</summary>
    public static async Task<string?> TryGetChecksumAsync(
        UpdateInfo update,
        CancellationToken cancellationToken = default)
    {
        if (update.ChecksumApiUrl is null)
        {
            return null;
        }
        try
        {
            using var response = await SendWithFallbackAsync(
                () =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, update.ChecksumApiUrl);
                    request.Headers.Accept.ParseAdd("application/octet-stream");
                    return request;
                },
                HttpCompletionOption.ResponseContentRead,
                ApiTimeout,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            var match = Regex.Match(text, "[0-9a-fA-F]{64}");
            return match.Success ? match.Value.ToLowerInvariant() : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>Verifica che il file scaricato corrisponda al checksum della Release.</summary>
    public static bool VerifySha256(string path, string expectedHex)
    {
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        return actual.Equals(expectedHex.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// GitHub ha rifiutato la richiesta perché il limite di chiamate è esaurito
    /// (non perché il token è sbagliato): provare un altro token non serve.
    /// </summary>
    internal static bool IsRateLimited(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Forbidden &&
            response.StatusCode != HttpStatusCode.TooManyRequests)
        {
            return false;
        }
        if (response.Headers.RetryAfter is not null)
        {
            return true;
        }
        return response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) &&
               values.Any(value => value.Trim() == "0");
    }

    /// <summary>
    /// Invia la richiesta provando i token disponibili uno dopo l'altro: se
    /// GitHub risponde 401/403/404 con un token (scaduto, revocato o senza
    /// accesso) si passa al successivo. Il token che funziona viene ricordato.
    /// Il chiamante possiede e rilascia la risposta restituita.
    /// </summary>
    private static async Task<HttpResponseMessage> SendWithFallbackAsync(
        Func<HttpRequestMessage> createRequest,
        HttpCompletionOption completion,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        var tokens = ResolveTokens();
        var candidates = tokens.Count == 0 ? new string?[] { null } : tokens.Cast<string?>().ToArray();
        HttpResponseMessage? last = null;
        foreach (var candidate in candidates)
        {
            last?.Dispose();
            last = await SendWithRetryAsync(candidate, createRequest, completion, timeout, cancellationToken);
            var rejected = last.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound ||
                           (last.StatusCode == HttpStatusCode.Forbidden && !IsRateLimited(last));
            if (!rejected)
            {
                if (last.IsSuccessStatusCode && candidate is not null)
                {
                    _workingToken = candidate;
                }
                return last;
            }
        }
        return last!;
    }

    /// <summary>Una richiesta con lo stesso token, ripetuta se il problema è transitorio.</summary>
    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        string? token,
        Func<HttpRequestMessage> createRequest,
        HttpCompletionOption completion,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var request = createRequest();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (timeout is { } wait)
                {
                    limit.CancelAfter(wait);
                }
                var response = await SharedClient.SendAsync(request, completion, limit.Token);
                if ((int)response.StatusCode >= 500 && attempt < RetryDelays.Length)
                {
                    response.Dispose();
                    await Task.Delay(RetryDelays[attempt], cancellationToken);
                    continue;
                }
                return response;
            }
            catch (Exception exception) when (
                attempt < RetryDelays.Length &&
                !cancellationToken.IsCancellationRequested &&
                exception is HttpRequestException or TaskCanceledException)
            {
                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }
        }
    }

    private static HttpClient CreateClient(string? token)
    {
        // Il proxy di sistema resta attivo (con le credenziali di Windows): in
        // un'azienda l'uscita verso GitHub passa quasi sempre da lì.
        var client = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            UseCookies = false,
            UseProxy = true,
            DefaultProxyCredentials = CredentialCache.DefaultCredentials,
            ConnectTimeout = TimeSpan.FromSeconds(10)
        })
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
