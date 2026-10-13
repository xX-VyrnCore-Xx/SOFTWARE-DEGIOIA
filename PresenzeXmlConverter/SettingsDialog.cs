using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>
/// Impostazioni dell'applicazione in un unico posto, divise in tre schede:
/// Conversione (regole di calcolo) e Aggiornamenti (collegamento alle
/// Release GitHub con il token di sola lettura). Il token viene verificato e
/// attivato subito, senza riavviare il programma.
///
/// Le schede tengono ogni gruppo di campi entro l'altezza della finestra, così
/// il dialogo non ha bisogno di scorrere: la barra di scorrimento nativa di
/// Windows resta grigia anche in una finestra dipinta con il tema dell'app.
/// Le altezze delle righe stanno in <see cref="SettingsPageLayout"/> perché una
/// scheda più bassa delle righe che contiene schiaccia i campi e taglia l'ultima
/// riga di testo (già successo con una scheda troppo bassa).
/// </summary>
internal sealed class SettingsDialog : Form
{
    private readonly int _referenceYear;
    private readonly RoundedField _hours = new();
    private readonly CheckBox _saturdays = new();
    private readonly TextBox _holidays = new();
    private readonly CheckBox _autoDetect = new();
    private readonly CheckBox _autoCheck = new();
    private readonly RoundedField _token = new();
    private readonly Label _tokenStatus = new();
    private readonly Label _errorLabel = new();
    private readonly Button _verifyTokenButton;
    private readonly Button _clearTokenButton;
    private readonly RoundedField _xmlRoot = new();
    private readonly RoundedField _xmlNamespace = new();
    private readonly CheckBox _xmlPerNode = new();
    private readonly Label _xmlStatus = new();

