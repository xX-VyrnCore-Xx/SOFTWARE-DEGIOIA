using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace PresenzeXmlConverter;

/// <summary>
/// Assistente AI locale (Ollama su 127.0.0.1). L'AI non scrive MAI l'XML: propone
/// associazioni di colonne, abbinamenti di giustificativi già configurati e
/// osservazioni sul documento generato, che il programma valida comunque.
/// </summary>
internal sealed partial class LocalAiAssistant
{
    /// <summary>
    /// Modello predefinito: fra i modelli piccoli è quello che Ollama stessa
    /// indica per la generazione di JSON, quindi il più affidabile per
    /// estrarre dati strutturati da intestazioni e causali.
    /// </summary>
    public const string DefaultModel = "qwen2.5:3b";

    /// <summary>Modelli accettati, in ordine di preferenza (il primo installato viene usato).</summary>
    private static readonly string[] PreferredModels =
    [
        "qwen2.5:3b",
        "qwen2.5:1.5b",
        "qwen2.5:0.5b",
        "qwen2.5:1.5b-instruct",
        "granite3.3:2b",
        "llama3.2:3b"
    ];

    /// <summary>Famiglie che gestiscono la modalità "ragionamento": per loro il ragionamento viene disattivato.</summary>
    private static readonly string[] ThinkingModelPrefixes = ["qwen3", "deepseek-r1", "magistral", "gpt-oss", "phi4-reasoning"];

    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(3)
    })
    {
        BaseAddress = new Uri(OllamaSetup.LocalApiBaseAddress),
        Timeout = TimeSpan.FromMinutes(20),
        MaxResponseContentBufferSize = 4 * 1024 * 1024
    };

    /// <summary>
    /// Modello preferito: la scelta fatta in fase di installazione o salvata
    /// dall'utente. Ha la precedenza su <see cref="PreferredModels"/>, ma viene
    /// usato solo se è davvero installato.
    /// </summary>
    public string? PreferredModel { get; set; }

    /// <summary>Modello effettivamente in uso, rilevato tra quelli installati.</summary>
    public string ActiveModel { get; private set; } = DefaultModel;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            using var response = await Client.GetAsync("api/tags", timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public async Task<bool> HasModelAsync(CancellationToken cancellationToken = default)
    {
        return await ResolveModelAsync(cancellationToken) is not null;
    }

    /// <summary>
    /// Sceglie il miglior modello tra quelli installati: prima il modello
    /// preferito (scelto in fase di installazione o dall'utente), poi i modelli
    /// piccoli e affidabili sul JSON, infine qualsiasi modello presente.
    /// </summary>
    public async Task<string?> ResolveModelAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync("api/tags", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var installed = document.RootElement.GetProperty("models").EnumerateArray()
            .Select(model => model.GetProperty("name").GetString() ?? string.Empty)
            .Where(name => name.Length > 0)
            .ToArray();

        var preferences = new List<string>();
        if (PreferredModel is not null)
        {
            preferences.Add(PreferredModel);
        }
        preferences.AddRange(PreferredModels);

        // Il confronto è sul nome COMPLETO (tag incluso): "qwen2.5:1.5b" non
        // deve essere usato quando l'unico modello installato è il 3b.
        foreach (var preferred in preferences)
        {
            var match = installed.FirstOrDefault(name => MatchesModelReference(name, preferred));
            if (match is not null)
            {
                ActiveModel = match;
                return match;
            }
        }

        var fallback = installed.FirstOrDefault(name =>
            name.Contains("qwen", StringComparison.OrdinalIgnoreCase));
        if (fallback is not null)
        {
            ActiveModel = fallback;
            return fallback;
        }

        return installed.Length > 0 ? installed[0] : null;
    }

    /// <summary>
    /// Un nome senza tag equivale a ":latest"; il confronto ignora le maiuscole
    /// ma non il tag, così non si sceglie un modello diverso da quello voluto.
    /// </summary>
    internal static bool MatchesModelReference(string installedName, string? preference)
    {
        if (string.IsNullOrWhiteSpace(preference))
        {
            return false;
        }
        var name = installedName.Trim();
        var wanted = preference.Trim();
        if (string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (!wanted.Contains(':'))
        {
            return string.Equals(name, wanted + ":latest", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    /// <summary>Un modello va interrogato senza "ragionamento": risposte più rapide e deterministiche.</summary>
    internal static bool SupportsThinking(string model) =>
        ThinkingModelPrefixes.Any(prefix => model.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Scarica un modello mostrando l'avanzamento reale: lo stream NDJSON di
    /// Ollama riporta i byte scaricati blob per blob.
    /// </summary>
    public async Task PullModelAsync(
        string model,
        IProgress<(string Status, int Percent)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/pull")
        {
            Content = JsonContent.Create(new { name = model, stream = true })
        };
        using var response = await Client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var tracker = new OllamaPullTracker();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            tracker.Consume(line);
            if (tracker.Error is not null)
            {
                throw new InvalidOperationException($"Ollama non ha potuto scaricare il modello: {tracker.Error}");
            }
            progress?.Report((tracker.Status, tracker.Percent));
        }

        if (!tracker.IsComplete && await ResolveModelAsync(cancellationToken) is null)
        {
            throw new InvalidOperationException(
                "Il download del modello non è stato completato: riprova quando la connessione è stabile.");
        }
        ActiveModel = model;
        progress?.Report(("Completato", 100));
    }

    /// <summary>
    /// Propone l'abbinamento fra le intestazioni del foglio e i campi richiesti.
    /// Restituisce null se la proposta non è coerente con le intestazioni reali.
    /// </summary>
    public async Task<ColumnSelection?> SuggestColumnsAsync(
        IReadOnlyList<string> headers,
        CancellationToken cancellationToken = default)
    {
        if (headers.Count == 0)
        {
            return null;
        }

        var numbered = string.Join(
            Environment.NewLine,
            headers.Select((header, index) => $"{index}: {(header.Length == 0 ? "(vuota)" : header)}"));
        var prompt = $$"""
            Sei un assistente esperto di fogli presenze/assenze aziendali.
            Devi indicare quale intestazione corrisponde a ciascun campo.
            Ricevi le intestazioni numerate del foglio:

            {{numbered}}

            Regole:
            - employee = colonna con nome e cognome della persona (NON il codice).
            - policy = colonna della causale/giustificativo (ferie, permesso, malattia...).
            - startDate = colonna della data di inizio. endDate = data di fine: se non
              esiste una colonna distinta, ripeti esattamente startDate.
            - duration = colonna delle ore o dei giorni di assenza.
            - Usa soltanto intestazioni presenti nell'elenco, copiate esattamente.
            - Se un campo non è riconoscibile restituisci la stringa vuota "".
            """;

        var payload = BuildGenerateRequest(prompt, ColumnMappingSchema);
        var root = await GenerateJsonAsync(payload, cancellationToken);
        if (root is null)
        {
            return null;
        }

        var indexes = new[]
        {
            FindHeader(headers, ReadField(root, "employee")),
            FindHeader(headers, ReadField(root, "policy")),
            FindHeader(headers, ReadField(root, "startDate")),
            FindHeader(headers, ReadField(root, "endDate")),
            FindHeader(headers, ReadField(root, "duration"))
        };
        // endDate assente o duplicato di startDate: usa startDate anche come fine.
        if (indexes[3] < 0 && indexes[2] >= 0)
        {
            indexes[3] = indexes[2];
        }
        if (indexes[2] < 0 && indexes[3] >= 0)
        {
            indexes[2] = indexes[3];
        }
        if (indexes.Any(index => index < 0))
        {
            return null;
        }
        for (var first = 0; first < indexes.Length; first++)
        {
            for (var second = first + 1; second < indexes.Length; second++)
            {
                if (indexes[first] == indexes[second] && !(first == 2 && second == 3))
                {
                    return null;
                }
            }
        }
        return new ColumnSelection(indexes[0], indexes[1], indexes[2], indexes[3], indexes[4]);
    }

    /// <summary>
    /// Propone il codice giustificativo per le causali nuove, scegliendo SOLO tra
    /// i codici già configurati: l'AI non può inventare codici ufficiali.
    /// </summary>
    public async Task<Dictionary<string, string>> SuggestPolicyCodesAsync(
        IReadOnlyList<string> policies,
        IReadOnlyDictionary<string, string> knownMappings,
        CancellationToken cancellationToken = default)
    {
        if (knownMappings.Count == 0 || policies.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var prompt = $$"""
            Sei un assistente che classifica giustificativi di assenza aziendali (ferie, permessi, malattia...).
            Per ciascuna NUOVA etichetta scegli un codice SOLO se esiste una corrispondenza semantica chiara
            con una delle associazioni già configurate qui sotto. In caso di dubbio restituisci stringa vuota.
            Non inventare MAI codici: devi riusare esattamente i codici configurati.

            Associazioni configurate: {{JsonSerializer.Serialize(knownMappings)}}
            Nuove etichette: {{JsonSerializer.Serialize(policies)}}
            """;

        var payload = BuildGenerateRequest(prompt, PolicyCodeSchema);
        var root = await GenerateJsonAsync(payload, cancellationToken);
        if (root is not JsonObject suggested)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var permittedCodes = knownMappings.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requestedPolicies = policies.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in suggested)
        {
            var code = TryReadString(property.Value)?.Trim() ?? string.Empty;
            if (requestedPolicies.Contains(property.Key) &&
                permittedCodes.Contains(code) &&
                code.Length is > 0 and <= 3)
            {
                result[property.Key] = code;
            }
        }
        return result;
    }

    /// <summary>
    /// Chiede all'AI di rileggere i movimenti generati e di segnalare soltanto
    /// anomalie plausibili. Il risultato si aggiunge ai controlli deterministici:
    /// non modifica nulla e non inventa codici (vengono scartati).
    /// </summary>
    public async Task<IReadOnlyList<string>> ReviewMovementsAsync(
        string documentSummary,
        IReadOnlyList<string> knownFindings,
        IReadOnlyList<string> configuredCodes,
        CancellationToken cancellationToken = default)
    {
        if (documentSummary.Length == 0)
        {
            return [];
        }

        var prompt = $$"""
            Sei un revisore di movimenti di presenze per la paghe italiana.
            Ti viene dato un riepilogo dei movimenti già generati e i controlli automatici già eseguiti.
            Segnala SOLO anomalie concrete e verificabili nei dati mostrati (ore impossibili, date
            incoerenti, codici dubbi, richieste sovrapposte). Non inventare dati non presenti.
            Se non trovi nulla da segnalare restituisci un elenco vuoto.
            Scorciatoie già segnalate automaticamente (non ripeterle): {{string.Join(" | ", knownFindings.Take(20))}}
            Codici giustificativo ammessi: {{string.Join(", ", configuredCodes)}}

            Riepilogo dei movimenti:
            {{documentSummary}}
            """;

        var payload = BuildGenerateRequest(prompt, ReviewSchema);
        var root = await GenerateJsonAsync(payload, cancellationToken);
        if (root?["osservazioni"] is not JsonArray observations)
        {
            return [];
        }

        var allowedCodes = configuredCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var messages = new List<string>();
        foreach (var observation in observations)
        {
            var message = TryReadString(observation?["messaggio"])?.Trim();
            if (string.IsNullOrWhiteSpace(message) || message.Length > 300)
            {
                continue;
            }
            // Un messaggio che cita codici non configurati è un'invenzione: scartato.
            if (MentionsUnexpectedCode(message, allowedCodes))
            {
                continue;
            }
            messages.Add(message);
            if (messages.Count == 8)
            {
                break;
            }
        }
        return messages;
    }

    /// <summary>
    /// Cerca nel messaggio codici del tipo "F01"/"01" che non siano fra quelli
    /// configurati: è il modo più semplice per intercettare un codice inventato.
    /// </summary>
    internal static bool MentionsUnexpectedCode(string message, IReadOnlySet<string> allowedCodes)
    {
        // Solo due forme sono considerate codici: quelli con prefisso alfabetico
        // ("F01", "M1") e quelli dichiarati come codice ("codice 07"). Così le
        // ore e le date citate nel testo non vengono scambiate per codici.
        foreach (Match match in Regex.Matches(
                     message,
                     @"\b[A-Za-z]{1,2}\d{1,3}\b|(?:codic\w*|cod\.?|giustificativ\w*)\s*[:=]?\s*(?<code>[A-Za-z]{0,3}\d{1,3})",
                     RegexOptions.IgnoreCase))
        {
            var candidate = match.Groups["code"].Success ? match.Groups["code"].Value : match.Value;
            if (!allowedCodes.Contains(candidate))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Riassunto compatto dei movimenti per la revisione AI (mai l'XML completo).</summary>
    internal static string BuildReviewSummary(IReadOnlyList<DailyMovement> movements, int maximumRows = 240)
    {
        if (movements.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var totals = movements
            .GroupBy(movement => movement.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new
            {
                Name = group.Key,
                Minutes = group.Sum(movement => movement.Hours * 60 + movement.Minutes),
                Days = group.Select(movement => movement.Date).Distinct().Count(),
                Codes = string.Join("/", group.Select(movement => movement.ReasonCode).Distinct())
            })
            .OrderByDescending(item => item.Minutes)
            .ToArray();

        builder.AppendLine($"Movimenti totali: {movements.Count}; dipendenti: {totals.Length}.");
        foreach (var total in totals.Take(40))
        {
            builder.AppendLine(
                $"- {total.Name}: {total.Minutes / 60}h {total.Minutes % 60:D2}m su {total.Days} giorni, codici {total.Codes}");
        }

        foreach (var movement in movements.Take(maximumRows))
        {
            builder.AppendLine(
                $"{movement.Date:yyyy-MM-dd} | {movement.EmployeeName} | {movement.EmployeeCode} | " +
                $"{movement.ReasonCode} | {movement.Hours}h{movement.Minutes:D2}");
        }
        if (movements.Count > maximumRows)
        {
            builder.AppendLine($"… e altri {movements.Count - maximumRows} movimenti non elencati.");
        }
        return builder.ToString();
    }

    private static object BuildGenerateRequest(string prompt, JsonObject schema) => new
    {
        model = (string?)null,
        prompt,
        stream = false,
        format = schema,
        keep_alive = "30m",
        options = new
        {
            temperature = 0,
            top_p = 0.1,
            seed = 42,
            num_ctx = 4096,
            num_predict = 800
        }
    };

    private async Task<JsonNode?> GenerateJsonAsync(object payload, CancellationToken cancellationToken)
    {
        var model = await ResolveModelAsync(cancellationToken) ?? DefaultModel;
        var node = JsonSerializer.SerializeToNode(payload)!.AsObject();
        node["model"] = model;
        if (SupportsThinking(model))
        {
            // I modelli "reasoning" risponderebbero con un lungo ragionamento:
            // per l'estrazione serve solo la risposta finale.
            node["think"] = false;
        }

        using var response = await Client.PostAsJsonAsync("api/generate", node, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var raw = document.RootElement.TryGetProperty("response", out var responseElement)
            ? responseElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        try
        {
            return JsonNode.Parse(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Schema dell'abbinamento colonne. L'AI risponde con il testo esatto delle
    /// intestazioni: niente indici, che i modelli piccoli sbagliano spesso.
    /// </summary>
    private static JsonObject ColumnMappingSchema => new()
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["properties"] = new JsonObject
        {
            ["employee"] = new JsonObject { ["type"] = "string" },
            ["policy"] = new JsonObject { ["type"] = "string" },
            ["startDate"] = new JsonObject { ["type"] = "string" },
            ["endDate"] = new JsonObject { ["type"] = "string" },
            ["duration"] = new JsonObject { ["type"] = "string" }
        },
        ["required"] = new JsonArray("employee", "policy", "startDate", "endDate", "duration")
    };

    /// <summary>Schema dei codici giustificativo: oggetto etichetta → codice.</summary>
    private static JsonObject PolicyCodeSchema => new()
    {
        ["type"] = "object",
        ["additionalProperties"] = new JsonObject { ["type"] = "string" }
    };

    private static JsonObject ReviewSchema => new()
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["properties"] = new JsonObject
        {
            ["osservazioni"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["gravita"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JsonArray("errore", "attenzione", "nota")
                        },
                        ["messaggio"] = new JsonObject { ["type"] = "string" }
                    },
                    ["required"] = new JsonArray("gravita", "messaggio")
                }
            }
        },
        ["required"] = new JsonArray("osservazioni")
    };

    private static string ReadField(JsonNode root, string name)
    {
        var value = TryReadString(root[name]);
        return value?.Trim() ?? string.Empty;
    }

    /// <summary>Legge un valore testuale da JSON senza mai lanciare eccezioni.</summary>
    private static string? TryReadString(JsonNode? node)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text))
        {
            return null;
        }
        return text;
    }

    private static int FindHeader(IReadOnlyList<string> headers, string suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion))
        {
            return -1;
        }
        for (var index = 0; index < headers.Count; index++)
        {
            if (string.Equals(headers[index].Trim(), suggestion.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        // Fallback tollerante: confronto normalizzato (maiuscole/spazi/punteggiatura).
        var normalizedSuggestion = Normalize(suggestion);
        for (var index = 0; index < headers.Count; index++)
        {
            if (string.Equals(Normalize(headers[index]), normalizedSuggestion, StringComparison.Ordinal))
            {
                return index;
            }
        }
        // Ultimo tentativo: l'AI ha risposto con una parte dell'intestazione.
        for (var index = 0; index < headers.Count; index++)
        {
            var normalizedHeader = Normalize(headers[index]);
            if (normalizedHeader.Length >= 4 &&
                (normalizedHeader.Contains(normalizedSuggestion, StringComparison.Ordinal) ||
                 normalizedSuggestion.Contains(normalizedHeader, StringComparison.Ordinal)))
            {
                return index;
            }
        }
        return -1;
    }

    private static string Normalize(string value) =>
        string.Join(" ", value.Trim().ToLowerInvariant()
            .Replace('"', ' ')
            .Replace("'", " ")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
