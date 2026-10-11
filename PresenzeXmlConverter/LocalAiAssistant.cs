using System.Net.Http.Json;
using System.Text.Json;

namespace PresenzeXmlConverter;

internal sealed partial class LocalAiAssistant
{
    /// <summary>Modello predefinito: bilancia qualità dei suggerimenti e velocità su CPU.</summary>
    public const string DefaultModel = "qwen2.5:1.5b";

    /// <summary>Modelli accettati, in ordine di preferenza (il primo installato viene usato).</summary>
    private static readonly string[] PreferredModels =
    [
        "qwen2.5:1.5b",
        "qwen2.5:3b",
        "qwen2.5:1.5b-instruct",
        "qwen2.5:0.5b"
    ];

    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(3)
    })
    {
        BaseAddress = new Uri("http://127.0.0.1:11434/"),
        Timeout = TimeSpan.FromMinutes(20),
        MaxResponseContentBufferSize = 4 * 1024 * 1024
    };

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
    /// Sceglie il miglior modello tra quelli installati: preferisce i modelli della lista
    /// in ordine di preferenza, accetta qualsiasi variante "qwen2.5" e infine qualsiasi
    /// modello presente (l'attività è comunque validata lato codice).
    /// </summary>
    public async Task<string?> ResolveModelAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync("api/tags", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var installed = document.RootElement.GetProperty("models").EnumerateArray()
            .Select(model => model.GetProperty("name").GetString() ?? string.Empty)
            .Where(name => name.Length > 0)
            .Select(name => name.Split(':')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var preferred in PreferredModels)
        {
            var baseName = preferred.Split(':')[0];
            if (installed.Contains(baseName, StringComparer.OrdinalIgnoreCase))
            {
                ActiveModel = preferred;
                return preferred;
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

    public async Task DownloadModelAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Client.PostAsJsonAsync(
            "api/pull",
            new { name = DefaultModel, stream = false },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        ActiveModel = DefaultModel;
    }

    public async Task<ColumnSelection?> SuggestColumnsAsync(
        IReadOnlyList<string> headers,
        CancellationToken cancellationToken = default)
    {
        var model = await ResolveModelAsync(cancellationToken) ?? DefaultModel;
        var prompt = $$"""
            Sei un assistente che abbinа intestazioni di fogli Excel (gestione presenze/assenze) ai campi richiesti.
            Restituisci SOLO un oggetto JSON con esattamente queste chiavi:
            employee, policy, startDate, endDate, duration.
            Per ogni chiave scrivi il testo ESATTO (copia-incolla) di una delle intestazioni elencate sotto.
            Regole:
            - employee = colonna col nominativo della persona (es. "Display Name", "Nome dipendente").
            - policy = colonna della causale/giustificativo (es. "Policy", "Causale").
            - startDate = colonna della data di inizio (es. "Start Date", "Data inizio").
            - endDate = colonna della data di fine (es. "End Date", "Data fine"). Se manca, ripeti startDate.
            - duration = colonna con la durata in ore (es. "Duration in Range", "Ore").
            - NON usare la colonna dei codici dipendente per "employee".
            - Se un campo non è riconoscibile usa una stringa vuota "".
            - Non inventare colonne e non restituire altro testo.

            Esempio:
            Intestazioni: ["Employee Code","Nome","Causale","Dal","Al","Ore totali"]
            Risposta: {"employee":"Nome","policy":"Causale","startDate":"Dal","endDate":"Al","duration":"Ore totali"}

            Intestazioni: {{System.Text.Json.JsonSerializer.Serialize(headers)}}
            Risposta:
            """;
        using var response = await Client.PostAsJsonAsync(
            "api/generate",
            new
            {
                model,
                prompt,
                stream = false,
                format = "json",
                options = new { temperature = 0, top_p = 0.1, num_predict = 200 }
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var raw = document.RootElement.GetProperty("response").GetString() ?? "{}";
        using var suggested = JsonDocument.Parse(raw);
        var root = suggested.RootElement;

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
        if (indexes.Any(index => index < 0) || indexes.Distinct().Count() != indexes.Length)
        {
            return null;
        }
        return new ColumnSelection(indexes[0], indexes[1], indexes[2], indexes[3], indexes[4]);
    }

    public async Task<Dictionary<string, string>> SuggestPolicyCodesAsync(
        IReadOnlyList<string> policies,
        IReadOnlyDictionary<string, string> knownMappings,
        CancellationToken cancellationToken = default)
    {
        if (knownMappings.Count == 0 || policies.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var model = await ResolveModelAsync(cancellationToken) ?? DefaultModel;
        var prompt = $$"""
            Sei un assistente che classifica giustificativi di assenza aziendali (ferie, permessi, malattia...).
            Per ciascuna NUOVA etichetta, scegli un codice SOLO se esiste una corrispondenza semantica chiara
            con una delle associazioni già configurate qui sotto. In caso di dubbio restituisci stringa vuota.
            Non inventare MAI codici: devi riusare esattamente i codici configurati.
            Restituisci SOLO un oggetto JSON: chiave = etichetta sorgente esatta, valore = codice configurato (o "" se nessuna corrispondenza).

            Esempio:
            Configurate: {"Ferie":"01","Permesso retribuito":"02","Malattia":"03"}
            Nuove: ["Ferie retribuite","Visita medica"]
            Risposta: {"Ferie retribuite":"01","Visita medica":""}

            Associazioni configurate: {{System.Text.Json.JsonSerializer.Serialize(knownMappings)}}
            Nuove etichette: {{System.Text.Json.JsonSerializer.Serialize(policies)}}
            Risposta:
            """;
        using var response = await Client.PostAsJsonAsync(
            "api/generate",
            new
            {
                model,
                prompt,
                stream = false,
                format = "json",
                options = new { temperature = 0, top_p = 0.1, num_predict = 300 }
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        using var suggestions = JsonDocument.Parse(document.RootElement.GetProperty("response").GetString() ?? "{}");
        var permittedCodes = knownMappings.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requestedPolicies = policies.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in suggestions.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }
            var code = property.Value.GetString()?.Trim() ?? string.Empty;
            if (requestedPolicies.Contains(property.Name) && permittedCodes.Contains(code) && code.Length <= 3)
            {
                result[property.Name] = code;
            }
        }
        return result;
    }

    private static string ReadField(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
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
        return -1;
    }

    private static string Normalize(string value) =>
        string.Join(" ", value.Trim().ToLowerInvariant()
            .Replace('"', ' ')
            .Replace("'", " ")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