    public SettingsDialog(
        GenerationOptions options,
        int referenceYear,
        bool autoUpdateCheck,
        XmlFormat? xmlFormat = null)
    {
        _referenceYear = referenceYear;
        Options = options;
        AutoUpdateCheck = autoUpdateCheck;
        Format = xmlFormat ?? XmlFormat.Active;

        // I due pulsanti del token servono già mentre si costruisce la scheda
        // degli aggiornamenti: vanno creati prima del layout.
        _verifyTokenButton = Theme.CreateButton("Verifica e salva token", Theme.ButtonTone.Positive, compact: true);
        _verifyTokenButton.AccessibleName = "Verifica e salva il token GitHub";
        _clearTokenButton = Theme.CreateButton("Rimuovi token", Theme.ButtonTone.Neutral, compact: true);
        _clearTokenButton.AccessibleName = "Rimuovi il token GitHub";

        Text = "Impostazioni di Vyrn Presence";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(SettingsPageLayout.MinimumWindowWidth, SettingsPageLayout.MinimumWindowHeight);
        ClientSize = new Size(SettingsPageLayout.DefaultClientWidth, SettingsPageLayout.DefaultClientHeight);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Theme.StyleDialog(this);
        BackColor = Theme.Canvas;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20, SettingsPageLayout.WindowPadding / 2, 20, SettingsPageLayout.WindowPadding / 2),
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsPageLayout.HeaderRow));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsPageLayout.FooterRow));
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
        // Titolo (34 px: il font dei titoli è alto una trentina di pixel) e
        // sottotitolo su una riga sola, che resta dentro la riga dichiarata.
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
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
            Text = $"Vyrn Presence {ExecutableSignature.DisplayVersion}  ·  regole di conversione e aggiornamenti.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Small,
            Margin = Padding.Empty
        }, 0, 1);
        root.Controls.Add(heading, 0, 0);

        // Card bianca con le schede: stesso schema della finestra principale, così
        // l'intestazione della card e la scheda attiva non mostrano colori di sistema.
        var shell = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.Surface,
            SurfaceBorder = Theme.Divider,
            Radius = 12,
            Padding = new Padding(16, SettingsPageLayout.ShellPadding / 2, 16, SettingsPageLayout.ShellPadding / 2),
            Margin = Padding.Empty
        };
        var tabs = new ThemedTabControl { Dock = DockStyle.Fill, Margin = Padding.Empty };
        tabs.AddPage("Conversione", BuildConversionPage());
        tabs.AddPage("Aggiornamenti", BuildUpdatePage());
        tabs.AddPage("Formato XML", BuildXmlPage());
        tabs.SelectedIndex = 0;
        shell.Controls.Add(tabs);
        root.Controls.Add(shell, 0, 1);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Messaggio di errore e pulsanti: le due righe sommano esattamente il piè
        // di pagina dichiarato in SettingsPageLayout (prima i pulsanti da 40 px
        // finivano in 30 px e il bordo inferiore veniva tagliato).
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsPageLayout.FooterMessageRow));
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
        save.Size = new Size(190, SettingsPageLayout.ActionButtonHeight);
        save.Margin = new Padding(8, 0, 0, 0);
        save.Click += (_, _) => TryCommit();
        save.AccessibleName = "Salva impostazioni";
        save.AccessibleDescription = "Applica e chiude la finestra delle impostazioni.";
        var cancel = Theme.CreateButton("Annulla", Theme.ButtonTone.Neutral);
        cancel.Size = new Size(130, SettingsPageLayout.ActionButtonHeight);
        cancel.DialogResult = DialogResult.Cancel;
        cancel.AccessibleName = "Annulla";
        cancel.AccessibleDescription = "Chiude senza salvare le impostazioni.";
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

    /// <summary>Forma del file XML scelta (valida solo dopo "Salva impostazioni").</summary>
    public XmlFormat Format { get; private set; }

    /// <summary>True se in questa sessione è stato salvato un token valido.</summary>
    public bool TokenSaved { get; private set; }

    /// <summary>
    /// Pagina di una scheda: le righe sono quelle dichiarate in
    /// <see cref="SettingsPageLayout"/>, quindi l'altezza della scheda e quella
    /// del suo contenuto non possono separarsi.
    /// </summary>
    private static (Panel Page, TableLayoutPanel Body) CreatePage(IReadOnlyList<int> rows)
    {
        var page = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = rows.Count,
            BackColor = Theme.Surface,
            Padding = new Padding(0, SettingsPageLayout.PagePadding / 2, 0, SettingsPageLayout.PagePadding / 2),
            Margin = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in rows)
        {
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        }
        page.Controls.Add(body);
        return (page, body);
    }

    /// <summary>Scheda "Conversione": giornata standard, sabato e chiusure aziendali.</summary>
    private Control BuildConversionPage()
    {
        var (page, body) = CreatePage(SettingsPageLayout.ConversionRows);

        body.Controls.Add(CreateHint(
            "Come vengono distribuite le ore della richiesta sui giorni lavorativi."), 0, 0);

        body.Controls.Add(CreateFieldLabel(
            $"Ore di una giornata standard  (in {_referenceYear} la durata della richiesta viene divisa sui giorni lavorativi)"), 0, 1);
        _hours.MaxLength = 5;
        _hours.Dock = DockStyle.Fill;
        _hours.InputFont = Theme.Fonts.Field;
        _hours.Placeholder = "es. 8  oppure  7,5";
        _hours.Value = Options.HoursPerDay.ToString("0.##", CultureInfo.CurrentCulture);
        _hours.Input.AccessibleName = "Ore di una giornata standard";
        _hours.Margin = new Padding(0, 2, 0, 8);
        body.Controls.Add(_hours, 0, 2);

        _saturdays.Text = "Il sabato è sempre lavorativo (le ore vengono distribuite anche di sabato)";
        _saturdays.Dock = DockStyle.Fill;
        _saturdays.TextAlign = ContentAlignment.MiddleLeft;
        _saturdays.ForeColor = Theme.InkSecondary;
        _saturdays.Font = Theme.Fonts.Small;
        _saturdays.Checked = Options.IncludeSaturdays;
        _saturdays.AccessibleName = "Considera lavorativo il sabato";
        _saturdays.Margin = Padding.Empty;
        body.Controls.Add(_saturdays, 0, 3);

        _autoDetect.Text = "Rileva in automatico sabati e ore dalle date (consigliato)";
        _autoDetect.Dock = DockStyle.Fill;
        _autoDetect.TextAlign = ContentAlignment.MiddleLeft;
        _autoDetect.ForeColor = Theme.InkSecondary;
        _autoDetect.Font = Theme.Fonts.Small;
        _autoDetect.Checked = Options.AutoDetect;
        _autoDetect.AccessibleName = "Rileva automaticamente sabati e ore";
        _autoDetect.Margin = Padding.Empty;
        body.Controls.Add(_autoDetect, 0, 4);

        body.Controls.Add(CreateFieldLabel(
            "Chiusure aziendali aggiuntive (oltre alle festività nazionali): una data per riga o separate da virgola — es. 02/06, 24/12"), 0, 5);
        _holidays.Dock = DockStyle.Fill;
        _holidays.Multiline = true;
        _holidays.ScrollBars = ScrollBars.None;
        _holidays.BorderStyle = BorderStyle.None;
        _holidays.BackColor = Theme.FieldFill;
        _holidays.ForeColor = Theme.Ink;
        _holidays.Font = Theme.Fonts.Small;
        _holidays.Text = GenerationSettings.FormatHolidays(Options.ExtraHolidays, _referenceYear)
            .Replace(", ", Environment.NewLine);
        _holidays.AccessibleName = "Chiusure aziendali aggiuntive";
        _holidays.Margin = Padding.Empty;
        // Cornice temizzata al posto del bordo grigio di sistema della casella. La
        // barra di scorrimento nativa resterebbe grigia anche qui: il campo mostra
        // quattro righe e la rotellina e i tasti continuano a scorrere il testo.
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
        body.Controls.Add(holidaysFrame, 0, 6);

        body.Controls.Add(CreateNote(
            "Le festività nazionali e le domeniche sono sempre escluse. Un giorno di chiusura qui indicato non riceve ore."), 0, 7);
        return page;
    }

    /// <summary>
    /// Scheda "Formato XML": il tracciato non dice come si chiama l'elemento radice né se
    /// c'è uno spazio dei nomi, ma il validatore di Zucchetti li pretende. Si possono
    /// impostare a mano o leggere da un file XML che Zucchetti accetta.
    /// </summary>
    private Control BuildXmlPage()
    {
        var (page, body) = CreatePage(SettingsPageLayout.XmlRows);

        body.Controls.Add(CreateHint(
            "Impostazioni avanzate: servono solo se Zucchetti respinge il file con «cvc-elt.1: impossibile trovare la dichiarazione dell'elemento…»."), 0, 0);

        body.Controls.Add(CreateFieldLabel("Nome dell'elemento radice del file XML (la prima riga dopo <?xml …?>)"), 0, 1);
        _xmlRoot.MaxLength = 64;
        _xmlRoot.Dock = DockStyle.Fill;
        _xmlRoot.InputFont = Theme.Fonts.Field;
        _xmlRoot.Placeholder = "es. Dipendenti";
        _xmlRoot.Value = Format.RootElement;
        _xmlRoot.Input.AccessibleName = "Nome dell'elemento radice";
        _xmlRoot.Margin = new Padding(0, 2, 0, 8);
        body.Controls.Add(_xmlRoot, 0, 2);

        body.Controls.Add(CreateFieldLabel("Spazio dei nomi (xmlns) dell'elemento radice: lascia vuoto se il file non ne ha"), 0, 3);
        _xmlNamespace.MaxLength = 300;
        _xmlNamespace.Dock = DockStyle.Fill;
        _xmlNamespace.InputFont = Theme.Fonts.Small;
        _xmlNamespace.Placeholder = "es. http://www.esempio.it/schema";
        _xmlNamespace.Value = Format.Namespace;
        _xmlNamespace.Input.AccessibleName = "Spazio dei nomi del file XML";
        _xmlNamespace.Margin = new Padding(0, 2, 0, 8);
        body.Controls.Add(_xmlNamespace, 0, 4);

        _xmlPerNode.Text = "Ogni movimento è un nodo «Movimenti» a sé (senza il nodo «Movimento» dentro)";
        _xmlPerNode.Dock = DockStyle.Fill;
        _xmlPerNode.TextAlign = ContentAlignment.MiddleLeft;
        _xmlPerNode.ForeColor = Theme.InkSecondary;
        _xmlPerNode.Font = Theme.Fonts.Small;
        _xmlPerNode.Checked = Format.MovementPerNode;
        _xmlPerNode.AccessibleName = "Un nodo Movimenti per ogni movimento";
        _xmlPerNode.Margin = Padding.Empty;
        body.Controls.Add(_xmlPerNode, 0, 5);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        var detect = Theme.CreateButton("Leggi da un XML di esempio…", Theme.ButtonTone.Neutral, compact: true);
        detect.Size = new Size(260, 34);
        detect.Margin = Padding.Empty;
        detect.AccessibleName = "Leggi la forma da un XML di esempio";
        detect.AccessibleDescription = "Sceglie un file XML accettato da Zucchetti e ne copia radice, spazio dei nomi e disposizione dei movimenti.";
        detect.Click += (_, _) => DetectFormatFromSample();
        actions.Controls.Add(detect);
        body.Controls.Add(actions, 0, 6);

        _xmlStatus.Dock = DockStyle.Fill;
        _xmlStatus.TextAlign = ContentAlignment.MiddleLeft;
        _xmlStatus.ForeColor = Theme.MutedInk;
        _xmlStatus.Font = Theme.Fonts.Tiny;
        _xmlStatus.AutoEllipsis = true;
        _xmlStatus.Margin = Padding.Empty;
        _xmlStatus.Text = "Con un file XML che Zucchetti accetta (anche di un altro programma) la forma si copia senza indovinare.";
        body.Controls.Add(_xmlStatus, 0, 7);
        return page;
    }

    private void DetectFormatFromSample()
    {
        using var picker = new OpenFileDialog
        {
            Title = "Scegli un file XML accettato da Zucchetti",
            Filter = "File XML (*.xml)|*.xml|Tutti i file (*.*)|*.*",
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        if (!XmlFormat.TryDetect(picker.FileName, out var detected, out var message))
        {
            _xmlStatus.ForeColor = Theme.DangerInk;
            _xmlStatus.Text = message;
            return;
        }
        _xmlRoot.Value = detected.RootElement;
        _xmlNamespace.Value = detected.Namespace;
        _xmlPerNode.Checked = detected.MovementPerNode;
        _xmlStatus.ForeColor = Theme.SuccessInk;
        _xmlStatus.Text = message + " Premi «Salva impostazioni» per usarla.";
    }

    /// <summary>Scheda "Aggiornamenti": ricerca automatica, token delle Release, firma.</summary>
    private Control BuildUpdatePage()
    {
        var (page, body) = CreatePage(SettingsPageLayout.UpdateRows);

        body.Controls.Add(CreateHint(
            "Le Release del repository privato si leggono con un token di sola lettura, già incluso nelle versioni ufficiali."), 0, 0);

        _autoCheck.Text = "Cerca aggiornamenti automaticamente all'avvio e durante l'uso";
        _autoCheck.Dock = DockStyle.Fill;
        _autoCheck.TextAlign = ContentAlignment.MiddleLeft;
        _autoCheck.ForeColor = Theme.InkSecondary;
        _autoCheck.Font = Theme.Fonts.Small;
        _autoCheck.Checked = AutoUpdateCheck;
        _autoCheck.AccessibleName = "Cerca aggiornamenti automaticamente";
        _autoCheck.Margin = Padding.Empty;
        body.Controls.Add(_autoCheck, 0, 1);

        body.Controls.Add(CreateFieldLabel(
            "Token personale (facoltativo, se quello incluso è scaduto). Fine-grained, permesso Contents → Read-only."), 0, 2);
        _token.MaxLength = 400;
        _token.Dock = DockStyle.Fill;
        _token.InputFont = Theme.Fonts.Field;
        _token.Placeholder = "github_pat_… oppure ghp_…";
        _token.Input.UseSystemPasswordChar = true;
        _token.Input.Text = UpdateService.ResolveUserToken() ?? string.Empty;
        _token.Input.AccessibleName = "Token GitHub delle Release";
        _token.Margin = new Padding(0, 2, 0, 8);
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

        body.Controls.Add(CreateNote(
            "Il token serve solo a leggere le Release. Se uno non vale, l'app prova gli altri (personale, file, incluso)."), 0, 6);

        // La firma dell'eseguibile riguarda l'aggiornamento (cioè se il file in uso
        // è quello pubblicato): sta qui, accanto alla versione delle Release.
        body.Controls.Add(CreateNote(ExecutableSignature.DescribeCurrentExecutable()), 0, 7);
        return page;
    }

    /// <summary>Sottotitolo della scheda: una frase, fino a due righe.</summary>
    private static Label CreateHint(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.TopLeft,
        ForeColor = Theme.MutedInk,
        Font = Theme.Fonts.Tiny,
        AutoEllipsis = true,
        Margin = Padding.Empty
    };

    /// <summary>Testo guida di un campo.</summary>
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

    /// <summary>Riga informativa in fondo alla scheda.</summary>
    private static Label CreateNote(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Theme.FaintInk,
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
            EmbeddedUpdateToken.IsPresent
                ? "Token personale rimosso: gli aggiornamenti continuano con il token incluso nell'app."
                : "Token rimosso: gli aggiornamenti automatici resteranno disattivati finché non ne inserisci uno nuovo.",
            EmbeddedUpdateToken.IsPresent ? Theme.MutedInk : Theme.WarningInk);
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

        if (!XmlFormat.IsValidElementName(_xmlRoot.Value))
        {
            _errorLabel.Text = "Il nome dell'elemento radice non è valido: usa lettere, cifre, trattino o underscore, senza spazi.";
            _xmlRoot.FocusInput();
            return;
        }
        if (!XmlFormat.IsValidNamespace(_xmlNamespace.Value))
        {
            _errorLabel.Text = "Lo spazio dei nomi deve essere un indirizzo completo (es. http://…) oppure vuoto.";
            _xmlNamespace.FocusInput();
            return;
        }
        Format = new XmlFormat(_xmlRoot.Value.Trim(), _xmlNamespace.Value.Trim(), _xmlPerNode.Checked);
        Options = new GenerationOptions(hours, _saturdays.Checked, holidays, _autoDetect.Checked);
        AutoUpdateCheck = _autoCheck.Checked;
        DialogResult = DialogResult.OK;
        Close();
    }
}
