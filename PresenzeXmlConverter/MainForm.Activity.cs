using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter;

/// <summary>
/// Registro delle attività: cosa è stato fatto con il programma (importazioni, anteprime, esportazioni, modifiche ai
/// codici e alle aziende, backup, aggiornamenti), con ricerca, filtro per tipo ed esportazione in CSV.
/// </summary>
internal sealed partial class MainForm
{
    private readonly TableLayoutPanel _activitySection = NewSection();
    private readonly ThemedGrid _activityGrid = new();
    private readonly RoundedField _activitySearch = new();
    private readonly ComboBox _activityKindFilter = new ThemedComboBox();
    private readonly Label _activitySummaryLabel = new();
    private readonly Button _activityExportButton = new ThemedButton();
    private readonly Button _activityClearButton = new ThemedButton();
    private IReadOnlyList<ActivityEntry> _activityEntries = Array.Empty<ActivityEntry>();

    /// <summary>
    /// Registra un'attività. Il registro è un di più: se il database non risponde l'operazione che si stava
    /// facendo non deve fallire.
    /// </summary>
    private void Audit(ActivityKind kind, string title, string detail = "")
    {
        try
        {
            _database.AddActivity(kind, title, detail);
            if (_navRail.SelectedIndex == SectionActivity)
            {
                RefreshActivity();
            }
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Nessun messaggio: il registro non è l'operazione.
        }
    }

    private Control BuildActivitySection()
    {
        var page = _activitySection;
        page.RowCount = 3;
        page.ColumnCount = 1;
        page.Padding = new Padding(0, 0, 4, 0);
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        page.Controls.Add(BuildSectionHeading(
            "Registro attività",
            "Cosa è stato fatto: importazioni, esportazioni, modifiche ai codici e alle aziende, backup e aggiornamenti."), 0, 0);

        var card = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.Surface,
            SurfaceBorder = Theme.Divider,
            Radius = 12,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(0, 0, 0, 8)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var filters = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Surface,
            Margin = Padding.Empty
        };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        filters.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _activitySearch.Dock = DockStyle.Fill;
        _activitySearch.Margin = new Padding(0, 0, 10, 6);
        _activitySearch.Placeholder = "Cerca nel registro…";
        _activitySearch.Input.AccessibleName = "Cerca nel registro delle attività";
        _activitySearch.TextChanged += (_, _) => ApplyActivityFilter();
        filters.Controls.Add(_activitySearch, 0, 0);

        Theme.StyleCombo(_activityKindFilter);
        _activityKindFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _activityKindFilter.Dock = DockStyle.Top;
        _activityKindFilter.Margin = new Padding(0, 2, 0, 0);
        _activityKindFilter.AccessibleName = "Tipo di attività";
        _activityKindFilter.Items.Add("Tutte le attività");
        foreach (var kind in Enum.GetValues<ActivityKind>())
        {
            _activityKindFilter.Items.Add(ActivityLog.Label(kind));
        }
        _activityKindFilter.SelectedIndex = 0;
        _activityKindFilter.SelectedIndexChanged += (_, _) => ApplyActivityFilter();
        filters.Controls.Add(_activityKindFilter, 1, 0);
        layout.Controls.Add(filters, 0, 0);

        _activitySummaryLabel.Dock = DockStyle.Fill;
        _activitySummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _activitySummaryLabel.ForeColor = Theme.InkSecondary;
        _activitySummaryLabel.Font = Theme.Fonts.SmallBold;
        _activitySummaryLabel.AutoEllipsis = true;
        _activitySummaryLabel.Margin = Padding.Empty;
        _activitySummaryLabel.Text = ActivityLog.Summarize([], DateTime.Now);
        layout.Controls.Add(_activitySummaryLabel, 0, 1);

