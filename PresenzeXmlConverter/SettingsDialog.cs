using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>
/// Impostazioni dell'applicazione in un unico posto: regole di conversione
/// (giornata standard, sabati, chiusure aziendali), collegamento alle Release
/// GitHub con il token di sola lettura e modello AI preferito. Il token viene
/// verificato e attivato subito, senza riavviare il programma.
/// </summary>
internal sealed class SettingsDialog : Form
{
    private readonly int _referenceYear;
    private readonly string _installedModelText;
    private readonly RoundedField _hours = new();
    private readonly CheckBox _saturdays = new();
    private readonly TextBox _holidays = new();
    private readonly RoundedField _model = new();
    private readonly CheckBox _autoCheck = new();
    private readonly RoundedField _token = new();
    private readonly Label _tokenStatus = new();
    private readonly Label _errorLabel = new();
    private readonly Button _verifyTokenButton;
    private readonly Button _clearTokenButton;

    public SettingsDialog(
        GenerationOptions options,
        int referenceYear,
        bool autoUpdateCheck,
        string? preferredModel,
        string installedModelText)
    {
        _referenceYear = referenceYear;
        _installedModelText = installedModelText;
        Options = options;
        AutoUpdateCheck = autoUpdateCheck;
        PreferredModel = preferredModel;

        // I due pulsanti del token servono già mentre si costruisce la card
        // degli aggiornamenti: vanno creati prima del layout.
        _verifyTokenButton = Theme.CreateButton("Verifica e salva token", Theme.ButtonTone.Positive, compact: true);
        _clearTokenButton = Theme.CreateButton("Rimuovi token", Theme.ButtonTone.Neutral, compact: true);

        Text = "Impostazioni di PresenzeFlow";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(660, 560);
        ClientSize = new Size(760, 780);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Theme.StyleDialog(this);
        BackColor = Theme.Canvas;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20, 16, 20, 16),
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        Controls.Add(root);

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.Controls.Add(new Label
        {
            Text = "Impostazioni",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Ink,
            Font = Theme.Fonts.CardTitle,
            Margin = Padding.Empty
        }, 0, 0);
        heading.Controls.Add(new Label
        {
            Text = "Regole di conversione, aggiornamenti dalle Release GitHub e modello AI locale.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Small,
            Margin = Padding.Empty
        }, 0, 1);
        root.Controls.Add(heading, 0, 0);

        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scroll.Controls.Add(content);
        root.Controls.Add(scroll, 0, 1);

        content.Controls.Add(BuildConversionCard());
        content.Controls.Add(BuildAiCard());
        content.Controls.Add(BuildUpdateCard());

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _errorLabel.Dock = DockStyle.Fill;
        _errorLabel.TextAlign = ContentAlignment.MiddleLeft;
        _errorLabel.ForeColor = Theme.DangerInk;
        _errorLabel.Font = Theme.Fonts.Small;
        _errorLabel.AutoEllipsis = true;
        _errorLabel.Margin = Padding.Empty;
        footer.Controls.Add(_errorLabel, 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        var save = Theme.CreateButton("Salva impostazioni", Theme.ButtonTone.Primary);
        save.Size = new Size(190, 40);
        save.Margin = new Padding(8, 0, 0, 0);
        save.Click += (_, _) => TryCommit();
        var cancel = Theme.CreateButton("Annulla", Theme.ButtonTone.Neutral);
        cancel.Size = new Size(130, 40);
        cancel.DialogResult = DialogResult.Cancel;
        actions.Controls.Add(save);
        actions.Controls.Add(cancel);
        footer.Controls.Add(actions, 0, 1);
        root.Controls.Add(footer, 0, 2);

        AcceptButton = save;
        CancelButton = cancel;
    }

    /// <summary>Impostazioni di conversione scelte (valide solo dopo "Salva impostazioni").</summary>
    public GenerationOptions Options { get; private set; }

    public bool AutoUpdateCheck { get; private set; }

    public string? PreferredModel { get; private set; }

    /// <summary>True se in questa sessione è stato salvato un token valido.</summary>
    public bool TokenSaved { get; private set; }

