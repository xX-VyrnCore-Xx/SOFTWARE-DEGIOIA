using System.Net.Http.Json;
using System.Text.Json;

namespace PresenzeXmlConverter;

/// <summary>
/// Conversazione con l'AI locale e proposta di modifiche al documento.
/// L'AI non tocca mai direttamente l'XML: restituisce una risposta in testo
/// ed eventuali modifiche strutturate che il programma valida e applica.
/// </summary>
internal sealed partial class LocalAiAssistant
{
    private const string ChatSystemPrompt = """
        Sei l'assistente di PresenzeFlow, un'app Windows che converte richieste di
        assenza Excel nel tracciato XML TRRIPW di Zucchetti. Parli italiano, in modo
        breve e concreto, e aiuti l'utente a controllare e correggere i movimenti
        dell'anteprima (un movimento = un dipendente, una data, un giustificativo,
        ore e minuti).

        Rispondi SEMPRE e SOLO con un oggetto JSON valido con questa forma:
        {
          "reply": "testo della risposta in italiano",
          "edits": [ { ... } ]
        }

        Ogni elemento di "edits" modifica l'anteprima e ha i campi:
          "kind": uno tra "setHours", "shiftDays", "scaleHours", "setReasonCode", "remove".
          "employee": (opzionale) parte del nome o del codice dipendente.
          "policy": (opzionale) parte del giustificativo originale.
          "filterReasonCode": (opzionale) parte del codice giustificativo da filtrare.
          "date": (opzionale) giorno esatto "aaaa-mm-gg".
          "dateFrom", "dateTo": (opzionali) intervallo di giorni inclusivo.
          "hours", "minutes": per "setHours".
          "days": per "shiftDays" (positivo sposta in avanti, negativo indietro).
          "factor": per "scaleHours" (es. 0.5 dimezza, 2 raddoppia).
          "reasonCode": per "setReasonCode" (DEVE essere uno dei codici già configurati).
          "summary": breve descrizione in italiano della modifica.

        Regole rigide:
        - Un filtro senza campi vale "tutti i movimenti": usalo solo se l'utente lo chiede.
        - Non inventare MAI codici giustificativo: "reasonCode" deve appartenere ai codici
          configurati elencati nel contesto. Se non sei sicuro, non proporre la modifica.
        - Non inventare dipendenti o giustificativi: usa solo quelli presenti nel contesto.
        - Se la richiesta è solo una domanda o un riepilogo, restituisci "edits": [].
        - Se ti mancano informazioni per agire, chiedile in "reply" con "edits": [].
        - "reply" è obbligatorio e sempre in italiano, anche quando applichi modifiche.
        """;

    /// <summary>
    /// Invia la conversazione al modello locale insieme a un riepilogo
    /// dell'anteprima corrente e restituisce testo e modifiche validate.
    /// </summary>
    public async Task<AiChatResponse> ChatAsync(
        IReadOnlyList<AiChatMessage> history,
        string documentSummary,
        IReadOnlySet<string> allowedReasonCodes,
        CancellationToken cancellationToken = default)
    {
        var model = await ResolveModelAsync(cancellationToken) ?? DefaultModel;
        var messages = new List<object>
        {
            new { role = "system", content = ChatSystemPrompt },
            new
            {
                role = "system",
                content =
                    "CONTESTO — ANTEPRIMA CORRENTE (solo in locale, non esce dal PC)\n" +
                    documentSummary +
                    "\nCodici giustificativo configurati e utilizzabili: " +
                    (allowedReasonCodes.Count == 0
                        ? "(nessuno: chiedi all'utente di configurarli)"
                        : string.Join(", ", allowedReasonCodes.OrderBy(code => code, StringComparer.OrdinalIgnoreCase)))
            }
        };
        foreach (var message in history.TakeLast(16))
        {
            messages.Add(new
            {
                role = message.IsUser ? "user" : "assistant",
                content = message.Text
            });
        }

        using var response = await Client.PostAsJsonAsync(
            "api/chat",
            new
            {
                model,
                messages,
                stream = false,
                format = "json",
                options = new { temperature = 0, top_p = 0.1, num_predict = 700 }
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var content = document.RootElement.TryGetProperty("message", out var messageElement) &&
                      messageElement.TryGetProperty("content", out var contentElement)
            ? contentElement.GetString() ?? "{}"
            : "{}";

        var reply = DocumentEditParser.ParseReply(content);
        var edits = DocumentEditParser.Parse(content, allowedReasonCodes);
        if (reply.Length == 0)
        {
            reply = edits.Count > 0
                ? $"Ho preparato {edits.Count} modifiche all'anteprima."
                : "Non ho una risposta da darti: riformula la richiesta.";
        }
        return new AiChatResponse(reply, edits);
    }
}
