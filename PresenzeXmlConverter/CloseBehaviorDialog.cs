namespace PresenzeXmlConverter;

/// <summary>
/// Chiede una sola volta come comportarsi alla chiusura: esci definitivamente
/// o resta in tray per ricevere le notifiche di fine conversione.
/// </summary>
internal sealed class CloseBehaviorDialog : Form
{
    private readonly CheckBox _remember = new();
    private bool _designScaled;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var factor = DeviceDpi / 96f;
        if (factor > 1.01f && !_designScaled)
        {
            _designScaled = true;
            ClientSize = new Size((int)(ClientSize.Width * factor), (int)(ClientSize.Height * factor));
            MainForm.ScaleLayoutStyles(this, factor);
        }
    }

    public CloseBehaviorDialog()
    {
        Text = "Chiudi PresenzeFlow";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(468, 236);
        Font = new Font("Segoe UI", 10);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;

        Controls.Add(new Label
        {
            Text = "Come vuoi chiudere il programma?",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 26, 40),
            AutoSize = true,
            Location = new Point(22, 18)
        });
        Controls.Add(new Label
        {
            Text = "Se resti in background ricevi una notifica quando una conversione termina,\r\nsenza tenere la finestra aperta.",
            Font = new Font("Segoe UI", 9.2f),
            ForeColor = Color.FromArgb(103, 120, 142),
            AutoSize = true,
            Location = new Point(22, 56)
        });

        var trayButton = new Button
        {
            Text = "Riduci in tray (notifiche attive)",
            Size = new Size(424, 42),
            Location = new Point(22, 104),
            BackColor = Color.FromArgb(59, 130, 246),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK,
            Font = new Font("Segoe UI", 9.7f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        trayButton.FlatAppearance.BorderSize = 0;
        Controls.Add(trayButton);

        var exitButton = new Button
        {
            Text = "Esci definitivamente",
            Size = new Size(424, 38),
            Location = new Point(22, 152),
            BackColor = Color.FromArgb(241, 245, 250),
            ForeColor = Color.FromArgb(55, 75, 100),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Abort,
            Cursor = Cursors.Hand
        };
        exitButton.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235);
        Controls.Add(exitButton);

        _remember.Text = "Ricorda questa scelta (non chiedere più)";
        _remember.AutoSize = true;
        _remember.Checked = true;
        _remember.Location = new Point(24, 202);
        _remember.Font = new Font("Segoe UI", 8.8f);
        _remember.ForeColor = Color.FromArgb(83, 100, 120);
        Controls.Add(_remember);

        AcceptButton = trayButton;
        CancelButton = exitButton;
    }

    /// <summary>"tray" per restare in background, "exit" per chiudere definitivamente.</summary>
    public string SelectedBehavior => DialogResult == DialogResult.Abort ? "exit" : "tray";

    public bool RememberChoice => _remember.Checked;
}