    private SurfacePanel BuildConversionCard()
    {
        var card = CreateCard(
            "Conversione",
            "Come vengono distribuite le ore della richiesta sui giorni lavorativi.",
            274);
        var body = (TableLayoutPanel)card.Controls[0]!;

        body.Controls.Add(CreateFieldLabel(
            $"Ore di una giornata standard  (in {_referenceYear} la durata della richiesta viene divisa sui giorni lavorativi)"), 0, 1);
        _hours.MaxLength = 5;
        _hours.Dock = DockStyle.Fill;
        _hours.InputFont = Theme.Fonts.Field;
        _hours.Placeholder = "es. 8  oppure  7,5";
        _hours.Value = Options.HoursPerDay.ToString("0.##", CultureInfo.CurrentCulture);
        _hours.Margin = new Padding(0, 2, 0, 8);
        body.Controls.Add(_hours, 0, 2);

        _saturdays.Text = "Considera lavorativo anche il sabato (le ore vengono distribuite anche di sabato)";
        _saturdays.Dock = DockStyle.Fill;
        _saturdays.TextAlign = ContentAlignment.MiddleLeft;
        _saturdays.ForeColor = Theme.InkSecondary;
        _saturdays.Font = Theme.Fonts.Small;
        _saturdays.Checked = Options.IncludeSaturdays;
        _saturdays.Margin = Padding.Empty;
        body.Controls.Add(_saturdays, 0, 3);

        body.Controls.Add(CreateFieldLabel(
            "Chiusure aziendali aggiuntive (oltre alle festività nazionali): una data per riga o separate da virgola — es. 02/06, 24/12"), 0, 4);
        _holidays.Dock = DockStyle.Fill;
        _holidays.Multiline = true;
        _holidays.ScrollBars = ScrollBars.Vertical;
        _holidays.BorderStyle = BorderStyle.None;
        _holidays.BackColor = Theme.FieldFill;
        _holidays.ForeColor = Theme.Ink;
        _holidays.Font = Theme.Fonts.Small;
        _holidays.Text = GenerationSettings.FormatHolidays(Options.ExtraHolidays, _referenceYear)
            .Replace(", ", Environment.NewLine);
        _holidays.Margin = Padding.Empty;
        // Cornice temizzata al posto del bordo grigio di sistema della casella.
        var holidaysFrame = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.FieldFill,
            SurfaceBorder = Theme.DividerStrong,
            Radius = 8,
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(0, 2, 0, 6)
        };
        holidaysFrame.Controls.Add(_holidays);
        body.Controls.Add(holidaysFrame, 0, 5);

