namespace PresenzeXmlConverter;

/// <summary>
/// Mostra le novità di una versione: usato sia per proporre un aggiornamento
/// disponibile sia per raccontare, dopo il riavvio, che cosa è cambiato.
/// </summary>
internal sealed class ReleaseNotesDialog : Form
{
    public ReleaseNotesDialog(string title, string subtitle, string notes, bool showUpdateButton)
    {
        Text = showUpdateButton ? "Aggiornamento disponibile" : "Novità di PresenzeFlow";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(560, 420);
        Size = new Size(760, 600);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Theme.StyleDialog(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24, 18, 24, 18),
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        Controls.Add(root);

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 6));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        heading.Controls.Add(new AccentBar { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 0);

        heading.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Ink,
            Font = Theme.Fonts.CardTitle,
            Margin = Padding.Empty
        }, 0, 1);

        heading.Controls.Add(new Label
        {
            Text = subtitle,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Small,
            Margin = Padding.Empty
        }, 0, 2);
        root.Controls.Add(heading, 0, 0);

        var notesPanel = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.FieldFill,
            SurfaceBorder = Theme.DividerStrong,
            Radius = 10,
            Padding = new Padding(12),
            Margin = new Padding(0, 4, 0, 4)
        };
        var notesText = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.FieldFill,
            ForeColor = Theme.InkSecondary,
            Font = Theme.Fonts.Small,
            WordWrap = true,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Text = notes
        };
        notesText.Select(0, 0);
        notesPanel.Controls.Add(notesText);
        root.Controls.Add(notesPanel, 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };

        if (showUpdateButton)
        {
            var update = Theme.CreateButton("Aggiorna ora", Theme.ButtonTone.Primary);
            update.Size = new Size(160, 38);
            update.Click += (_, _) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };
            var later = Theme.CreateButton("Più tardi", Theme.ButtonTone.Neutral);
            later.Size = new Size(130, 38);
            later.DialogResult = DialogResult.Cancel;
            actions.Controls.Add(update);
            actions.Controls.Add(later);
            AcceptButton = update;
            CancelButton = later;
        }
        else
        {
            var close = Theme.CreateButton("Chiudi", Theme.ButtonTone.Primary);
            close.Size = new Size(130, 38);
            close.DialogResult = DialogResult.OK;
            actions.Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;
        }

        root.Controls.Add(actions, 0, 2);
    }
}
