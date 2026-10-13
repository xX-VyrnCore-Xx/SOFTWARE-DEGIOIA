using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter;

/// <summary>
/// Sezione «Aziende»: l'anagrafica delle aziende (codice ufficiale PAGHE e ragione
/// sociale) salvata nel database locale. Il codice usato in conversione resta
/// sempre sul computer, si ritrova scrivendo le prime lettere e ogni codice digitato
/// in conversione entra da solo nell'elenco: nessuno deve reinserire l'azienda.
/// </summary>
internal sealed partial class MainForm
{
    private const string DefaultCompanyHint = "Codice Zucchetti · max 6 caratteri";

    private readonly TableLayoutPanel _companiesSection = NewSection();
    private readonly ThemedGrid _companiesGrid = new();
    private readonly RoundedField _companyEditCode = new();
    private readonly RoundedField _companyEditName = new();
    private readonly RoundedField _companyEditNotes = new();
    private readonly Label _companiesSummaryLabel = new();
    private readonly Button _companySaveButton = new();
    private readonly Button _companyNewButton = new();
    private readonly Button _companyDeleteButton = new();
    private readonly Button _companyRulesButton = new();
    private readonly Button _companyUseButton = new();
    private Label? _companyHintLabel;
    private IReadOnlyList<CompanyRecord> _companies = Array.Empty<CompanyRecord>();
    private bool _companyFormLoading;

    private Control BuildCompaniesSection()
    {
        var page = _companiesSection;
        page.RowCount = 3;
        page.ColumnCount = 1;
        page.Padding = new Padding(0, 0, 4, 0);
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        page.Controls.Add(BuildSectionHeading(
            "Aziende",
            "Le aziende per cui prepari i file: codice ufficiale PAGHE e ragione sociale, sempre salvati."), 0, 0);

        var card = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.Surface,
            SurfaceBorder = Theme.Divider,
            Radius = 12,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(0, 0, 0, 8)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

        layout.Controls.Add(CreateCardTitle("CODICE PAGHE"), 0, 0);
        layout.Controls.Add(CreateCardTitle("RAGIONE SOCIALE"), 1, 0);
        layout.Controls.Add(CreateCardTitle("NOTE"), 2, 0);

        ConfigureCompanyField(_companyEditCode, "ABC123", 6, "Codice azienda ufficiale PAGHE", new Padding(0, 4, 8, 4));
        _companyEditCode.CharacterCasing = CharacterCasing.Upper;
        ConfigureCompanyField(_companyEditName, "Ragione sociale", 120, "Ragione sociale dell'azienda", new Padding(0, 4, 8, 4));
        ConfigureCompanyField(_companyEditNotes, "Facoltative", 500, "Note sull'azienda", new Padding(0, 4, 0, 4));
        layout.Controls.Add(_companyEditCode, 0, 1);
        layout.Controls.Add(_companyEditName, 1, 1);
        layout.Controls.Add(_companyEditNotes, 2, 1);

        _companiesGrid.ReadOnly = true;
        _companiesGrid.EmptyStateText =
            "Nessuna azienda salvata: scrivi codice e ragione sociale e premi «Salva azienda».";
        _companiesGrid.Columns.Add(CreateGridColumn("use", "In uso", 10, true));
        _companiesGrid.Columns.Add(CreateGridColumn("code", "Codice", 14, true));
        _companiesGrid.Columns.Add(CreateGridColumn("name", "Ragione sociale", 32, true));
        _companiesGrid.Columns.Add(CreateGridColumn("rules", "Regole", 20, true));
        _companiesGrid.Columns.Add(CreateGridColumn("notes", "Note", 24, true));
        _companiesGrid.SelectionChanged += (_, _) => OnCompanySelected();
        _companiesGrid.CellDoubleClick += (_, args) =>
        {
            if (args.RowIndex >= 0)
            {
                UseSelectedCompany();
            }
        };
        layout.Controls.Add(_companiesGrid, 0, 2);
        layout.SetColumnSpan(_companiesGrid, 3);

        _companiesSummaryLabel.Dock = DockStyle.Fill;
        _companiesSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _companiesSummaryLabel.ForeColor = Theme.MutedInk;
        _companiesSummaryLabel.Font = Theme.Fonts.Tiny;
        _companiesSummaryLabel.AutoEllipsis = true;
        _companiesSummaryLabel.Margin = Padding.Empty;
        layout.Controls.Add(_companiesSummaryLabel, 0, 3);
        layout.SetColumnSpan(_companiesSummaryLabel, 3);

        card.Controls.Add(layout);
        page.Controls.Add(card, 0, 1);
        page.Controls.Add(BuildCompaniesActions(), 0, 2);
        UpdateCompanyButtons();
        return page;
    }

