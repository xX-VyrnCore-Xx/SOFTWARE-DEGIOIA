namespace PresenzeXmlConverter;

/// <summary>
/// Mostra le novità di una versione: usato sia per proporre un aggiornamento
/// disponibile sia per raccontare, dopo il riavvio, che cosa è cambiato.
/// Le note possono essere lunghe: sono disegnate con la palette dell'app e
/// scorrono con una barra temizzata, perché quella nativa di Windows resta
/// grigia anche dentro una finestra dipinta a mano.
/// </summary>
internal sealed class ReleaseNotesDialog : Form
{
    public ReleaseNotesDialog(string title, string subtitle, string notes, bool showUpdateButton)
    {
        var notesText = notes ?? string.Empty;

        Text = showUpdateButton ? "Aggiornamento disponibile" : "Novità di Vyrn Presence";
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
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
        var notesView = new NoteTextView
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            AccessibleName = showUpdateButton ? "Novità dell'aggiornamento" : "Novità di questa versione"
        };
        notesView.Value = notesText;
        notesPanel.Controls.Add(notesView);
        root.Controls.Add(notesPanel, 0, 1);

        // Il pulsante "Copia note" sta a sinistra e le azioni a destra: il testo
        // delle novità si può incollare altrove (una mail, un ticket) senza doverlo
        // selezionare a mano.
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var copy = Theme.CreateButton("Copia note", Theme.ButtonTone.Neutral);
        copy.AutoSize = false;
        copy.Size = new Size(140, 38);
        copy.Anchor = AnchorStyles.Left;
        copy.Margin = Padding.Empty;
        copy.Enabled = notesText.Length > 0;
        copy.Click += (_, _) => CopyNotes(copy, notesText);
        footer.Controls.Add(copy, 0, 0);

        var actions = new FlowLayoutPanel
        {
            // La colonna è AutoSize: il pannello deve dichiarare la propria
            // larghezza (AutoSize), altrimenti la colonna lo stringe e il pulsante
            // più a sinistra ("Più tardi") finisce tagliato fuori dal bordo.
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
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

        footer.Controls.Add(actions, 1, 0);
        root.Controls.Add(footer, 0, 2);
    }

    /// <summary>
    /// Copia le note negli appunti e lo conferma sul pulsante per qualche
    /// secondo: senza un riscontro l'utente non sa se la copia è avvenuta.
    /// </summary>
    private static void CopyNotes(Button button, string notes)
    {
        try
        {
            Clipboard.SetText(notes);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Appunti occupati da un altro programma: si riprova al prossimo clic.
            return;
        }

        var caption = button.Text;
        button.Text = "Note copiate";
        var restore = new System.Windows.Forms.Timer { Interval = 2000 };
        restore.Tick += (_, _) =>
        {
            restore.Stop();
            restore.Dispose();
            button.Text = caption;
        };
        button.HandleDestroyed += (_, _) =>
        {
            restore.Stop();
            restore.Dispose();
        };
        restore.Start();
    }
}
