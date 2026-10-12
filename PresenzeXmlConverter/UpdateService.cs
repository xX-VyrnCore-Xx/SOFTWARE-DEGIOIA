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
internal sealed record UpdateCheckResult(bool Succeeded, UpdateInfo? Update, string? Error)
{
    public static UpdateCheckResult UpToDate { get; } = new(true, null, null);

    public static UpdateCheckResult Failed(string error) => new(false, null, error);
}

/// <summary>
/// Legge le Release dal repository GitHub. La repo è **privata**, quindi ogni
/// chiamata deve essere autenticata: il token (Personal Access Token con
/// permesso "Contents: Read") viene letto dalla variabile d'ambiente
/// PRESENZEFLOW_GITHUB_TOKEN oppure dal file github.token nella cartella dati.
/// Il token non è mai incorporato nell'eseguibile.
/// </summary>
internal static class UpdateService
{
    public const string DefaultOwner = "xX-VyrnCore-Xx";
    public const string DefaultRepository = "SOFTWARE-DEGIOIA";
    public const string TokenEnvironmentVariable = "PRESENZEFLOW_GITHUB_TOKEN";

    private const string UserAgent = "PresenzeFlow-Updater";

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
    /// Token di sola lettura: prima la variabile d'ambiente, poi il file locale.
    /// Restituisce null se non è configurato nulla.
    /// </summary>
    public static string? ResolveToken()
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
        return "non configurato";
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
        return true;
    }

    /// <summary>Dimentica il token (sessione e file locale).</summary>
    public static bool TryClearToken(out string error)
    {
        error = string.Empty;
        SessionToken = null;
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
        var token = ResolveToken();
        var endpoint = string.IsNullOrWhiteSpace(endpointOverride)
            ? $"https://api.github.com/repos/{DefaultOwner}/{DefaultRepository}/releases/latest"
            : endpointOverride.Trim();
        try
        {
            using var client = CreateClient(token);
            using var response = await client.GetAsync(endpoint, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return token is null
                    ? UpdateCheckResult.Failed(
                        "Il repository delle Release è privato: per controllare gli aggiornamenti serve un token di sola lettura. " +
                        $"Imposta la variabile d'ambiente {TokenEnvironmentVariable} oppure il file {TokenFilePath}.")
                    : UpdateCheckResult.Failed("Nessuna Release pubblicata trovata sul repository.");
            }
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return UpdateCheckResult.Failed(
                    "Token GitHub non valido, scaduto o senza permesso di lettura dei contenuti (Contents: Read).");
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
                : $"PresenzeFlow {version.ToString(3)} — {summary}{Environment.NewLine}{Environment.NewLine}{notes.Trim()}";
            return new UpdateCheckResult(
                true,
                new UpdateInfo(version, tag, fullNotes, installerName, installerUrl, installerSize, checksumUrl),
                null);
        }
        catch (HttpRequestException exception)
        {
            return UpdateCheckResult.Failed($"Impossibile contattare GitHub: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UpdateCheckResult.Failed("Il controllo degli aggiornamenti non ha ricevuto risposta in tempo.");
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
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(ResolveToken());
        using var request = new HttpRequestMessage(HttpMethod.Get, update.InstallerApiUrl);
        request.Headers.Accept.ParseAdd("application/octet-stream");
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
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
        var token = ResolveToken();
        var baseEndpoint = $"https://api.github.com/repos/{DefaultOwner}/{DefaultRepository}/releases";
        try
        {
            using var client = CreateClient(token);
            foreach (var tag in new[] { $"v{version.ToString(3)}", version.ToString(3) })
            {
                using var response = await client.GetAsync(
                    $"{baseEndpoint}/tags/{tag}",
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
            using var client = CreateClient(ResolveToken());
            using var request = new HttpRequestMessage(HttpMethod.Get, update.ChecksumApiUrl);
            request.Headers.Accept.ParseAdd("application/octet-stream");
            using var response = await client.SendAsync(request, cancellationToken);
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

    private static HttpClient CreateClient(string? token)
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            UseCookies = false,
            UseProxy = false,
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