    private static void ConfigureCompanyField(RoundedField field, string placeholder, int maxLength, string accessibleName, Padding margin)
    {
        field.MaxLength = maxLength;
        field.InputFont = Theme.Fonts.Field;
        field.Placeholder = placeholder;
        field.Input.AccessibleName = accessibleName;
        field.Dock = DockStyle.Fill;
        field.Margin = margin;
    }

    private Control BuildCompaniesActions()
    {
        // Solo i pulsanti: il testo guida («Doppio clic su una riga…») rubava ai
        // cinque pulsanti la larghezza che serviva a tenere ogni etichetta su una riga.
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var actions = new FlowLayoutPanel
        {
            // Colonna AutoSize: il pannello dichiara la propria larghezza, altrimenti
            // la colonna lo stringe e i pulsanti più a sinistra vengono tagliati.
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        _companySaveButton.Text = "Salva azienda";
        Theme.StyleButton(_companySaveButton, Theme.ButtonTone.Primary, compact: true);
        _companySaveButton.Margin = new Padding(8, 0, 0, 0);
        _companySaveButton.Click += (_, _) => SaveCompanyFromForm();
        Describe(_companySaveButton, "Salva azienda", "Crea l'azienda o aggiorna quella con lo stesso codice.");

        _companyUseButton.Text = "Usa in conversione";
        Theme.StyleButton(_companyUseButton, Theme.ButtonTone.Positive, compact: true);
        _companyUseButton.Margin = new Padding(8, 0, 0, 0);
        _companyUseButton.Click += (_, _) => UseSelectedCompany();
        Describe(_companyUseButton, "Usa in conversione", "Imposta l'azienda selezionata come azienda del file XML.");

        _companyNewButton.Text = "Nuova";
        Theme.StyleButton(_companyNewButton, Theme.ButtonTone.Neutral, compact: true);
        _companyNewButton.Margin = new Padding(8, 0, 0, 0);
        _companyNewButton.Click += (_, _) => ClearCompanyForm();
        Describe(_companyNewButton, "Nuova azienda", "Svuota i campi per inserire un'altra azienda.");

        _companyDeleteButton.Text = "Elimina";
        Theme.StyleButton(_companyDeleteButton, Theme.ButtonTone.Neutral, compact: true);
        _companyDeleteButton.Margin = Padding.Empty;
        _companyDeleteButton.Click += (_, _) => DeleteSelectedCompany();
        Describe(_companyDeleteButton, "Elimina azienda", "Toglie l'azienda dall'elenco: i file già esportati non cambiano.");

        _companyRulesButton.Text = "Regole…";
        Theme.StyleButton(_companyRulesButton, Theme.ButtonTone.Neutral, compact: true);
        _companyRulesButton.Margin = new Padding(8, 0, 0, 0);
        _companyRulesButton.Click += (_, _) => EditSelectedCompanyRules();
        Describe(
            _companyRulesButton,
            "Regole di conversione dell'azienda",
            "Giornata standard, sabato e chiusure proprie di questa azienda: si applicano da sole quando la scegli in conversione.");

        // Il flusso va da destra a sinistra: il primo aggiunto è il più a destra, e
        // lì sta «Salva azienda», l'azione principale, che non deve mai finire fuori.
        actions.Controls.Add(_companySaveButton);
        actions.Controls.Add(_companyUseButton);
        actions.Controls.Add(_companyNewButton);
        actions.Controls.Add(_companyRulesButton);
        actions.Controls.Add(_companyDeleteButton);
        bar.Controls.Add(actions, 0, 0);
        return bar;
    }

    /// <summary>
    /// Carica le aziende all'avvio, dopo il primo disegno, per i suggerimenti del campo
    /// azienda: un errore del database non deve disturbare l'apertura.
    /// </summary>
    private void LoadCompaniesQuietly()
    {
        try
        {
            _companies = _database.GetCompanies();
        }
        catch (SqliteException)
        {
            _companies = Array.Empty<CompanyRecord>();
        }
        RefreshCompanyHints();
        ApplyCompanyRules(announce: false);
    }

    /// <summary>Rilegge le aziende dal database e aggiorna griglia, suggerimenti e testo guida.</summary>
    private void RefreshCompanies()
    {
        try
        {
            _companies = _database.GetCompanies();
        }
        catch (SqliteException exception)
        {
            ShowError("Impossibile leggere le aziende", exception);
            return;
        }
        var inUse = _companyCode.Value.Trim();
        var selectedCode = SelectedCompany()?.Code;
        _companiesGrid.Rows.Clear();
        foreach (var company in _companies)
        {
            var index = _companiesGrid.Rows.Add(
                string.Equals(company.Code, inUse, StringComparison.OrdinalIgnoreCase) ? "●" : string.Empty,
                company.Code,
                company.Name,
                company.Rules is null ? "generali" : GenerationSettings.Describe(company.Rules.ToOptions(DateTime.Today.Year)),
                company.Notes);
            _companiesGrid.Rows[index].Tag = company;
            if (selectedCode is not null && string.Equals(company.Code, selectedCode, StringComparison.OrdinalIgnoreCase))
            {
                _companiesGrid.ClearSelection();
                _companiesGrid.Rows[index].Selected = true;
            }
        }
        _companiesSummaryLabel.Text = _companies.Count == 0
            ? "Nessuna azienda salvata."
            : $"{_companies.Count:N0} aziende salvate" +
              (inUse.Length == 0 ? string.Empty : $" · in uso: {inUse}");
        RefreshCompanyHints();
        UpdateCompanyButtons();
        ApplyCompanyRules(announce: true);
    }

    /// <summary>Suggerimenti del campo azienda in conversione e ragione sociale sotto al campo.</summary>
    private void RefreshCompanyHints()
    {
        try
        {
            var codes = new AutoCompleteStringCollection();
            codes.AddRange(_companies.Select(company => company.Code).ToArray());
            _companyCode.Input.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _companyCode.Input.AutoCompleteSource = AutoCompleteSource.CustomSource;
            _companyCode.Input.AutoCompleteCustomSource = codes;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            // I suggerimenti sono una comodità: senza si digita il codice a mano.
        }
        UpdateCompanyHintLabel();
    }

    /// <summary>Contatori della barra laterale e azienda in uso nel piede della barra.</summary>
    private void UpdateNavSummaries()
    {
        _navRail.SetBadge(SectionCompanies, _companies.Count == 0 ? null : _companies.Count.ToString("N0"));
        _navRail.SetBadge(SectionHistory, _exports.Count == 0 ? null : _exports.Count.ToString("N0"));
        var code = _companyCode.Value.Trim();
        var known = _companies.FirstOrDefault(company =>
            string.Equals(company.Code, code, StringComparison.OrdinalIgnoreCase));
        _railCompanyLabel.Text = code.Length == 0
            ? "nessuna azienda"
            : known is { Name.Length: > 0 } ? $"Azienda {known.Label}" : $"Azienda {code}";
    }

    /// <summary>
    /// Calcola le regole in uso: quelle proprie dell'azienda scelta, altrimenti le generali.
    /// Se cambiano, l'anteprima già generata non vale più e le richieste del foglio si rileggono
    /// con la nuova giornata standard.
    /// </summary>
    private void ApplyCompanyRules(bool announce)
    {
        var code = _companyCode.Value.Trim();
        var company = _companies.FirstOrDefault(item =>
            string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));
        var referenceYear = _inspection?.PeriodStart?.Year ?? DateTime.Today.Year;
        var effective = company?.Rules is { } rules ? rules.ToOptions(referenceYear) : _globalOptions;
        var wasOwn = _companyRulesActive;
        _companyRulesActive = company?.Rules is not null;
        var changed = !GenerationSettings.AreEquivalent(_generationOptions, effective);
        if (!changed && wasOwn == _companyRulesActive)
        {
            return;
        }
        var previousHours = _generationOptions.HoursPerDay;
        _generationOptions = effective;
        if (_inspection is not null && _columns is not null && previousHours != effective.HoursPerDay)
        {
            try
            {
                _requests = _inspection.Read(_columns, effective.HoursPerDay).Requests;
            }
            catch (InvalidDataException)
            {
                // Le richieste restano quelle già lette: la conversione le segnalerà.
            }
        }
        RefreshDashboard();
        if (announce && changed)
        {
            InvalidatePreview();
            SetStatus(
                _companyRulesActive
                    ? $"●   Regole dell'azienda {code} applicate: {GenerationSettings.Describe(effective)}. Rigenera l'anteprima."
                    : "●   Regole generali applicate. Rigenera l'anteprima.",
                false);
        }
    }

