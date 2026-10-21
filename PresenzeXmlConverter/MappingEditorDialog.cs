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
        UiScale.GuardAutoScale(this);
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

    /// <summary>Codici richiesti dal foglio che mancano ancora dopo il salvataggio.</summary>
    public int MissingRequiredCount => _panel.MissingRequiredCount;

    /// <summary>
    /// Chiudere con «Annulla» o con la X dopo aver scritto dei codici non li butta via di colpo:
    /// si chiede se salvare quelli inseriti.
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK && _panel.HasUnsavedChanges && !_closingAfterAsk)
        {
            var answer = MessageBox.Show(
                this,
                "Hai inserito dei codici non ancora salvati. Vuoi salvare quelli inseriti? " +
                "Puoi completare gli altri più tardi.",
                "Abbinamenti non salvati",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);
            if (answer == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            _closingAfterAsk = true;
            if (answer == DialogResult.Yes)
            {
                // Si salva senza chiudere dal dialogo «sì»: il risultato resta Annulla ma «Committed» dice al
                // chiamante che i codici vanno comunque scritti nel database.
                CommitMappings();
            }
        }
        base.OnFormClosing(e);
    }

    private bool _closingAfterAsk;

    /// <summary>True se i codici inseriti sono stati raccolti (anche chiudendo con «Annulla» e scegliendo di salvarli).</summary>
    public bool Committed { get; private set; }

    private void SaveMappings()
    {
        CommitMappings();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CommitMappings()
    {
        // Salvataggio «a metà»: i codici mancanti non bloccano; solo le righe scritte male restano fuori.
        _panel.TrySave(out var note, allowIncomplete: true);
        if (note is not null || _panel.Warnings is not null)
        {
            var message = note is not null
                ? note + Environment.NewLine + "Il resto è stato salvato." +
                  (_panel.Warnings is not null ? Environment.NewLine + _panel.Warnings : string.Empty)
                : _panel.Warnings!;
            MessageBox.Show(
                this,
                message,
                note is not null ? "Abbinamenti salvati in parte" : "Abbinamenti salvati",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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
        Committed = true;
    }
}
