using System.Drawing.Drawing2D;

namespace PresenzeXmlConverter;

/// <summary>
/// Finestra di avanzamento dell'aggiornamento: i quattro passi (scaricamento, controllo del file, verifica della
/// firma, installazione) con quello in corso evidenziato, la barra e, durante il download, dimensione, velocità e
/// tempo residuo. Non è chiudibile dall'utente: la chiude il codice, «Annulla» interrompe l'operazione.
/// </summary>
internal sealed class UpdateProgressDialog : Form
{
    private readonly StepList _steps = new();
    private readonly Label _detail = new();
    private readonly ProgressStrip _bar = new();
    private readonly Button _cancel;
    private readonly CancellationTokenSource _cancellation = new();

    /// <summary>Token da passare al download: viene annullato da «Annulla».</summary>
    public CancellationToken Token => _cancellation.Token;

    public UpdateProgressDialog(string version)
    {
        Text = "Aggiornamento di Vyrn Presence";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ControlBox = false;
        ClientSize = new Size(460, 292);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Theme.StyleDialog(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            // Righe coerenti con gli stili dichiarati: un conteggio diverso sposterebbe i controlli.
            RowCount = 5,
            Padding = new Padding(20, 18, 20, 18),
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = $"Aggiornamento a Vyrn Presence {version}",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Ink,
            Font = Theme.Fonts.CardTitle,
            Margin = Padding.Empty
        }, 0, 0);

        _steps.Dock = DockStyle.Fill;
        _steps.Margin = Padding.Empty;
        root.Controls.Add(_steps, 0, 1);

        _bar.Dock = DockStyle.Fill;
        _bar.Margin = new Padding(0, 4, 0, 4);
        root.Controls.Add(_bar, 0, 2);

        _detail.Text = "Preparazione del download…";
        _detail.Dock = DockStyle.Top;
        _detail.Height = 24;
        _detail.TextAlign = ContentAlignment.MiddleLeft;
        _detail.ForeColor = Theme.MutedInk;
        _detail.Font = Theme.Fonts.Small;
        _detail.AutoEllipsis = true;
        _detail.Margin = Padding.Empty;
        root.Controls.Add(_detail, 0, 3);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        _cancel = Theme.CreateButton("Annulla", Theme.ButtonTone.Neutral);
        _cancel.Size = new Size(120, 34);
        _cancel.Click += (_, _) =>
        {
            _cancel.Enabled = false;
            SetDetail("Annullamento in corso…");
            _cancellation.Cancel();
        };
        actions.Controls.Add(_cancel);
        root.Controls.Add(actions, 0, 4);
    }

    /// <summary>Passa al passo indicato; da «Installazione» in poi non si può più annullare.</summary>
    public void SetStep(UpdateStep step)
    {
        _steps.Current = step;
        if (step >= UpdateStep.Install)
        {
            _cancel.Enabled = false;
        }
        if (step > UpdateStep.Download)
        {
            _bar.Value = 100;
        }
        _detail.Text = step switch
        {
            UpdateStep.Checksum => "Controllo che il file sia identico a quello pubblicato…",
            UpdateStep.Signature => "Verifica della firma digitale con Windows…",
            UpdateStep.Install => "Il programma si chiude e si riapre da solo…",
            _ => _detail.Text
        };
    }

    /// <summary>Aggiorna barra e dettagli del download.</summary>
    public void SetTransfer(TransferMeter meter)
    {
        _bar.Value = meter.Percent;
        _detail.Text = $"{meter.Percent}% · {meter.Describe()}";
    }

    public void SetDetail(string text) => _detail.Text = text;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancellation.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>Elenco dei passi con un cerchio per stato: verde con spunta, azzurro in corso, grigio in attesa.</summary>
    private sealed class StepList : Control
    {
        private const int RowHeight = 28;
        private UpdateStep _current = UpdateStep.Download;

        public StepList()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Theme.Canvas;
            AccessibleRole = AccessibleRole.List;
            AccessibleName = "Passi dell'aggiornamento";
        }

        public UpdateStep Current
        {
            get => _current;
            set
            {
                _current = value;
                AccessibleDescription = $"Passo in corso: {UpdateSteps.Label(value)}";
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Canvas);
            var y = 0;
            foreach (var step in UpdateSteps.All)
            {
                var state = UpdateSteps.StateOf(step, _current);
                var dot = new Rectangle(2, y + (RowHeight - 18) / 2, 18, 18);
                DrawDot(g, dot, state);
                TextRenderer.DrawText(
                    g,
                    UpdateSteps.Label(step),
                    state == StepState.Active ? Theme.Fonts.SmallBold : Theme.Fonts.Small,
                    new Rectangle(30, y, Math.Max(0, Width - 30), RowHeight),
                    state == StepState.Pending ? Theme.MutedInk : Theme.Ink,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                y += RowHeight;
            }
        }

        private static void DrawDot(Graphics g, Rectangle dot, StepState state)
        {
            switch (state)
            {
                case StepState.Done:
                    using (var fill = new SolidBrush(Theme.Success))
                    {
                        g.FillEllipse(fill, dot);
                    }
                    Glyphs.Draw(g, Rectangle.Inflate(dot, -3, -3), Glyph.Check, Color.White, 2f);
                    break;
                case StepState.Active:
                    using (var fill = new SolidBrush(Theme.BrandSoft))
                    using (var ring = new Pen(Theme.Brand, 2f))
                    using (var core = new SolidBrush(Theme.Brand))
                    {
                        g.FillEllipse(fill, dot);
                        g.DrawEllipse(ring, Rectangle.Inflate(dot, -1, -1));
                        g.FillEllipse(core, Rectangle.Inflate(dot, -6, -6));
                    }
                    break;
                case StepState.Failed:
                    using (var fill = new SolidBrush(Theme.Danger))
                    {
                        g.FillEllipse(fill, dot);
                    }
                    break;
                default:
                    using (var ring = new Pen(Theme.DividerStrong, 1.5f))
                    {
                        g.DrawEllipse(ring, Rectangle.Inflate(dot, -1, -1));
                    }
                    break;
            }
        }
    }
}
