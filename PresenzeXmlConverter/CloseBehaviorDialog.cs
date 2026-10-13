namespace PresenzeXmlConverter;

/// <summary>
/// Chiede una sola volta come comportarsi alla chiusura: esci definitivamente
/// o resta in tray per ricevere le notifiche di fine conversione.
/// </summary>
internal sealed class CloseBehaviorDialog : Form
{
    private readonly CheckBox _remember = new();

    public CloseBehaviorDialog()
    {
        Text = "Chiudi PresenzeFlow";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(470, 246);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Theme.StyleDialog(this);
        BackColor = Theme.Surface;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(22, 18, 22, 16),
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Come vuoi chiudere il programma?",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Ink,
            Font = Theme.Fonts.DialogTitle,
            Margin = Padding.Empty
        }, 0, 0);

        root.Controls.Add(new Label
        {
            Text = "Se resti in background ricevi una notifica quando una conversione termina,\nsenza tenere la finestra aperta.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Small,
            Margin = Padding.Empty
        }, 0, 1);

        var trayButton = Theme.CreateButton("Riduci in tray  ·  notifiche attive", Theme.ButtonTone.Primary);
        trayButton.Dock = DockStyle.Fill;
        trayButton.Margin = new Padding(0, 2, 0, 4);
        trayButton.DialogResult = DialogResult.OK;
        root.Controls.Add(trayButton, 0, 2);

        var exitButton = Theme.CreateButton("Esci definitivamente", Theme.ButtonTone.Neutral);
        exitButton.Dock = DockStyle.Fill;
        exitButton.Margin = new Padding(0, 0, 0, 4);
        exitButton.DialogResult = DialogResult.Abort;
        root.Controls.Add(exitButton, 0, 3);

        _remember.Text = "Ricorda questa scelta (non chiedere più)";
        _remember.AutoSize = true;
        // Non preselezionata: «non chiedere più» è una scelta da fare di proposito,
        // perché poi la finestra si chiude in tray senza più domande.
        _remember.Checked = false;
        _remember.Dock = DockStyle.Fill;
        _remember.TextAlign = ContentAlignment.MiddleLeft;
        _remember.ForeColor = Theme.InkSecondary;
        _remember.Font = Theme.Fonts.Small;
        _remember.Margin = Padding.Empty;
        root.Controls.Add(_remember, 0, 4);

        AcceptButton = trayButton;
        // Esc e la X non devono uscire dal programma: sono un ripensamento, e la
        // finestra principale resta aperta (DialogResult.Cancel). Il pulsante
        // «Esci definitivamente» NON è il pulsante di annullamento: l'esito Abort
        // era interpretato come «annulla» e il programma non si chiudeva mai.
        KeyPreview = true;
        KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
            }
        };
    }

    /// <summary>"tray" per restare in background, "exit" per chiudere definitivamente.</summary>
    public string SelectedBehavior => DialogResult == DialogResult.Abort ? "exit" : "tray";

    /// <summary>
    /// La scelta fatta nel dialogo: solo «Riduci in tray» (OK) e «Esci definitivamente»
    /// (Abort) sono scelte; la X e Esc (Cancel) lasciano il programma com'è.
    /// </summary>
    public static bool IsChoice(DialogResult result) => result is DialogResult.OK or DialogResult.Abort;

    public bool RememberChoice => _remember.Checked;
}