        _activityGrid.ReadOnly = true;
        _activityGrid.UseRowSelection();
        _activityGrid.EmptyStateText = "Nessuna attività: importa un foglio o esporta un XML e comparirà qui.";
        _activityGrid.Columns.Add(CreateGridColumn("when", "Quando", 18, true));
        _activityGrid.Columns.Add(CreateGridColumn("kind", "Tipo", 15, true));
        _activityGrid.Columns.Add(CreateGridColumn("title", "Azione", 34, true));
        _activityGrid.Columns.Add(CreateGridColumn("detail", "Dettaglio", 33, true));
        layout.Controls.Add(_activityGrid, 0, 2);
        card.Controls.Add(layout);
        page.Controls.Add(card, 0, 1);

        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Canvas,
            Margin = Padding.Empty
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bar.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Tiny,
            AutoEllipsis = true,
            Margin = Padding.Empty,
            Text = $"Si conservano le ultime {ActivityLog.MaximumEntries:N0} voci."
        }, 0, 0);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        _activityExportButton.Text = "Esporta CSV…";
        Theme.StyleButton(_activityExportButton, Theme.ButtonTone.Primary, compact: true);
        _activityExportButton.Size = new Size(132, 34);
        _activityExportButton.Margin = new Padding(8, 0, 0, 0);
        _activityExportButton.Click += (_, _) => ExportActivityCsv();
        Describe(_activityExportButton, "Esporta CSV", "Salva le voci mostrate in un file CSV da aprire con Excel.");

        _activityClearButton.Text = "Svuota registro";
        Theme.StyleButton(_activityClearButton, Theme.ButtonTone.Neutral, compact: true);
        _activityClearButton.Size = new Size(132, 34);
        _activityClearButton.Margin = new Padding(8, 0, 0, 0);
        _activityClearButton.Click += (_, _) => ClearActivityLog();
        Describe(_activityClearButton, "Svuota registro", "Elimina tutte le voci del registro; non tocca codici, aziende ed esportazioni.");

        actions.Controls.Add(_activityExportButton);
        actions.Controls.Add(_activityClearButton);
        bar.Controls.Add(actions, 1, 0);
        page.Controls.Add(bar, 0, 2);
        UpdateActivityButtons();
        return page;
    }

    private void RefreshActivity()
    {
        try
        {
            _activityEntries = _database.GetActivities();
        }
        catch (SqliteException exception)
        {
            ShowError("Impossibile leggere il registro delle attività", exception);
            return;
        }
        ApplyActivityFilter();
    }

    private void ApplyActivityFilter()
    {
        ActivityKind? kind = _activityKindFilter.SelectedIndex > 0
            ? Enum.GetValues<ActivityKind>()[_activityKindFilter.SelectedIndex - 1]
            : null;
        var shown = ActivityLog.Filter(_activityEntries, _activitySearch.Value, kind);
        var now = DateTime.Now;
        _activityGrid.Rows.Clear();
        foreach (var entry in shown)
        {
            var index = _activityGrid.Rows.Add(
                ActivityLog.DescribeWhen(entry.LocalTime, now),
                ActivityLog.Label(entry.Kind),
                entry.Title,
                entry.Detail);
            _activityGrid.Rows[index].Tag = entry;
        }
        var summary = ActivityLog.Summarize(shown, now);
        _activitySummaryLabel.Text = shown.Count == _activityEntries.Count || _activityEntries.Count == 0
            ? summary
            : $"{shown.Count:N0} di {_activityEntries.Count:N0} · {summary}";
        UpdateActivityButtons();
    }

    private void UpdateActivityButtons()
    {
        _activityExportButton.Enabled = _activityGrid.Rows.Count > 0;
        _activityClearButton.Enabled = _activityEntries.Count > 0;
    }

    private void ExportActivityCsv()
    {
        var shown = _activityGrid.Rows.Cast<DataGridViewRow>()
            .Select(row => row.Tag as ActivityEntry)
            .OfType<ActivityEntry>()
            .ToList();
        if (shown.Count == 0)
        {
            return;
        }
        using var picker = new SaveFileDialog
        {
            Title = "Esporta il registro delle attività",
            Filter = "File CSV (*.csv)|*.csv",
            AddExtension = true,
            DefaultExt = "csv",
            FileName = $"registro-attivita-{DateTime.Now:yyyyMMdd}.csv",
            OverwritePrompt = true
        };
        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            File.WriteAllText(picker.FileName, ActivityLog.ToCsv(shown), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            SetStatus($"Registro esportato: {picker.FileName}", false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowError("Esportazione del registro non riuscita", exception);
        }
    }

    private void ClearActivityLog()
    {
        if (MessageBox.Show(
                this,
                "Vuoi eliminare tutte le voci del registro delle attività?" + Environment.NewLine +
                "Codici, aziende ed esportazioni non vengono toccati.",
                "Svuota registro",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }
        try
        {
            _database.ClearActivities();
            RefreshActivity();
            SetStatus("Registro delle attività svuotato.", false);
        }
        catch (SqliteException exception)
        {
            ShowError("Impossibile svuotare il registro", exception);
        }
    }
}
