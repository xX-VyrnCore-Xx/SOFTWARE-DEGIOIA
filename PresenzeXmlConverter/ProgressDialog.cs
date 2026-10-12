namespace PresenzeXmlConverter;

/// <summary>
/// Finestra di avanzamento temizzata e riusabile: aggiornamenti dell'app,
/// download dell'installer di Ollama, download del modello AI locale.
/// Non è chiudibile dall'utente: la chiude il codice al termine o in caso di
/// errore, mentre il pulsante "Annulla" annulla l'operazione in corso.
/// </summary>
internal class ProgressDialog : Form
{
    private readonly Label _status = new();
    private readonly ProgressStrip _bar = new();
    private readonly CancellationTokenSource _cancellation = new();

    /// <summary>Token da passare al download: viene annullato da "Annulla".</summary>
    public CancellationToken Token => _cancellation.Token;

    public ProgressDialog(string title, string heading, string initialStatus)
    {
        Text = title;
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
            // Un numero di righe coerente con gli stili dichiarati: un conteggio
            // diverso farebbe crescere la tabella e sposterebbe i controlli.
            RowCount = 4,
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
            Text = heading,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Ink,
            Font = Theme.Fonts.CardTitle,
            Margin = Padding.Empty
        }, 0, 0);

        _status.Text = initialStatus;
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = Theme.MutedInk;
        _status.Font = Theme.Fonts.Small;
        _status.Margin = Padding.Empty;
        root.Controls.Add(_status, 0, 1);

        _bar.Dock = DockStyle.Top;
        _bar.Height = 18;
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

    /// <summary>Aggiorna la barra (0-100) e il testo di stato.</summary>
    public void SetProgress(int percent, string? status = null)
    {
        var value = Math.Clamp(percent, 0, 100);
        _bar.Value = value;
        _status.Text = status ?? $"Avanzamento… {value}%";
    }

    /// <summary>Cambia solo il testo di stato, senza toccare la barra.</summary>
    public void SetStatus(string text) => _status.Text = text;

    /// <summary>Segnala il completamento dell'operazione.</summary>
    public void SetCompleted(string text)
    {
        _bar.Value = 100;
        _status.Text = text;
    }
}
