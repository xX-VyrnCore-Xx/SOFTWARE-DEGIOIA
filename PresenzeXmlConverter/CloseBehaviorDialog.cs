namespace PresenzeXmlConverter;

/// <summary>
/// Chiede una sola volta come comportarsi alla chiusura: esci definitivamente
/// o resta in tray per ricevere le notifiche di fine conversione.
/// </summary>
internal sealed class CloseBehaviorDialog : Form
{
    private readonly ToggleSwitch _remember = new();

    public CloseBehaviorDialog()
    {
        Text = "Chiudi Vyrn Presence";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 240);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        UiScale.GuardAutoScale(this);
        Theme.StyleDialog(this);
        BackColor = Theme.Surface;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(26, 22, 26, 18),
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 5; row++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        Controls.Add(root);

        root.Controls.Add(new TextBlock
        {
            Text = "Come vuoi chiudere il programma?",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Ink,
            Font = Theme.Fonts.DialogTitle,
            Margin = new Padding(0, 0, 0, 6)
        }, 0, 0);

        root.Controls.Add(new TextBlock
        {
            Text = "Se resti in background ricevi una notifica quando una conversione termina, senza tenere la finestra aperta.",
            Wrap = true,
            Dock = DockStyle.Fill,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Small,
            Margin = new Padding(0, 0, 0, 16)
        }, 0, 1);

        var trayButton = Theme.CreateButton("Riduci in tray  ·  notifiche attive", Theme.ButtonTone.Primary);
        trayButton.Dock = DockStyle.Fill;
        trayButton.Height = 42;
        trayButton.Margin = new Padding(0, 0, 0, 8);
        trayButton.DialogResult = DialogResult.OK;
        root.Controls.Add(trayButton, 0, 2);

        var exitButton = Theme.CreateButton("Esci definitivamente", Theme.ButtonTone.Neutral);
        exitButton.Dock = DockStyle.Fill;
        exitButton.Height = 42;
        exitButton.Margin = new Padding(0, 0, 0, 8);
        exitButton.DialogResult = DialogResult.Abort;
        root.Controls.Add(exitButton, 0, 3);

        _remember.Text = "Ricorda questa scelta (non chiedere più)";
        // Non preselezionata: «non chiedere più» è una scelta da fare di proposito,
        // perché poi la finestra si chiude in tray senza più domande.
        _remember.Checked = false;
        _remember.Dock = DockStyle.Fill;
        _remember.ForeColor = Theme.InkSecondary;
        _remember.Font = Theme.Fonts.Small;
        _remember.Margin = new Padding(0, 4, 0, 0);
        root.Controls.Add(_remember, 0, 4);

        // Larghezza fissa e altezza quanto il contenuto: la finestra si adatta al testo (e alla scala dello schermo)
        // senza altezze scritte a mano per le righe. Va fatto a finestra aperta, quando la scala è definitiva.
        Shown += (_, _) =>
        {
            var needed = root.GetPreferredSize(new Size(ClientSize.Width, 0)).Height;
            if (needed != ClientSize.Height)
            {
                ClientSize = new Size(ClientSize.Width, needed);
            }
        };

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
