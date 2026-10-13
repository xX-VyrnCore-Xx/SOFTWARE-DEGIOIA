namespace PresenzeXmlConverter;

/// <summary>
/// Editor degli abbinamenti nome → codice PAGHE e Policy → giustificativo, in
/// due schede. Era il contenuto del solo dialogo "Codici PAGHE": ora è un
/// pannello riusabile, così la stessa identica schermata si apre sia come
/// finestra sia come sezione della finestra principale, senza due copie della
/// stessa logica di validazione.
///
/// Il pannello non salva nulla da sé: compila le proprie mappe con
/// <see cref="TrySave"/> e chi lo ospita decide dove scriverle (database locale).
/// </summary>
internal sealed class MappingEditorPanel : Panel
{
    private readonly ThemedTabControl _tabs = new();
    private readonly ThemedGrid _employeesGrid = CreateGrid("Dipendente / nome Excel", "Codice ufficiale PAGHE");
    private readonly ThemedGrid _policiesGrid = CreateGrid(
        "Valore della colonna Policy",
        "Codice giustificativo PAGHE");
    private readonly Label _note = new();
    private HashSet<string> _requiredEmployees = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _requiredPolicies = new(StringComparer.OrdinalIgnoreCase);

    public MappingEditorPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Surface;
        Margin = Padding.Empty;

        var employeePage = new Panel { BackColor = Theme.Surface, Padding = new Padding(7), Margin = Padding.Empty };
        var policyPage = new Panel { BackColor = Theme.Surface, Padding = new Padding(7), Margin = Padding.Empty };
        employeePage.Controls.Add(_employeesGrid);
        policyPage.Controls.Add(_policiesGrid);
        _tabs.Dock = DockStyle.Fill;
        _tabs.Margin = Padding.Empty;
        _tabs.AddPage("Dipendenti", employeePage);
        _tabs.AddPage("Giustificativi", policyPage);
        _tabs.SelectedIndex = 0;
        Controls.Add(_tabs);

        _note.Text =
            "I campi * sono obbligatori. Controlla i codici prima di salvare.";
        _note.Dock = DockStyle.Bottom;
        _note.Height = 28;
        _note.Padding = new Padding(8, 4, 0, 0);
        _note.BackColor = Theme.Surface;
        _note.ForeColor = Theme.MutedInk;
        _note.Font = Theme.Fonts.Small;
        _note.AutoEllipsis = true;
        Controls.Add(_note);

        ((DataGridViewTextBoxColumn)_policiesGrid.Columns[1]).MaxInputLength = 3;
    }

    /// <summary>Abinamenti validati dall'ultimo <see cref="TrySave"/> riuscito.</summary>
    public Dictionary<string, string> EmployeeMappings { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> PolicyMappings { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Riempie le due griglie con gli abbinamenti salvati e con i valori del
    /// foglio importato che ne sono ancora privi (quelli obbligatori sono
    /// segnati con un asterisco).
    /// </summary>
    public void Load(
        IReadOnlyDictionary<string, string> employeeMappings,
        IReadOnlyDictionary<string, string> policyMappings,
        IEnumerable<string> requiredEmployees,
        IEnumerable<string> requiredPolicies,
        IReadOnlyDictionary<string, string>? policySuggestions = null)
    {
        _requiredEmployees = new HashSet<string>(requiredEmployees, StringComparer.OrdinalIgnoreCase);
        _requiredPolicies = new HashSet<string>(requiredPolicies, StringComparer.OrdinalIgnoreCase);
        Fill(_employeesGrid, employeeMappings, _requiredEmployees);
        Fill(_policiesGrid, policyMappings, _requiredPolicies, policySuggestions);
    }

    /// <summary>
    /// Convalida le griglie e compila <see cref="EmployeeMappings"/> e
    /// <see cref="PolicyMappings"/>. In caso di problema restituisce false e la
    /// spiegazione, senza aprire finestre: decide chi ospita il pannello come
    /// mostrarla (un messaggio nel dialogo, lo stato nella finestra principale).
    /// </summary>
    public bool TrySave(out string? error)
    {
        error = null;
        if (!TryReadMappings(_employeesGrid, _requiredEmployees, 7, "dipendente", EmployeeMappings, out error) ||
            !TryReadMappings(_policiesGrid, _requiredPolicies, 3, "giustificativo", PolicyMappings, out error))
        {
            return false;
        }

        var duplicate = EmployeeMappings
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.Value))
            .GroupBy(mapping => mapping.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            error = $"Il codice dipendente “{duplicate.Key}” è assegnato a più nomi. Verifica gli abbinamenti.";
            return false;
        }

        return true;
    }

    /// <summary>Sceglie la scheda da mostrare (0 = Dipendenti, 1 = Giustificativi).</summary>
    public void SelectTab(int index) => _tabs.SelectedIndex = index;

    private static ThemedGrid CreateGrid(string nameHeader, string codeHeader, string? suggestionHeader = null)
    {
        var grid = new ThemedGrid();
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = nameHeader,
            ReadOnly = true,
            FillWeight = 64,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = codeHeader,
            FillWeight = 36,
            MaxInputLength = 7,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        if (suggestionHeader is not null)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = suggestionHeader,
                ReadOnly = true,
                FillWeight = 36,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }
        return grid;
    }

    private static void Fill(
        ThemedGrid grid,
        IReadOnlyDictionary<string, string> mappings,
        IEnumerable<string> required,
        IReadOnlyDictionary<string, string>? suggestions = null)
    {
        grid.Rows.Clear();
        var requiredSet = required.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in mappings.Keys.Concat(requiredSet)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase))
        {
            var code = mappings.TryGetValue(name, out var existing) ? existing : string.Empty;
            var suggestion = suggestions is not null && suggestions.TryGetValue(name, out var proposed)
                ? proposed
                : string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                code = suggestion;
            }
            var rowIndex = grid.Columns.Count > 2
                ? grid.Rows.Add(name, code, suggestion)
                : grid.Rows.Add(name, code);
            if (requiredSet.Contains(name))
            {
                grid.Rows[rowIndex].Cells[0].Value = $"{name} *";
            }
            grid.Rows[rowIndex].Tag = name;
        }
    }

    private static bool TryReadMappings(
        ThemedGrid grid,
        IReadOnlySet<string> required,
        int maximumLength,
        string label,
        IDictionary<string, string> output,
        out string? error)
    {
        error = null;
        output.Clear();
        grid.EndEdit();
        foreach (DataGridViewRow row in grid.Rows)
        {
            var name = row.Tag as string ?? string.Empty;
            var code = Convert.ToString(row.Cells[1].Value)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                if (required.Contains(name))
                {
                    error = $"Inserisci il codice {label} richiesto per “{name}”.";
                    return false;
                }
                continue;
            }
            if (code.Length > maximumLength)
            {
                error =
                    $"Il codice {label} “{code}” supera il limite di {maximumLength} caratteri del tracciato.";
                return false;
            }
            output[name] = code;
        }
        return true;
    }
}
