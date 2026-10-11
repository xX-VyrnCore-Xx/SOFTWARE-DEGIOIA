namespace PresenzeXmlConverter;

internal sealed class ColumnMappingDialog : Form
{
    private sealed record ColumnOption(int Index, string Header)
    {
        public override string ToString() => $"Colonna {Index + 1}: {Header}";
    }

    private readonly ComboBox _employee = new();
    private readonly ComboBox _policy = new();
    private readonly ComboBox _start = new();
    private readonly ComboBox _end = new();
    private readonly ComboBox _duration = new();
    private bool _designScaled;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Geometria a 96 DPI scalata sul DPI reale (i font crescono già da soli):
        // niente testo più largo della sua casella anche al 125-150%.
        var factor = DeviceDpi / 96f;
        if (factor > 1.01f && !_designScaled)
        {
            _designScaled = true;
            ClientSize = new Size((int)(ClientSize.Width * factor), (int)(ClientSize.Height * factor));
            MainForm.ScaleLayoutStyles(this, factor);
        }
    }

    public ColumnMappingDialog(IReadOnlyList<string> headers, ColumnSelection? suggested)
    {
        Text = "Associa le colonne del file Excel";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(690, 365);
        Font = new Font("Segoe UI", 10);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var title = new Label
        {
            Text = "Verifica le colonne da utilizzare per la conversione",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 20)
        };
        Controls.Add(title);

        var fields = new (string Label, ComboBox Box, int Suggested)[]
        {
            ("Nome dipendente", _employee, suggested?.Employee ?? -1),
            ("Giustificativo (Policy)", _policy, suggested?.Policy ?? -1),
            ("Data iniziale", _start, suggested?.StartDate ?? -1),
            ("Data finale", _end, suggested?.EndDate ?? -1),
            ("Durata (ore)", _duration, suggested?.Duration ?? -1)
        };

        for (var row = 0; row < fields.Length; row++)
        {
            var field = fields[row];
            Controls.Add(new Label
            {
                Text = field.Label,
                AutoSize = true,
                Location = new Point(28, 68 + row * 48)
            });
            field.Box.DropDownStyle = ComboBoxStyle.DropDownList;
            field.Box.Location = new Point(215, 63 + row * 48);
            field.Box.Size = new Size(440, 30);
            field.Box.Items.Add(new ColumnOption(-1, "Seleziona una colonna"));
            foreach (var header in headers.Select((value, index) => new ColumnOption(index, string.IsNullOrWhiteSpace(value) ? "(senza intestazione)" : value)))
            {
                field.Box.Items.Add(header);
            }
            field.Box.SelectedIndex = field.Suggested >= 0 && field.Suggested < headers.Count
                ? field.Suggested + 1
                : 0;
            Controls.Add(field.Box);
        }

        var confirm = new Button
        {
            Text = "Conferma colonne",
            DialogResult = DialogResult.None,
            Location = new Point(470, 315),
            Size = new Size(185, 34),
            BackColor = Color.FromArgb(19, 97, 160),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        confirm.Click += (_, _) =>
        {
            var indexes = new[] { _employee, _policy, _start, _end, _duration }
                .Select(combo => (combo.SelectedItem as ColumnOption)?.Index ?? -1)
                .ToArray();
            if (indexes.Any(index => index < 0) || indexes.Distinct().Count() != indexes.Length)
            {
                MessageBox.Show(
                    this,
                    "Seleziona una colonna diversa per ogni campo richiesto.",
                    "Associazione non valida",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            Selection = new ColumnSelection(indexes[0], indexes[1], indexes[2], indexes[3], indexes[4]);
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(confirm);
        AcceptButton = confirm;

        // Chiudere senza confermare: prima non esisteva un modo visibile
        // (né tasto né pulsante Annulla) per abbandonare la finestra.
        var cancel = new Button
        {
            Text = "Annulla",
            DialogResult = DialogResult.Cancel,
            Location = new Point(24, 315),
            Size = new Size(150, 34),
            BackColor = Color.FromArgb(241, 245, 250),
            ForeColor = Color.FromArgb(55, 75, 100),
            FlatStyle = FlatStyle.Flat
        };
        cancel.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235);
        Controls.Add(cancel);
        CancelButton = cancel;
    }

    public ColumnSelection? Selection { get; private set; }
}
