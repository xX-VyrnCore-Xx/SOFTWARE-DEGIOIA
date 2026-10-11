namespace PresenzeXmlConverter;

/// <summary>
/// Finestra di avanzamento del download dell'aggiornamento. Non è chiudibile
/// dall'utente: viene chiusa dal codice al termine o in caso di errore.
/// </summary>
internal sealed class UpdateProgressDialog : Form
{
    private readonly Label _status = new();
    private readonly ProgressBar _bar = new();
    private readonly CancellationTokenSource _cancellation = new();

    /// <summary>Token da passare al download: annullato dal pulsante "Annulla".</summary>
    public CancellationToken Token => _cancellation.Token;

    public UpdateProgressDialog(string version)
    {
        Text = "Aggiornamento di PresenzeFlow";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ControlBox = false;
        ClientSize = new Size(460, 208);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Theme.StyleDialog(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20, 18, 20, 18),
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = $"Scaricamento di PresenzeFlow {version}",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Ink,
            Font = Theme.Fonts.CardTitle,
            Margin = Padding.Empty
        }, 0, 0);

        _status.Text = "Preparazione del download…";
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = Theme.MutedInk;
        _status.Font = Theme.Fonts.Small;
        _status.Margin = Padding.Empty;
        root.Controls.Add(_status, 0, 1);

        _bar.Dock = DockStyle.Fill;
        _bar.Minimum = 0;
        _bar.Maximum = 100;
        _bar.Style = ProgressBarStyle.Continuous;
        _bar.Margin = new Padding(0, 6, 0, 0);
        root.Controls.Add(_bar, 0, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        var cancel = Theme.CreateButton("Annulla", Theme.ButtonTone.Neutral);
        cancel.Size = new Size(120, 34);
        cancel.Click += (_, _) =>
        {
            cancel.Enabled = false;
            SetStatus("Annullamento in corso…");
            _cancellation.Cancel();
        };
        actions.Controls.Add(cancel);
        root.Controls.Add(actions, 0, 3);
    }

    public void SetProgress(int percent)
    {
        var value = Math.Clamp(percent, 0, 100);
        _bar.Value = value;
        SetStatus($"Scaricamento in corso… {value}%");
    }

    public void SetStatus(string text) => _status.Text = text;
}