        body.Controls.Add(new Label
        {
            Text = "Le festività nazionali e le domeniche sono sempre escluse. Un giorno di chiusura qui indicato non riceve ore.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.FaintInk,
            Font = Theme.Fonts.Tiny,
            AutoEllipsis = true,
            Margin = Padding.Empty
        }, 0, 6);
        return card;
    }

    private SurfacePanel BuildAiCard()
    {
        var card = CreateCard(
            "AI locale (facoltativa)",
            "L'AI gestisce solo suggerimenti: l'XML viene sempre validato e scritto dal programma.",
            150);
        var body = (TableLayoutPanel)card.Controls[0]!;

        body.Controls.Add(CreateFieldLabel(
            "Modello preferito (tag Ollama completo, es. qwen2.5:3b). Vuoto = scelta automatica."), 0, 1);
        _model.MaxLength = 120;
        _model.Dock = DockStyle.Fill;
        _model.InputFont = Theme.Fonts.Field;
        _model.Placeholder = LocalAiAssistant.DefaultModel;
        _model.Value = PreferredModel ?? string.Empty;
        _model.Margin = new Padding(0, 2, 0, 6);
        body.Controls.Add(_model, 0, 2);

        body.Controls.Add(new Label
        {
            Text = _installedModelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Tiny,
            AutoEllipsis = true,
            Margin = Padding.Empty
        }, 0, 3);
        return card;
    }

    private SurfacePanel BuildUpdateCard()
    {
        var card = CreateCard(
            "Aggiornamenti dalle Release GitHub",
            "PresenzeFlow legge le Release del repository: la repo è privata, quindi serve un token di sola lettura.",
            316);
        var body = (TableLayoutPanel)card.Controls[0]!;

        _autoCheck.Text = "Cerca aggiornamenti automaticamente all'avvio e durante l'uso";
        _autoCheck.Dock = DockStyle.Fill;
        _autoCheck.TextAlign = ContentAlignment.MiddleLeft;
        _autoCheck.ForeColor = Theme.InkSecondary;
        _autoCheck.Font = Theme.Fonts.Small;
        _autoCheck.Checked = AutoUpdateCheck;
        _autoCheck.Margin = Padding.Empty;
        body.Controls.Add(_autoCheck, 0, 1);

        body.Controls.Add(CreateFieldLabel(
            "Token GitHub (fine-grained, permesso Contents → Read-only sul repository). Solo lettere e numeri: non viene mai salvato nell'eseguibile."), 0, 2);
        _token.MaxLength = 400;
        _token.Dock = DockStyle.Fill;
        _token.InputFont = Theme.Fonts.Field;
        _token.Placeholder = "github_pat_… oppure ghp_…";
        _token.Input.UseSystemPasswordChar = true;
        _token.Input.Text = UpdateService.ResolveToken() ?? string.Empty;
        _token.Margin = new Padding(0, 2, 0, 6);
        body.Controls.Add(_token, 0, 3);

        var tokenActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        _verifyTokenButton.Size = new Size(180, 34);
        _verifyTokenButton.Margin = new Padding(0, 0, 8, 0);
        _verifyTokenButton.Click += async (_, _) => await VerifyTokenAsync();
        _clearTokenButton.Size = new Size(140, 34);
        _clearTokenButton.Margin = Padding.Empty;
        _clearTokenButton.Click += (_, _) => ClearToken();
        tokenActions.Controls.Add(_verifyTokenButton);
        tokenActions.Controls.Add(_clearTokenButton);
        body.Controls.Add(tokenActions, 0, 4);

        _tokenStatus.Dock = DockStyle.Fill;
        _tokenStatus.TextAlign = ContentAlignment.TopLeft;
        _tokenStatus.Font = Theme.Fonts.Small;
        _tokenStatus.AutoEllipsis = true;
        _tokenStatus.Margin = new Padding(0, 4, 0, 0);
        SetTokenStatus(
            $"Stato attuale: {UpdateService.DescribeTokenSource()}. Il file del token è {UpdateService.TokenFilePath}",
            Theme.MutedInk);
        body.Controls.Add(_tokenStatus, 0, 5);

        body.Controls.Add(new Label
        {
            Text = "Il token serve solo a leggere le Release e scaricare l'installer: senza di esso l'app resta funzionante ma non riceve aggiornamenti.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.FaintInk,
            Font = Theme.Fonts.Tiny,
            AutoEllipsis = true,
            Margin = Padding.Empty
        }, 0, 6);
        return card;
    }

    /// <summary>Card con titolo, sottotitolo e corpo a righe fisse.</summary>
    private static SurfacePanel CreateCard(string title, string subtitle, int height)
    {
        var card = new SurfacePanel
        {
            Dock = DockStyle.Top,
            Height = height,
            SurfaceFill = Theme.Surface,
            SurfaceBorder = Theme.Divider,
            Radius = 12,
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(0, 0, 0, 12)
        };
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Ink,
            Font = Theme.Fonts.SmallBold,
            Margin = Padding.Empty
        }, 0, 0);
        heading.Controls.Add(new Label
        {
            Text = subtitle,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Tiny,
            AutoEllipsis = true,
            Margin = Padding.Empty
        }, 0, 1);
        body.Controls.Add(heading, 0, 0);
        card.Controls.Add(body);
        return card;
    }

    private static Label CreateFieldLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Theme.MutedInk,
        Font = Theme.Fonts.Tiny,
        AutoEllipsis = true,
        Margin = Padding.Empty
    };

    private void SetTokenStatus(string text, Color color)
    {
        _tokenStatus.Text = text;
        _tokenStatus.ForeColor = color;
    }

    /// <summary>Verifica il token su GitHub e, se valido, lo salva e lo attiva subito.</summary>
    private async Task VerifyTokenAsync()
    {
        var candidate = UpdateService.SanitizeToken(_token.Value);
        if (candidate.Length == 0)
        {
            SetTokenStatus("Inserisci prima il token nel campo qui sopra.", Theme.DangerInk);
            _token.FocusInput();
            return;
        }

        _verifyTokenButton.Enabled = false;
        SetTokenStatus("Verifica in corso su GitHub…", Theme.MutedInk);
        try
        {
            var result = await UpdateService.ValidateTokenAsync(candidate);
            if (!result.Succeeded)
            {
                SetTokenStatus(result.Message, Theme.DangerInk);
                return;
            }
            if (!UpdateService.TrySaveToken(candidate, out var error))
            {
                SetTokenStatus(error, Theme.DangerInk);
                return;
            }
            TokenSaved = true;
            _token.Value = candidate;
            SetTokenStatus(
                $"Token verificato, salvato e attivo da subito. {result.Message}",
                Theme.SuccessInk);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            SetTokenStatus($"Verifica non riuscita: {exception.Message}", Theme.DangerInk);
        }
        finally
        {
            _verifyTokenButton.Enabled = true;
        }
    }

    private void ClearToken()
    {
        if (!UpdateService.TryClearToken(out var error))
        {
            SetTokenStatus(error, Theme.DangerInk);
            return;
        }
        _token.Clear();
        TokenSaved = true;
        SetTokenStatus(
            "Token rimosso: gli aggiornamenti automatici resteranno disattivati finché non ne inserisci uno nuovo.",
            Theme.WarningInk);
    }

    /// <summary>Valida i valori e chiude la finestra solo se sono utilizzabili.</summary>
    private void TryCommit()
    {
        if (!GenerationSettings.TryParseHoursPerDay(_hours.Value, out var hours))
        {
            _errorLabel.Text =
                $"Le ore di una giornata devono essere un numero fra {GenerationSettings.MinimumHoursPerDay:0.##} e " +
                $"{GenerationSettings.MaximumHoursPerDay:0.##} (es. 8 oppure 7,5).";
            _hours.FocusInput();
            return;
        }

        var holidays = GenerationSettings.ParseHolidaysDetailed(
            _holidays.Text,
            _referenceYear,
            out var ignored);
        if (ignored.Count > 0)
        {
            _errorLabel.Text =
                $"Date non riconosciute e ignorate: {string.Join(", ", ignored.Take(4))}. " +
                "Usa il formato gg/mm oppure gg/mm/aaaa.";
            return;
        }

        var model = _model.Value.Trim();
        Options = new GenerationOptions(hours, _saturdays.Checked, holidays);
        AutoUpdateCheck = _autoCheck.Checked;
        PreferredModel = model.Length == 0 ? null : model;
        DialogResult = DialogResult.OK;
        Close();
    }
}
