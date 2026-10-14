namespace PresenzeXmlConverter;

internal sealed class ColumnMappingDialog : Form
{
    private sealed record ColumnOption(int Index, string Header)
    {
        public override string ToString() => Index < 0 ? Header : $"Colonna {Index + 1}: {Header}";
    }

    private readonly ComboBox _employee = new();
    private readonly ComboBox _policy = new();
    private readonly ComboBox _start = new();
    private readonly ComboBox _end = new();
    private readonly ComboBox _duration = new();

    public ColumnMappingDialog(IReadOnlyList<string> headers, ColumnSelection? suggested)
    {
        Text = "Associa le colonne del file Excel";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(700, 396);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Theme.StyleDialog(this);

        // Layout a righe fisse: etichetta e casella non possono mai incolonnarsi
        // l'una sull'altra, qualunque sia il DPI o la lunghezza del testo.
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24, 18, 24, 18),
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Verifica le colonne da utilizzare per la conversione",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Ink,
            Font = Theme.Fonts.CardTitle,
            Margin = Padding.Empty
        }, 0, 0);

        var fields = new (string Label, ComboBox Box, int Suggested)[]
        {
            ("Nome dipendente", _employee, suggested?.Employee ?? -1),
            ("Giustificativo (Policy)", _policy, suggested?.Policy ?? -1),
            ("Data iniziale", _start, suggested?.StartDate ?? -1),
            ("Data finale", _end, suggested?.EndDate ?? -1),
            ("Durata (ore)", _duration, suggested?.Duration ?? -1)
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = fields.Length,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var _ in fields)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        }

        for (var row = 0; row < fields.Length; row++)
        {
            var field = fields[row];
            grid.Controls.Add(new Label
            {
                Text = field.Label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.InkSecondary,
                Font = Theme.Fonts.Ui,
                Margin = new Padding(0, 0, 12, 0)
            }, 0, row);

            field.Box.DropDownStyle = ComboBoxStyle.DropDownList;
            field.Box.Dock = DockStyle.Fill;
            field.Box.Margin = new Padding(0, 6, 0, 6);
            Theme.StyleCombo(field.Box);
            field.Box.Items.Add(new ColumnOption(-1, "Seleziona una colonna"));
            foreach (var header in headers.Select((value, index) =>
                         new ColumnOption(index, string.IsNullOrWhiteSpace(value) ? "(senza intestazione)" : value)))
            {
                field.Box.Items.Add(header);
            }
            field.Box.SelectedIndex = field.Suggested >= 0 && field.Suggested < headers.Count
                ? field.Suggested + 1
                : 0;
            grid.Controls.Add(field.Box, 1, row);
        }
        root.Controls.Add(grid, 0, 1);

        root.Controls.Add(new Label
        {
            Text = "Ogni campo richiede una colonna diversa del foglio Excel.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Small,
            Margin = Padding.Empty
        }, 0, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        var confirm = Theme.CreateButton("Conferma colonne", Theme.ButtonTone.Primary);
        confirm.Size = new Size(170, 36);
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

        // Chiudere senza confermare: serve un tasto visibile (e un pulsante
        // Annulla) per abbandonare la finestra senza toccare nulla.
        var cancel = Theme.CreateButton("Annulla", Theme.ButtonTone.Neutral);
        cancel.Size = new Size(120, 36);
        cancel.DialogResult = DialogResult.Cancel;

        actions.Controls.Add(confirm);
        actions.Controls.Add(cancel);
        root.Controls.Add(actions, 0, 3);
        AcceptButton = confirm;
        CancelButton = cancel;
        // Senza questo il primo menu parte con il testo evidenziato di blu di sistema.
        Shown += (_, _) => ActiveControl = confirm;
    }

    public ColumnSelection? Selection { get; private set; }
}
