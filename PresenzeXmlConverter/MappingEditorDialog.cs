namespace PresenzeXmlConverter;

internal sealed class MappingEditorDialog : Form
{
    private readonly ThemedGrid _employeesGrid = CreateGrid("Dipendente / nome Excel", "Codice ufficiale PAGHE");
    private readonly ThemedGrid _policiesGrid = CreateGrid("Valore della colonna Policy", "Codice giustificativo PAGHE", "Proposta AI locale");
    private readonly HashSet<string> _requiredEmployees;
    private readonly HashSet<string> _requiredPolicies;

    public MappingEditorDialog(
        IReadOnlyDictionary<string, string> employeeMappings,
        IReadOnlyDictionary<string, string> policyMappings,
        IEnumerable<string> requiredEmployees,
        IEnumerable<string> requiredPolicies,
        IReadOnlyDictionary<string, string>? policySuggestions = null)
    {
        _requiredEmployees = new HashSet<string>(requiredEmployees, StringComparer.OrdinalIgnoreCase);
        _requiredPolicies = new HashSet<string>(requiredPolicies, StringComparer.OrdinalIgnoreCase);

        Text = "Codici dipendenti e giustificativi";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(760, 520);
        Size = new Size(900, 620);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Theme.StyleDialog(this);

        // Schede disegnate in casa: la striscia del TabControl nativo resta
        // grigia di sistema anche con il tema applicato.
        var tabs = new ThemedTabControl { Dock = DockStyle.Fill, Margin = Padding.Empty };
        var employeePage = new Panel { BackColor = Theme.Surface, Padding = new Padding(7), Margin = Padding.Empty };
        var policyPage = new Panel { BackColor = Theme.Surface, Padding = new Padding(7), Margin = Padding.Empty };
        employeePage.Controls.Add(_employeesGrid);
        policyPage.Controls.Add(_policiesGrid);
        tabs.AddPage("Dipendenti", employeePage);
        tabs.AddPage("Giustificativi", policyPage);
        tabs.SelectedIndex = 0;
        Controls.Add(tabs);

        AddRows(_employeesGrid, employeeMappings, _requiredEmployees);
        AddRows(_policiesGrid, policyMappings, _requiredPolicies, policySuggestions);
        ((DataGridViewTextBoxColumn)_policiesGrid.Columns[1]).MaxInputLength = 3;

        var requiredNote = new Label
        {
            Text = "I campi * sono obbligatori. Le proposte AI sono solo suggerimenti: controlla i codici prima di salvare.",
            Dock = DockStyle.Bottom,
            Height = 30,
            Padding = new Padding(14, 6, 0, 0),
            BackColor = Theme.Canvas,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Small
        };
        Controls.Add(requiredNote);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Theme.Canvas,
            Padding = new Padding(12)
        };
        var save = Theme.CreateButton("Salva abbinamenti", Theme.ButtonTone.Primary);
        save.Size = new Size(180, 36);
        var cancel = Theme.CreateButton("Annulla", Theme.ButtonTone.Neutral);
        cancel.Size = new Size(120, 36);
        cancel.DialogResult = DialogResult.Cancel;
        save.Click += (_, _) => SaveMappings();
        actions.Controls.Add(save);
        actions.Controls.Add(cancel);
        Controls.Add(actions);
        AcceptButton = save;
        CancelButton = cancel;
    }

    public Dictionary<string, string> EmployeeMappings { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> PolicyMappings { get; } = new(StringComparer.OrdinalIgnoreCase);

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

    private static void AddRows(
        ThemedGrid grid,
        IReadOnlyDictionary<string, string> mappings,
        IEnumerable<string> required,
        IReadOnlyDictionary<string, string>? suggestions = null)
    {
        var names = mappings.Keys.Concat(required)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase);
        foreach (var name in names)
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
            if (required.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                grid.Rows[rowIndex].Cells[0].Value = $"{name} *";
            }
            grid.Rows[rowIndex].Tag = name;
        }
    }

    private void SaveMappings()
    {
        if (!TryReadMappings(_employeesGrid, _requiredEmployees, 7, "dipendente", EmployeeMappings) ||
            !TryReadMappings(_policiesGrid, _requiredPolicies, 3, "giustificativo", PolicyMappings))
        {
            return;
        }

        var duplicateCode = EmployeeMappings
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.Value))
            .GroupBy(mapping => mapping.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCode is not null)
        {
            MessageBox.Show(
                this,
                $"Il codice dipendente “{duplicateCode.Key}” è assegnato a più nomi. Verifica gli abbinamenti.",
                "Codice dipendente duplicato",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TryReadMappings(
        ThemedGrid grid,
        IReadOnlySet<string> required,
        int maximumLength,
        string label,
        IDictionary<string, string> output)
    {
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
                    MessageBox.Show(
                        this,
                        $"Inserisci il codice {label} richiesto per “{name}”.",
                        "Codice mancante",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }
                continue;
            }
            if (code.Length > maximumLength)
            {
                MessageBox.Show(
                    this,
                    $"Il codice {label} “{code}” supera il limite di {maximumLength} caratteri del tracciato.",
                    "Codice non valido",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
            output[name] = code;
        }
        return true;
    }
}