    private bool _companyRulesActive;

    private void EditSelectedCompanyRules()
    {
        var company = SelectedCompany();
        if (company is null)
        {
            return;
        }
        using var dialog = new CompanyRulesDialog(
            company,
            _globalOptions,
            _inspection?.PeriodStart?.Year ?? DateTime.Today.Year);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            _database.SaveCompanyRules(company.Code, dialog.Rules);
        }
        catch (Exception exception) when (exception is SqliteException or InvalidDataException)
        {
            ShowError("Impossibile salvare le regole", exception);
            return;
        }
        RefreshCompanies();
        SelectCompanyRow(company.Code);
        SetStatus(
            dialog.Rules is null
                ? $"●   L'azienda {company.Code} usa le regole generali."
                : $"●   Regole dell'azienda {company.Code} salvate: {GenerationSettings.Describe(dialog.Rules.ToOptions(DateTime.Today.Year))}.",
            false);
    }

    private void UpdateCompanyHintLabel()
    {
        UpdateNavSummaries();
        if (_companyHintLabel is null)
        {
            return;
        }
        var code = _companyCode.Value.Trim();
        var known = _companies.FirstOrDefault(company =>
            string.Equals(company.Code, code, StringComparison.OrdinalIgnoreCase));
        _companyHintLabel.Text = known is { Name.Length: > 0 } ? known.Name : DefaultCompanyHint;
    }

    private CompanyRecord? SelectedCompany() =>
        _companiesGrid.SelectedRows.Count > 0 &&
        _companiesGrid.SelectedRows[0].Index < _companiesGrid.Rows.Count
            ? _companiesGrid.SelectedRows[0].Tag as CompanyRecord
            : null;

    private void OnCompanySelected()
    {
        var company = SelectedCompany();
        if (company is not null && !_companyFormLoading)
        {
            _companyFormLoading = true;
            _companyEditCode.Value = company.Code;
            _companyEditName.Value = company.Name;
            _companyEditNotes.Value = company.Notes;
            _companyFormLoading = false;
        }
        UpdateCompanyButtons();
    }

    private void UpdateCompanyButtons()
    {
        var hasSelection = SelectedCompany() is not null;
        _companyUseButton.Enabled = hasSelection;
        _companyRulesButton.Enabled = hasSelection;
        _companyDeleteButton.Enabled = hasSelection;
    }

    private void ClearCompanyForm()
    {
        _companiesGrid.ClearSelection();
        _companyEditCode.Value = string.Empty;
        _companyEditName.Value = string.Empty;
        _companyEditNotes.Value = string.Empty;
        _companyEditCode.FocusInput();
        UpdateCompanyButtons();
    }

    private void SaveCompanyFromForm()
    {
        var code = _companyEditCode.Value.Trim();
        if (code.Length is 0 or > 6)
        {
            MessageBox.Show(
                this,
                "Il codice azienda ufficiale PAGHE deve contenere da 1 a 6 caratteri.",
                "Codice azienda non valido",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _companyEditCode.FocusInput();
            return;
        }
        try
        {
            _database.SaveCompany(code, _companyEditName.Value, _companyEditNotes.Value);
        }
        catch (Exception exception) when (exception is SqliteException or InvalidDataException)
        {
            ShowError("Impossibile salvare l'azienda", exception);
            return;
        }
        // Se è l'azienda in uso la ragione sociale appare subito sotto al campo.
        RefreshCompanies();
        SelectCompanyRow(code);
        SetStatus($"●   Azienda {code} salvata.", false);
    }

    private void SelectCompanyRow(string code)
    {
        foreach (DataGridViewRow row in _companiesGrid.Rows)
        {
            if (row.Tag is CompanyRecord company &&
                string.Equals(company.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                _companiesGrid.ClearSelection();
                row.Selected = true;
                return;
            }
        }
    }

    private void UseSelectedCompany()
    {
        var company = SelectedCompany();
        if (company is null)
        {
            return;
        }
        _companyCode.Value = company.Code;
        SaveCompanyCode();
        RefreshCompanies();
        SetStatus($"●   Azienda {company.Label} in uso per la conversione.", false);
    }

    private void DeleteSelectedCompany()
    {
        var company = SelectedCompany();
        if (company is null)
        {
            return;
        }
        var answer = MessageBox.Show(
            this,
            $"Eliminare l'azienda {company.Label} dall'elenco?{Environment.NewLine}{Environment.NewLine}" +
            "I file XML già esportati non cambiano.",
            "Elimina azienda",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
        {
            return;
        }
        try
        {
            _database.DeleteCompany(company.Code);
        }
        catch (SqliteException exception)
        {
            ShowError("Impossibile eliminare l'azienda", exception);
            return;
        }
        ClearCompanyForm();
        RefreshCompanies();
        SetStatus($"●   Azienda {company.Code} eliminata dall'elenco.", false);
    }
}
