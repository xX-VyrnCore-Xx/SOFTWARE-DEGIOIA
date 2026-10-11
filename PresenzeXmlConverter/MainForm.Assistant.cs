using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace PresenzeXmlConverter;

/// <summary>
/// Assistente AI conversazionale integrato: l'utente descrive a parole la
/// correzione desiderata e il programma la traduce in modifiche controllate
/// sull'anteprima (mai direttamente sull'XML). La conversazione viene salvata
/// nel database locale, quindi resta disponibile ai riavvii successivi.
/// </summary>
internal sealed partial class MainForm
{
    private const string ChatHistorySetting = "ai-chat-history";
    private const int MaximumStoredMessages = 60;

    private readonly RichTextBox _chatTranscript = new();
    private readonly RoundedField _chatInput = new();
    private readonly Button _chatSendButton = new();
    private readonly Button _chatClearButton = new();
    private readonly Label _chatStateLabel = new();
    private readonly List<AiChatMessage> _chatHistory = new();
    private bool _chatBusy;

    private Control BuildAiChatPanel()
    {
        var page = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.Surface,
            SurfaceBorder = Theme.Divider,
            Radius = 10,
            Padding = new Padding(12),
            Margin = Padding.Empty
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 122));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _chatStateLabel.Text = "Parla con l'AI locale per correggere i movimenti";
        _chatStateLabel.Dock = DockStyle.Fill;
        _chatStateLabel.TextAlign = ContentAlignment.MiddleLeft;
        _chatStateLabel.AutoEllipsis = true;
        _chatStateLabel.Font = Theme.Fonts.SmallBold;
        _chatStateLabel.ForeColor = Theme.InkSecondary;
        _chatStateLabel.Margin = Padding.Empty;
        header.Controls.Add(_chatStateLabel, 0, 0);

        _chatClearButton.Text = "Nuova chat";
        Theme.StyleButton(_chatClearButton, Theme.ButtonTone.Neutral, compact: true);
        _chatClearButton.Dock = DockStyle.Fill;
        _chatClearButton.Margin = new Padding(0, 6, 0, 6);
        _chatClearButton.Click += (_, _) => ClearChat();
        header.Controls.Add(_chatClearButton, 1, 0);
        layout.Controls.Add(header, 0, 0);

        _chatTranscript.Dock = DockStyle.Fill;
        _chatTranscript.ReadOnly = true;
        _chatTranscript.BorderStyle = BorderStyle.None;
        _chatTranscript.BackColor = Theme.FieldFill;
        _chatTranscript.ForeColor = Theme.Ink;
        _chatTranscript.Font = Theme.Fonts.Small;
        _chatTranscript.WordWrap = true;
        _chatTranscript.DetectUrls = false;
        _chatTranscript.ScrollBars = RichTextBoxScrollBars.Vertical;
        _chatTranscript.Margin = new Padding(0, 0, 0, 8);
        layout.Controls.Add(_chatTranscript, 0, 1);

        var composer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        composer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        composer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        composer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _chatInput.Dock = DockStyle.Fill;
        _chatInput.Placeholder = "es. “metti le ferie di Rossi a 4 ore il 17/08”";
        _chatInput.Margin = new Padding(0, 9, 8, 9);
        _chatInput.Input.KeyDown += async (_, args) =>
        {
            if (args.KeyCode == Keys.Enter && !args.Shift)
            {
                args.SuppressKeyPress = true;
                args.Handled = true;
                await SendChatMessageAsync();
            }
        };
        composer.Controls.Add(_chatInput, 0, 0);

        _chatSendButton.Text = "Invia";
        Theme.StyleButton(_chatSendButton, Theme.ButtonTone.Primary, compact: true);
        _chatSendButton.Dock = DockStyle.Fill;
        _chatSendButton.Margin = new Padding(0, 9, 0, 9);
        _chatSendButton.Click += async (_, _) => await SendChatMessageAsync();
        composer.Controls.Add(_chatSendButton, 1, 0);
        layout.Controls.Add(composer, 0, 2);

        page.Controls.Add(layout);
        return page;
    }

    private void LoadChatHistory()
    {
        try
        {
            var raw = _database.GetSetting(ChatHistorySetting);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }
            var stored = JsonSerializer.Deserialize<List<AiChatMessage>>(raw);
            if (stored is null)
            {
                return;
            }
            _chatHistory.AddRange(stored.TakeLast(MaximumStoredMessages));
            foreach (var message in _chatHistory)
            {
                AppendChatLine(
                    message.IsUser ? "Tu" : "Assistente",
                    message.Text,
                    message.IsUser ? Theme.BrandInk : Theme.Ink);
            }
            _chatStateLabel.Text = $"Conversazione ripristinata ({_chatHistory.Count} messaggi)";
        }
        catch (JsonException)
        {
            // Cronologia non leggibile: si riparte da una chat vuota.
        }
    }

    private void PersistChatHistory()
    {
        try
        {
            var payload = JsonSerializer.Serialize(_chatHistory.TakeLast(MaximumStoredMessages).ToArray());
            _database.SaveSetting(ChatHistorySetting, payload);
        }
        catch (InvalidOperationException)
        {
            // Il salvataggio della cronologia non deve mai bloccare la chat.
        }
    }

    private void ClearChat()
    {
        _chatHistory.Clear();
        _chatTranscript.Clear();
        PersistChatHistory();
        ShowChatWelcome();
        _chatStateLabel.Text = "Nuova conversazione";
    }

    private void ShowChatWelcome()
    {
        if (_chatTranscript.TextLength > 0)
        {
            return;
        }
        AppendChatLine(
            "Assistente",
            "Ciao! Dopo aver generato l'anteprima posso correggere i movimenti per te: " +
            "chiedi ad esempio “sposta di un giorno le ferie di Mario Rossi” oppure " +
            "“dimezza le ore del 20 agosto”. Ogni modifica viene applicata solo dopo la tua richiesta, " +
            "con i codici già configurati. Tutto resta sul tuo PC.",
            Theme.Ink);
    }

    private async Task SendChatMessageAsync()
    {
        if (_chatBusy)
        {
            return;
        }
        var text = _chatInput.Value.Trim();
        if (text.Length == 0)
        {
            return;
        }

        _chatInput.Clear();
        AppendChatLine("Tu", text, Theme.BrandInk);
        _chatHistory.Add(new AiChatMessage(true, text));
        PersistChatHistory();
        SetChatBusy(true, "L'AI locale sta elaborando…");
        try
        {
            if (!await _ai.IsAvailableAsync())
            {
                AppendChatLine("Assistente", "L'AI locale non è in esecuzione. Apri “Configura AI locale” e riprova.", Theme.WarningInk);
                return;
            }
            if (!await _ai.HasModelAsync())
            {
                AppendChatLine("Assistente", "Il modello locale non è ancora scaricato: usa “Configura AI locale”.", Theme.WarningInk);
                return;
            }

            var allowedCodes = _database.GetPolicyMappings().Values
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var response = await _ai.ChatAsync(_chatHistory, BuildDocumentSummary(), allowedCodes);
            AppendChatLine("Assistente", response.Reply, Theme.Ink);
            _chatHistory.Add(new AiChatMessage(false, response.Reply));
            PersistChatHistory();

            if (response.Edits.Count == 0)
            {
                _chatStateLabel.Text = "Nessuna modifica proposta";
                return;
            }
            ApplyDocumentEdits(response.Edits);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            AppendChatLine("Assistente", $"Comunicazione con l'AI non riuscita: {exception.Message}", Theme.WarningInk);
        }
        finally
        {
            SetChatBusy(false, null);
        }
    }

    private void ApplyDocumentEdits(IReadOnlyList<DocumentEdit> edits)
    {
        if (_movements.Count == 0)
        {
            AppendChatLine(
                "Assistente",
                "Prima genera l'anteprima dei movimenti: poi potrò correggerli.",
                Theme.WarningInk);
            return;
        }

        var configuredCodes = _database.GetPolicyMappings().Values
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rejected = edits
            .Where(edit => edit.Kind == DocumentEditKind.SetReasonCode &&
                           (edit.ReasonCode is null || !configuredCodes.Contains(edit.ReasonCode)))
            .ToArray();
        var applicable = edits.Except(rejected).ToArray();
        if (rejected.Length > 0)
        {
            AppendChatLine(
                "Assistente",
                $"{rejected.Length} modifiche ignorate: userebbero un codice giustificativo non configurato. " +
                "Configura i codici in “Codici e giustificativi”.",
                Theme.WarningInk);
        }
        if (applicable.Length == 0)
        {
            return;
        }

        var result = DocumentEditor.Apply(_movements, applicable);
        if (result.Applied.Count == 0)
        {
            var reason = result.Skipped.Count > 0 ? string.Join(" ", result.Skipped) : "Nessuna modifica applicabile.";
            AppendChatLine("Assistente", reason, Theme.WarningInk);
            _chatStateLabel.Text = "Nessuna modifica applicata";
            return;
        }

        _movements = result.Movements.ToList();
        RefreshPreviewAfterAiEdits();
        AppendChatLine("Sistema", string.Join(Environment.NewLine, result.Applied), Theme.SuccessInk);
        _chatStateLabel.Text = $"{result.Applied.Count} modifiche applicate all'anteprima";
        SetStatus("●   Anteprima aggiornata dall'assistente AI: ricontrolla prima di esportare.", false);
    }

    private void RefreshPreviewAfterAiEdits()
    {
        if (_movements.Count == 0)
        {
            _xml = null;
            _xmlPreview.Clear();
            _movementsGrid.Rows.Clear();
            _previewTabs.SetPageText(0, "Movimenti  ·  0");
            _previewCountLabel.Text = "Nessun movimento residuo";
            _hasWarnings = false;
            UpdateButtons();
            return;
        }
        PopulatePreview(_movements);
        RefreshXmlFromPreview();
    }

    private string BuildDocumentSummary()
    {
        var builder = new StringBuilder();
        builder.AppendLine(_inspection is null
            ? "Nessun file Excel importato."
            : $"Foglio Excel: “{_inspection.SheetName}” · {_requests.Count} richieste sorgente.");
        builder.AppendLine($"Anteprima: {_movements.Count} movimenti generati.");
        if (_inspection?.PeriodStart is { } start && _inspection.PeriodEnd is { } end)
        {
            builder.AppendLine($"Periodo dichiarato nel foglio: {start:yyyy-MM-dd} → {end:yyyy-MM-dd}");
        }
        builder.Append(DocumentEditor.Summarize(_movements));
        return builder.ToString();
    }

    private void AppendChatLine(string speaker, string text, Color color)
    {
        _chatTranscript.SelectionStart = _chatTranscript.TextLength;
        _chatTranscript.SelectionLength = 0;
        _chatTranscript.SelectionColor = Theme.MutedInk;
        _chatTranscript.SelectionFont = Theme.Fonts.SmallBold;
        _chatTranscript.AppendText($"{speaker}{Environment.NewLine}");
        _chatTranscript.SelectionStart = _chatTranscript.TextLength;
        _chatTranscript.SelectionColor = color;
        _chatTranscript.SelectionFont = Theme.Fonts.Small;
        _chatTranscript.AppendText($"{text}{Environment.NewLine}{Environment.NewLine}");
        _chatTranscript.SelectionStart = _chatTranscript.TextLength;
        _chatTranscript.SelectionLength = 0;
        _chatTranscript.ScrollToCaret();
    }

    private void SetChatBusy(bool busy, string? state)
    {
        _chatBusy = busy;
        _chatSendButton.Enabled = !busy;
        _chatInput.Enabled = !busy;
        _chatStateLabel.ForeColor = busy ? Theme.BrandInk : Theme.InkSecondary;
        if (state is not null)
        {
            _chatStateLabel.Text = state;
        }
    }

    private void FocusAiChat()
    {
        _previewTabs.SelectedIndex = 2;
        _chatInput.FocusInput();
    }
}
