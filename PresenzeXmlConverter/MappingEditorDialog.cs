namespace PresenzeXmlConverter;

internal sealed class MappingEditorDialog : Form
{
    private readonly DataGridView _employeesGrid = CreateGrid("Dipendente / nome Excel", "Codice ufficiale PAGHE");
    private readonly DataGridView _policiesGrid = CreateGrid("Valore della colonna Policy", "Codice giustificativo PAGHE", "Proposta AI locale");
    private readonly HashSet<string> _requiredEmployees;
    private readonly HashSet<string> _requiredPolicies;
    private bool _designScaled;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var factor = DeviceDpi / 96f;
        if (factor > 1.01f && !_designScaled)
        {
            _designScaled = true;
            Size = new Size((int)(Size.Width * factor), (int)(Size.Height * factor));
            MinimumSize = new Size((int)(MinimumSize.Width * factor), (int)(MinimumSize.Height * factor));
            MainForm.ScaleLayoutStyles(this, factor);
        }
    }

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
        Font = new Font("Segoe UI", 10);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(14, 5)
        };
        var employeeTab = new TabPage("Dipendenti");
        var policyTab = new TabPage("Giustificativi");
        employeeTab.Controls.Add(_employeesGrid);
        policyTab.Controls.Add(_policiesGrid);
        tabs.TabPages.Add(employeeTab);
        tabs.TabPages.Add(policyTab);
        Controls.Add(tabs);

        AddRows(_employeesGrid, employeeMappings, _requiredEmployees);
        AddRows(_policiesGrid, policyMappings, _requiredPolicies, policySuggestions);
        ((DataGridViewTextBoxColumn)_policiesGrid.Columns[1]).MaxInputLength = 3;

        var requiredNote = new Label
        {
            Text = "I campi * sono obbligatori. Le proposte AI sono solo suggerimenti: controlla i codici prima di salvare.",
            Dock = DockStyle.Bottom,
            Height = 28,
            Padding = new Padding(12, 4, 0, 0),
            ForeColor = Color.FromArgb(70, 82, 100)
        };
        Controls.Add(requiredNote);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10)
        };
        var save = new Button
        {
            Text = "Salva abbinamenti",
            DialogResult = DialogResult.None,
            Width = 170,
            Height = 34,
            BackColor = Color.FromArgb(19, 97, 160),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        var cancel = new Button
        {
            Text = "Annulla",
            DialogResult = DialogResult.Cancel,
            Width = 110,
            Height = 34
        };
        save.Click += (_, _) => SaveMappings();
        actions.Controls.Add(save);
        actions.Controls.Add(cancel);
        Controls.Add(actions);
        AcceptButton = save;
        CancelButton = cancel;
    }

    public Dictionary<string, string> EmployeeMappings { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> PolicyMappings { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static DataGridView CreateGrid(string nameHeader, string codeHeader, string? suggestionHeader = null)
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            MultiSelect = false
        };
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
        DataGridView grid,
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
                grid.Rows[rowIndex].Tag = name;
            }
            else
            {
                grid.Rows[rowIndex].Tag = name;
            }
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
        DataGridView grid,
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
