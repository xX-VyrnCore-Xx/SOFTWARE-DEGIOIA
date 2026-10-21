using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>
/// Impostazioni dell'applicazione in un unico posto, divise in schede:
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
    private readonly ToggleSwitch _saturdays = new();
    private readonly TextBox _holidays = new();
    private readonly ToggleSwitch _autoDetect = new();
    private readonly ToggleSwitch _fillPresence = new();
    private readonly ToggleSwitch _autoCheck = new();
    private readonly ToggleSwitch _startWithWindows = new();
    private readonly RoundedField _token = new();
    private readonly TextBlock _tokenStatus = new() { Wrap = true };
    private readonly TextBlock _errorLabel = new() { Wrap = true };
    private readonly Button _verifyTokenButton;
    private readonly Button _clearTokenButton;
    private readonly RoundedField _xmlRoot = new();
    private readonly RoundedField _xmlNamespace = new();
    private readonly ToggleSwitch _xmlPerNode = new();
    private readonly TextBlock _xmlStatus = new() { Wrap = true };
    private readonly ToggleSwitch _inAppNotifications = new();
    private readonly ToggleSwitch _systemNotifications = new();
    private readonly SegmentedControl _closeChoice = new();
    private readonly SegmentedControl _themeChoice = new();
    private readonly TextBlock _backupStatus = new() { Wrap = true };
    private readonly GeneralInfo _general;

    public SettingsDialog(
        GenerationOptions options,
        int referenceYear,
        bool autoUpdateCheck,
        XmlFormat? xmlFormat = null,
        bool inAppNotifications = true,
        bool systemNotifications = true,
        string closeBehavior = "",
        GeneralInfo? general = null)
    {
        _general = general ?? GeneralInfo.Empty;
        CloseBehavior = closeBehavior is "tray" or "exit" ? closeBehavior : string.Empty;
        _referenceYear = referenceYear;
        Options = options;
        AutoUpdateCheck = autoUpdateCheck;
        InAppNotifications = inAppNotifications;
        SystemNotifications = systemNotifications;
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
        UiScale.GuardAutoScale(this);
        Theme.StyleDialog(this);
        BackColor = Theme.Canvas;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24, 18, 24, 18),
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // Titolo e sottotitolo hanno l'altezza del loro testo: niente più titolo tagliato o sovrapposto.
        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 0, 14)
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        heading.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        heading.Controls.Add(new TextBlock
        {
            Text = "Impostazioni",
            Dock = DockStyle.Top,
            ForeColor = Theme.Ink,
            Font = Theme.Fonts.CardTitle,
            Margin = Padding.Empty
        }, 0, 0);
        heading.Controls.Add(new TextBlock
        {
            Text = $"Vyrn Presence {ExecutableSignature.DisplayVersion}  ·  regole di conversione e aggiornamenti.",
            Dock = DockStyle.Top,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Small,
            Margin = new Padding(1, 2, 0, 0)
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
            Padding = new Padding(18, 12, 18, 12),
            Margin = Padding.Empty
        };
        var tabs = new ThemedTabControl { Dock = DockStyle.Fill, Margin = Padding.Empty };
        tabs.AddPage("Conversione", BuildConversionPage());
        tabs.AddPage("Aggiornamenti", BuildUpdatePage());
        tabs.AddPage("Formato XML", BuildXmlPage());
        tabs.AddPage("Notifiche", BuildNotificationsPage());
        tabs.AddPage("Generale", BuildGeneralPage());
        tabs.SelectedIndex = 0;
        shell.Controls.Add(tabs);
        root.Controls.Add(shell, 0, 1);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty,
            Padding = new Padding(0, 12, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _errorLabel.Dock = DockStyle.Fill;
        _errorLabel.ForeColor = Theme.DangerInk;
        _errorLabel.Font = Theme.Fonts.Small;
        _errorLabel.Margin = new Padding(0, 0, 0, 6);
        footer.Controls.Add(_errorLabel, 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
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

    /// <summary>Cartellini dentro la finestra (valido solo dopo "Salva impostazioni").</summary>
    public bool InAppNotifications { get; private set; }

    /// <summary>Fumetti di Windows dal tray (valido solo dopo "Salva impostazioni").</summary>
    public bool SystemNotifications { get; private set; }

    /// <summary>Cosa fa la X della finestra: «» = chiede ogni volta, «tray» = resta in background, «exit» = esce.</summary>
    public string CloseBehavior { get; private set; }

    /// <summary>Tema scelto: come Windows, chiaro o scuro (si applica al prossimo avvio; valido dopo "Salva impostazioni").</summary>
    public ThemeMode ThemeMode { get; private set; } = ThemePreference.Load();

    /// <summary>Crea subito un backup dei dati e restituisce il messaggio da mostrare (la finestra principale lo imposta).</summary>
    public Func<string>? BackupNow { get; set; }

    /// <summary>Chiesta da «Prova le notifiche»: la finestra principale le mostra con le scelte correnti.</summary>
    public event EventHandler<(bool InApp, bool System)>? NotificationTestRequested;

    /// <summary>Forma del file XML scelta (valida solo dopo "Salva impostazioni").</summary>
    public XmlFormat Format { get; private set; }

    /// <summary>True se in questa sessione è stato salvato un token valido.</summary>
    public bool TokenSaved { get; private set; }

    /// <summary>
    /// Pagina di una scheda: una colonna di righe alte quanto il loro contenuto. Nessuna altezza scritta a
    /// mano: con un carattere più grande (scala 125% o 150%) o un testo più lungo la riga cresce e nulla si
    /// sovrappone né viene tagliato.
    /// </summary>
    private (Panel Page, TableLayoutPanel Body) CreatePage()
    {
        var page = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 0,
            BackColor = Theme.Surface,
            Padding = new Padding(0, 14, 0, 10),
            Margin = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.Controls.Add(body);
        _pages.Add((page, body));
        return (page, body);
    }

    private readonly List<(Panel Page, TableLayoutPanel Body)> _pages = new();

    private static void AddRow(TableLayoutPanel body, Control control)
    {
        body.RowCount++;
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.Controls.Add(control, 0, body.RowCount - 1);
    }

    /// <summary>
    /// Per ogni scheda, l'altezza che chiede e quella che ha: se la prima supera la seconda la scheda
    /// avrebbe bisogno di una barra di scorrimento (che Windows disegna grigia, fuori tema). Lo usa il test grafico.
    /// </summary>
    internal IReadOnlyList<(int Needed, int Available)> MeasurePages() =>
        _pages.Select(entry => (
            entry.Body.GetPreferredSize(new Size(Math.Max(1, entry.Page.ClientSize.Width), 0)).Height,
            entry.Page.ClientSize.Height)).ToList();

    /// <summary>Scheda "Conversione": giornata standard, sabato e chiusure aziendali.</summary>
    private Control BuildConversionPage()
    {
        var (page, body) = CreatePage();

        AddRow(body, CreateHint(
            "Come vengono distribuite le ore della richiesta sui giorni lavorativi."));

        AddRow(body, CreateFieldLabel(
            $"Ore di una giornata standard  (in {_referenceYear} la durata della richiesta viene divisa sui giorni lavorativi)"));
        _hours.MaxLength = 5;
        _hours.Dock = DockStyle.Fill;
        _hours.InputFont = Theme.Fonts.Field;
        _hours.Placeholder = "es. 8  oppure  7,5";
        _hours.Value = Options.HoursPerDay.ToString("0.##", CultureInfo.CurrentCulture);
        _hours.Input.AccessibleName = "Ore di una giornata standard";
        _hours.Margin = new Padding(0, 2, 0, 8);
        AddRow(body, _hours);

        _saturdays.Text = "Il sabato è sempre lavorativo (le ore vengono distribuite anche di sabato)";
        _saturdays.Dock = DockStyle.Fill;
        _saturdays.TextAlign = ContentAlignment.MiddleLeft;
        _saturdays.ForeColor = Theme.InkSecondary;
        _saturdays.Font = Theme.Fonts.Small;
        _saturdays.Checked = Options.IncludeSaturdays;
        _saturdays.AccessibleName = "Considera lavorativo il sabato";
        _saturdays.Margin = Padding.Empty;
        AddRow(body, _saturdays);

        _autoDetect.Text = "Rileva in automatico sabati e ore dalle date (consigliato)";
        _autoDetect.Dock = DockStyle.Fill;
        _autoDetect.TextAlign = ContentAlignment.MiddleLeft;
        _autoDetect.ForeColor = Theme.InkSecondary;
        _autoDetect.Font = Theme.Fonts.Small;
        _autoDetect.Checked = Options.AutoDetect;
        _autoDetect.AccessibleName = "Rileva automaticamente sabati e ore";
        _autoDetect.Margin = Padding.Empty;
        AddRow(body, _autoDetect);

        _fillPresence.Text = "Registra la presenza (01) nei giorni lavorativi senza assenze";
        _fillPresence.Dock = DockStyle.Fill;
        _fillPresence.TextAlign = ContentAlignment.MiddleLeft;
        _fillPresence.ForeColor = Theme.InkSecondary;
        _fillPresence.Font = Theme.Fonts.Small;
        _fillPresence.Checked = Options.FillPresence;
        _fillPresence.AccessibleName = "Registra la presenza nei giorni senza assenze";
        _fillPresence.Margin = Padding.Empty;
        AddRow(body, _fillPresence);

        AddRow(body, CreateFieldLabel(
            "Chiusure aziendali aggiuntive (oltre alle festività nazionali): una data per riga o separate da virgola — es. 02/06, 24/12"));
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
            Padding = new Padding(8, 6, 8, 6),
            Margin = new Padding(0, 2, 0, 6),
            // Quattro righe di testo: l'altezza della riga è quella della casella, non una misura della tabella.
            Height = 88
        };
        holidaysFrame.Controls.Add(_holidays);
        AddRow(body, holidaysFrame);

        AddRow(body, CreateNote(
            "Le festività nazionali e le domeniche sono sempre escluse. Un giorno di chiusura qui indicato non riceve ore."));
        return page;
    }

    /// <summary>
    /// Scheda "Formato XML": il tracciato non dice come si chiama l'elemento radice né se
    /// c'è uno spazio dei nomi, ma il validatore di Zucchetti li pretende. Si possono
    /// impostare a mano o leggere da un file XML che Zucchetti accetta.
    /// </summary>
    private Control BuildXmlPage()
    {
        var (page, body) = CreatePage();

        AddRow(body, CreateHint(
            "Impostazioni avanzate: servono solo se Zucchetti respinge il file con «cvc-elt.1: impossibile trovare la dichiarazione dell'elemento…»."));

        AddRow(body, CreateFieldLabel("Nome dell'elemento radice del file XML (la prima riga dopo <?xml …?>)"));
        _xmlRoot.MaxLength = 64;
        _xmlRoot.Dock = DockStyle.Fill;
        _xmlRoot.InputFont = Theme.Fonts.Field;
        _xmlRoot.Placeholder = "es. Dipendenti";
        _xmlRoot.Value = Format.RootElement;
        _xmlRoot.Input.AccessibleName = "Nome dell'elemento radice";
        _xmlRoot.Margin = new Padding(0, 2, 0, 8);
        AddRow(body, _xmlRoot);

        AddRow(body, CreateFieldLabel("Spazio dei nomi (xmlns) dell'elemento radice: lascia vuoto se il file non ne ha"));
        _xmlNamespace.MaxLength = 300;
        _xmlNamespace.Dock = DockStyle.Fill;
        _xmlNamespace.InputFont = Theme.Fonts.Small;
        _xmlNamespace.Placeholder = "es. http://www.esempio.it/schema";
        _xmlNamespace.Value = Format.Namespace;
        _xmlNamespace.Input.AccessibleName = "Spazio dei nomi del file XML";
        _xmlNamespace.Margin = new Padding(0, 2, 0, 8);
        AddRow(body, _xmlNamespace);

        _xmlPerNode.Text = "Ogni movimento è un nodo «Movimenti» a sé (senza il nodo «Movimento» dentro)";
        _xmlPerNode.Dock = DockStyle.Fill;
        _xmlPerNode.TextAlign = ContentAlignment.MiddleLeft;
        _xmlPerNode.ForeColor = Theme.InkSecondary;
        _xmlPerNode.Font = Theme.Fonts.Small;
        _xmlPerNode.Checked = Format.MovementPerNode;
        _xmlPerNode.AccessibleName = "Un nodo Movimenti per ogni movimento";
        _xmlPerNode.Margin = Padding.Empty;
        AddRow(body, _xmlPerNode);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Surface,
            Margin = new Padding(0, 8, 0, 0)
        };
        var detect = Theme.CreateButton("Leggi da un XML di esempio…", Theme.ButtonTone.Neutral, compact: true);
        detect.Size = new Size(260, 34);
        detect.Margin = Padding.Empty;
        detect.AccessibleName = "Leggi la forma da un XML di esempio";
        detect.AccessibleDescription = "Sceglie un file XML accettato da Zucchetti e ne copia radice, spazio dei nomi e disposizione dei movimenti.";
        detect.Click += (_, _) => DetectFormatFromSample();
        actions.Controls.Add(detect);
        AddRow(body, actions);

        _xmlStatus.Dock = DockStyle.Fill;
        _xmlStatus.ForeColor = Theme.MutedInk;
        _xmlStatus.Font = Theme.Fonts.Tiny;
        _xmlStatus.Margin = new Padding(0, 6, 0, 0);
        _xmlStatus.Text = "Con un file XML che Zucchetti accetta (anche di un altro programma) la forma si copia senza indovinare.";
        AddRow(body, _xmlStatus);
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
        picker.ClientGuid = FileDialogs.ClientId;
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
    /// <summary>Scheda "Notifiche": cartellini nella finestra, fumetti di Windows e prova.</summary>
    private Control BuildNotificationsPage()
    {
        var (page, body) = CreatePage();

        AddRow(body, CreateHint(
            "Avvisi per conversione completata, file salvato e aggiornamenti. Quelli di Windows arrivano solo quando la finestra non è in primo piano."));

        ConfigureCheck(_inAppNotifications, "Mostra gli avvisi dentro la finestra", InAppNotifications, "Avvisi dentro la finestra");
        AddRow(body, _inAppNotifications);
        ConfigureCheck(_systemNotifications, "Mostra le notifiche di Windows quando il programma è in background", SystemNotifications, "Notifiche di Windows");
        AddRow(body, _systemNotifications);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Surface,
            Margin = new Padding(0, 8, 0, 0)
        };
        var test = Theme.CreateButton("Prova le notifiche", Theme.ButtonTone.Primary, compact: true);
        test.Size = new Size(170, 34);
        test.Margin = new Padding(0, 0, 8, 0);
        test.AccessibleName = "Prova le notifiche";
        test.Click += (_, _) => NotificationTestRequested?.Invoke(
            this,
            (_inAppNotifications.Checked, _systemNotifications.Checked));
        var windowsSettings = Theme.CreateButton("Impostazioni di Windows", Theme.ButtonTone.Neutral, compact: true);
        windowsSettings.Size = new Size(200, 34);
        windowsSettings.Margin = Padding.Empty;
        windowsSettings.AccessibleName = "Apri le impostazioni delle notifiche di Windows";
        windowsSettings.Click += (_, _) => OpenWindowsNotificationSettings();
        actions.Controls.Add(test);
        actions.Controls.Add(windowsSettings);
        AddRow(body, actions);

        AddRow(body, CreateNote(
            "Se la prova di Windows non compare, controlla Assistente focus e le notifiche di «Vyrn Presence» nelle impostazioni di Windows."));
        return page;
    }

    private static void ConfigureCheck(CheckBox box, string text, bool isChecked, string accessibleName)
    {
        box.Text = text;
        box.Dock = DockStyle.Fill;
        box.TextAlign = ContentAlignment.MiddleLeft;
        box.ForeColor = Theme.InkSecondary;
        box.Font = Theme.Fonts.Small;
        box.Checked = isChecked;
        box.AccessibleName = accessibleName;
        box.Margin = Padding.Empty;
    }

    private static void OpenWindowsNotificationSettings()
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("ms-settings:notifications") { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Pagina non disponibile: si apre a mano dalle impostazioni di Windows.
        }
    }

    /// <summary>Scheda «Generale»: comportamento della X, cartella dei dati, backup e sicurezza.</summary>
    private Control BuildGeneralPage()
    {
        var (page, body) = CreatePage();

        AddRow(body, CreateHint(
            "Cosa fa il programma quando chiudi la finestra, dove tiene i dati e come sono protetti."));

        AddRow(body, CreateFieldLabel(
            "Quando chiudi la finestra con la X  (in background nel tray ricevi gli avvisi degli aggiornamenti)"));
        _closeChoice.AddItem("Chiedi ogni volta", string.Empty);
        _closeChoice.AddItem("Resta in background (tray)", "tray");
        _closeChoice.AddItem("Esci dal programma", "exit");
        _closeChoice.SelectTag(CloseBehavior);
        _closeChoice.AccessibleName = "Alla chiusura della finestra";
        _closeChoice.Margin = new Padding(0, 6, 0, 4);
        _closeChoice.Anchor = AnchorStyles.Left;
        AddRow(body, _closeChoice);

        AddRow(body, CreateFieldLabel(
            "Aspetto dell'app  (la scelta si applica al prossimo avvio: chiudi e riapri il programma)"));
        _themeChoice.AddItem("Chiaro", ThemeMode.Light);
        _themeChoice.AddItem("Scuro", ThemeMode.Dark);
        _themeChoice.AddItem("Come Windows", ThemeMode.System);
        _themeChoice.AddItem("Vetro", ThemeMode.Glass);
        _themeChoice.SelectTag(ThemeMode);
        _themeChoice.AccessibleName = "Aspetto dell'app";
        _themeChoice.Margin = new Padding(0, 6, 0, 4);
        _themeChoice.Anchor = AnchorStyles.Left;
        AddRow(body, _themeChoice);

        AddRow(body, CreateFieldLabel(
            "Dati: tutto resta sul tuo computer, nella cartella dei dati del tuo profilo Windows."));

        _backupStatus.Dock = DockStyle.Fill;
        _backupStatus.ForeColor = Theme.MutedInk;
        _backupStatus.Font = Theme.Fonts.Small;
        _backupStatus.Margin = new Padding(0, 2, 0, 0);
        _backupStatus.Text = _general.Describe();
        AddRow(body, _backupStatus);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Surface,
            Margin = new Padding(0, 8, 0, 0)
        };
        var openData = Theme.CreateButton("Apri la cartella dei dati", Theme.ButtonTone.Neutral, compact: true).WithIcon(Glyph.Folder);
        openData.Size = new Size(212, 34);
        openData.Margin = new Padding(0, 0, 8, 0);
        openData.Enabled = _general.DataFolder.Length > 0;
        openData.Click += (_, _) => OpenFolder(_general.DataFolder);
        var backup = Theme.CreateButton("Backup dei dati ora", Theme.ButtonTone.Primary, compact: true).WithIcon(Glyph.Database);
        backup.Size = new Size(190, 34);
        backup.Margin = new Padding(0, 0, 8, 0);
        backup.Enabled = BackupNow is not null || _general.DataFolder.Length > 0;
        backup.Click += (_, _) =>
        {
            try
            {
                _backupStatus.ForeColor = Theme.SuccessInk;
                _backupStatus.Text = BackupNow?.Invoke() ?? "Backup non disponibile.";
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                _backupStatus.ForeColor = Theme.DangerInk;
                _backupStatus.Text = $"Backup non riuscito: {exception.Message}";
            }
        };
        var codes = Theme.CreateButton("Copie dei codici", Theme.ButtonTone.Neutral, compact: true).WithIcon(Glyph.Transfer);
        codes.Size = new Size(172, 34);
        codes.Margin = Padding.Empty;
        codes.Enabled = _general.CodesBackupFolder.Length > 0;
        codes.Click += (_, _) => OpenFolder(_general.CodesBackupFolder);
        actions.Controls.Add(openData);
        actions.Controls.Add(backup);
        actions.Controls.Add(codes);
        AddRow(body, actions);

        AddRow(body, CreateNote(
            "Il token degli aggiornamenti è cifrato con il tuo utente Windows. " + ExecutableSignature.DescribeCurrentExecutable()));
        return page;
    }

    private static void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            // Cartella non apribile: il percorso resta scritto nella scheda.
        }
    }

    private Control BuildUpdatePage()
    {
        var (page, body) = CreatePage();

        AddRow(body, CreateHint(
            "Le Release del repository privato si leggono con un token di sola lettura, già incluso nelle versioni ufficiali."));

        _autoCheck.Text = "Cerca aggiornamenti automaticamente all'avvio e durante l'uso";
        _autoCheck.Dock = DockStyle.Fill;
        _autoCheck.TextAlign = ContentAlignment.MiddleLeft;
        _autoCheck.ForeColor = Theme.InkSecondary;
        _autoCheck.Font = Theme.Fonts.Small;
        _autoCheck.Checked = AutoUpdateCheck;
        _autoCheck.AccessibleName = "Cerca aggiornamenti automaticamente";
        _autoCheck.Margin = Padding.Empty;
        AddRow(body, _autoCheck);

        _startWithWindows.Text = "Avvia con Windows nel tray: gli aggiornamenti arrivano subito, anche a programma chiuso";
        _startWithWindows.Dock = DockStyle.Fill;
        _startWithWindows.TextAlign = ContentAlignment.MiddleLeft;
        _startWithWindows.ForeColor = Theme.InkSecondary;
        _startWithWindows.Font = Theme.Fonts.Small;
        _startWithWindows.Checked = StartupRegistration.IsEnabled();
        _startWithWindows.AccessibleName = "Avvia con Windows";
        _startWithWindows.AccessibleDescription =
            "All'accesso a Windows il programma parte nel tray, senza aprire la finestra, e avvisa appena esce una nuova versione.";
        _startWithWindows.Margin = Padding.Empty;
        AddRow(body, _startWithWindows);

        AddRow(body, CreateFieldLabel(
            "Token personale (facoltativo, se quello incluso è scaduto). Fine-grained, permesso Contents → Read-only."));
        _token.MaxLength = 400;
        _token.Dock = DockStyle.Fill;
        _token.InputFont = Theme.Fonts.Field;
        _token.Placeholder = "github_pat_… oppure ghp_…";
        _token.Input.UseSystemPasswordChar = true;
        _token.Input.Text = UpdateService.ResolveUserToken() ?? string.Empty;
        _token.Input.AccessibleName = "Token GitHub delle Release";
        _token.Margin = new Padding(0, 2, 0, 8);
        AddRow(body, _token);

        var tokenActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Surface,
            Margin = new Padding(0, 8, 0, 0)
        };
        _verifyTokenButton.Size = new Size(180, 34);
        _verifyTokenButton.Margin = new Padding(0, 0, 8, 0);
        _verifyTokenButton.Click += async (_, _) => await VerifyTokenAsync();
        _clearTokenButton.Size = new Size(140, 34);
        _clearTokenButton.Margin = Padding.Empty;
        _clearTokenButton.Click += (_, _) => ClearToken();
        tokenActions.Controls.Add(_verifyTokenButton);
        tokenActions.Controls.Add(_clearTokenButton);
        AddRow(body, tokenActions);

        _tokenStatus.Dock = DockStyle.Fill;
        _tokenStatus.Font = Theme.Fonts.Small;
        _tokenStatus.Margin = new Padding(0, 4, 0, 0);
        SetTokenStatus(
            $"Stato attuale: {UpdateService.DescribeTokenSource()}. Il file del token è {UpdateService.TokenFilePath}",
            Theme.MutedInk);
        AddRow(body, _tokenStatus);

        AddRow(body, CreateNote(
            "Il token serve solo a leggere le Release. Se uno non vale, l'app prova gli altri (personale, file, incluso)."));

        // La firma dell'eseguibile riguarda l'aggiornamento (cioè se il file in uso
        // è quello pubblicato): sta qui, accanto alla versione delle Release.
        AddRow(body, CreateNote(ExecutableSignature.DescribeCurrentExecutable()));
        return page;
    }

    /// <summary>Sottotitolo della scheda: una frase, a capo se serve.</summary>
    private static TextBlock CreateHint(string text) => new()
    {
        Text = text,
        Wrap = true,
        Dock = DockStyle.Fill,
        ForeColor = Theme.MutedInk,
        Font = Theme.Fonts.Small,
        Margin = new Padding(0, 0, 0, 10)
    };

    /// <summary>Testo guida di un campo.</summary>
    private static TextBlock CreateFieldLabel(string text) => new()
    {
        Text = text,
        Wrap = true,
        Dock = DockStyle.Fill,
        ForeColor = Theme.MutedInk,
        Font = Theme.Fonts.Small,
        Margin = new Padding(0, 10, 0, 2)
    };

    /// <summary>Riga informativa in fondo alla scheda.</summary>
    private static TextBlock CreateNote(string text) => new()
    {
        Text = text,
        Wrap = true,
        Dock = DockStyle.Fill,
        ForeColor = Theme.FaintInk,
        Font = Theme.Fonts.Tiny,
        Margin = new Padding(0, 10, 0, 0)
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
        Options = Options with
        {
            HoursPerDay = hours,
            IncludeSaturdays = _saturdays.Checked,
            ExtraHolidays = holidays,
            AutoDetect = _autoDetect.Checked,
            FillPresence = _fillPresence.Checked
        };
        AutoUpdateCheck = _autoCheck.Checked;
        if (_startWithWindows.Checked != StartupRegistration.IsEnabled() && !StartupRegistration.Set(_startWithWindows.Checked))
        {
            _errorLabel.Text = "Windows non ha permesso di cambiare l'avvio automatico: riprova o lascia la spunta com'era.";
            return;
        }
        InAppNotifications = _inAppNotifications.Checked;
        CloseBehavior = _closeChoice.SelectedTag as string ?? string.Empty;
        ThemeMode = _themeChoice.SelectedTag is ThemeMode chosen ? chosen : ThemeMode.System;
        SystemNotifications = _systemNotifications.Checked;
        DialogResult = DialogResult.OK;
        Close();
    }
}

/// <summary>Cosa mostra la scheda «Generale»: percorsi e riassunti già calcolati dalla finestra principale.</summary>
internal sealed record GeneralInfo(string DataFolder, string DatabaseSummary, string CodesSummary, string CodesBackupFolder)
{
    public static GeneralInfo Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty);

    /// <summary>Il testo della scheda: cartella, stato del database e dei codici.</summary>
    public string Describe()
    {
        var lines = new List<string>();
        if (DataFolder.Length > 0)
        {
            lines.Add($"Cartella: {DataFolder}");
        }
        var state = string.Join(" · ", new[] { DatabaseSummary, CodesSummary }.Where(part => part.Length > 0));
        if (state.Length > 0)
        {
            lines.Add(state);
        }
        return lines.Count == 0 ? "Nessuna informazione sui dati." : string.Join(Environment.NewLine, lines);
    }
}
