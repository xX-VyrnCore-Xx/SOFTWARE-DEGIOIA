namespace PresenzeXmlConverter;

/// <summary>
/// Finestra "Codici dipendenti e giustificativi": è l'involucro del pannello
/// <see cref="MappingEditorPanel"/>, che è lo stesso usato dalla sezione
/// "Gestione codici" della finestra principale. Qui restano solo i pulsanti di
/// conferma e la finestra di avviso: la logica delle griglie sta in un posto solo.
/// </summary>
internal sealed class MappingEditorDialog : Form
{
    private readonly MappingEditorPanel _panel = new();

    public MappingEditorDialog(
        IReadOnlyDictionary<string, string> employeeMappings,
        IReadOnlyDictionary<string, string> policyMappings,
        IEnumerable<string> requiredEmployees,
        IEnumerable<string> requiredPolicies,
        IReadOnlyDictionary<string, string>? policySuggestions = null,
        IReadOnlyDictionary<string, EmployeeProfile>? employeeProfiles = null)
    {
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

        _panel.Load(
            employeeMappings,
            policyMappings,
            requiredEmployees,
            requiredPolicies,
            policySuggestions,
            employeeProfiles);

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
        save.AccessibleName = "Salva abbinamenti";
        var cancel = Theme.CreateButton("Annulla", Theme.ButtonTone.Neutral);
        cancel.Size = new Size(120, 36);
        cancel.DialogResult = DialogResult.Cancel;
        cancel.AccessibleName = "Annulla";
        save.Click += (_, _) => SaveMappings();
        actions.Controls.Add(save);
        actions.Controls.Add(cancel);

        // Prima le griglie (riempiono), poi i pulsanti in basso: l'ordine con cui
        // si aggiungono decide chi prende lo spazio che avanza.
        Controls.Add(_panel);
        Controls.Add(actions);
        AcceptButton = save;
        CancelButton = cancel;
    }

    public Dictionary<string, string> EmployeeMappings { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> PolicyMappings { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, EmployeeProfile> EmployeeProfiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    private void SaveMappings()
    {
        if (!_panel.TrySave(out var error))
        {
            MessageBox.Show(
                this,
                error,
                "Abbinamenti da verificare",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        foreach (var mapping in _panel.EmployeeMappings)
        {
            EmployeeMappings[mapping.Key] = mapping.Value;
        }
        foreach (var mapping in _panel.PolicyMappings)
        {
            PolicyMappings[mapping.Key] = mapping.Value;
        }
        foreach (var profile in _panel.EmployeeProfiles)
        {
            EmployeeProfiles[profile.Key] = profile.Value;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
