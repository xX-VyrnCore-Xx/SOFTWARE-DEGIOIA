namespace PresenzeXmlConverter;

/// <summary>
/// Scheda "Controlli": mostra in un unico elenco tutto ciò che va verificato prima
/// di esportare. I controlli girano sempre, in locale e senza rete.
/// </summary>
internal sealed partial class MainForm
{
    private const int MaximumFindingsShown = 200;

    private readonly ThemedGrid _findingsGrid = new();
    private readonly Label _findingsSummaryLabel = new();
    private readonly Button _copyFindingsButton = new ThemedButton();
    private IReadOnlyList<string> _sourceProblems = Array.Empty<string>();
    private IReadOnlyList<string> _generationWarnings = Array.Empty<string>();
    private IReadOnlyList<MovementFinding> _findings = Array.Empty<MovementFinding>();

    private int ErrorCount => _findings.Count(finding => finding.Severity == MovementSeverity.Error);

    private int WarningCount => _findings.Count(finding => finding.Severity == MovementSeverity.Warning);

    /// <summary>Scheda dei controlli: riepilogo, elenco dei problemi e azioni.</summary>
    private Control BuildReviewPanel()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Surface,
            Padding = new Padding(7),
            Margin = Padding.Empty
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // La fascia dei comandi è alta quanto serve ai pulsanti (30 px) più la
        // spaziatura interna e il margine: con una riga più bassa il bordo
        // inferiore dei due pulsanti veniva tagliato di 3 px.
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        var toolbar = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.Sunken,
            SurfaceBorder = Theme.Divider,
            Radius = 10,
            Padding = new Padding(12, 5, 12, 5),
            Margin = new Padding(0, 0, 0, 6)
        };
        var toolbarLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Sunken,
            Margin = Padding.Empty
        };
        toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _findingsSummaryLabel.Dock = DockStyle.Fill;
        _findingsSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _findingsSummaryLabel.AutoEllipsis = true;
        _findingsSummaryLabel.Font = Theme.Fonts.SmallBold;
        _findingsSummaryLabel.ForeColor = Theme.InkSecondary;
        _findingsSummaryLabel.Margin = Padding.Empty;
        _findingsSummaryLabel.Text = "Nessun movimento generato: importa un file e crea l’anteprima.";
        toolbarLayout.Controls.Add(_findingsSummaryLabel, 0, 0);

        var actions = new FlowLayoutPanel
        {
            // Colonna AutoSize: il pannello dichiara la propria larghezza, altrimenti
            // la colonna lo stringe e i pulsanti più a sinistra vengono tagliati.
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, 1, 0, 0)
        };

        _copyFindingsButton.Text = "Copia elenco";
        Theme.StyleButton(_copyFindingsButton, Theme.ButtonTone.Neutral, compact: true);
        _copyFindingsButton.Size = new Size(120, 30);
        _copyFindingsButton.Margin = new Padding(6, 0, 0, 0);
        _copyFindingsButton.Click += (_, _) => CopyFindingsToClipboard();

        actions.Controls.Add(_copyFindingsButton);
        toolbarLayout.Controls.Add(actions, 1, 0);
        toolbar.Controls.Add(toolbarLayout);
        page.Controls.Add(toolbar, 0, 0);

        _findingsGrid.ReadOnly = true;
        _findingsGrid.EmptyStateText =
            "Nessuna segnalazione: importa un foglio Excel e genera l'anteprima per controllare i movimenti.";
        _findingsGrid.Columns.Add(CreateGridColumn("severity", "Gravità", 22, true));
        _findingsGrid.Columns.Add(CreateGridColumn("message", "Segnalazione", 78, true));
        _findingsGrid.CellFormatting += FormatFindingCell;
        _findingsGrid.CellDoubleClick += (_, args) => FocusMovementForFinding(args.RowIndex);
        page.Controls.Add(_findingsGrid, 0, 1);

        var hint = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Doppio clic su una segnalazione per raggiungere il movimento in anteprima. " +
                   "I controlli automatici si aggiornano a ogni modifica della griglia.",
            Font = Theme.Fonts.Tiny,
            ForeColor = Theme.MutedInk,
            AutoEllipsis = true,
            Margin = new Padding(2, 4, 0, 0)
        };
        page.Controls.Add(hint, 0, 2);
        SetReviewButtonsEnabled();
        return page;
    }

    /// <summary>
    /// Controlli deterministici sui movimenti: righe non importate dal foglio,
    /// regole sui movimenti generati e confronto con le ore dichiarate. Vengono
    /// sempre eseguiti.
    /// </summary>
    private void RunDeterministicChecks(IReadOnlyList<DailyMovement> movements)
    {
        var findings = new List<MovementFinding>();
        findings.AddRange(_sourceProblems
            .Take(40)
            .Select(problem => new MovementFinding(MovementSeverity.Warning, $"Foglio Excel · {problem}")));
        // Avvisi nati durante la generazione (richieste escluse, sovrapposizioni,
        // giornate oltre la standard): contengono il numero di riga del foglio e
        // non devono andare persi, altrimenti una richiesta esclusa resta invisibile.
        findings.AddRange(_generationWarnings
            .Take(40)
            .Select(warning => new MovementFinding(MovementSeverity.Warning, $"Conversione · {warning}")));

        if (movements.Count == 0)
        {
            findings.Add(new MovementFinding(
                MovementSeverity.Error,
                "Non è stato generato alcun movimento: controlla date, durata e periodo del foglio."));
        }
        else
        {
            var policies = SafeMappings(() => _database.GetPolicyMappings());
            var employees = SafeMappings(() => _database.GetEmployeeMappings());
            findings.AddRange(MovementChecks.Review(
                movements,
                policies,
                _requests.Select(request => request.EmployeeName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                _inspection?.PeriodStart,
                _inspection?.PeriodEnd,
                _generationOptions.HoursPerDay,
                // Con il rilevamento automatico il sabato può comparire quando serve:
                // non va segnalato come anomalia.
                _generationOptions.IncludeSaturdays || _generationOptions.AutoDetect));
            findings.AddRange(MovementChecks.CompareWithSource(movements, _requests));
            findings.AddRange(MovementChecks.CheckMonthlyHours(
                movements,
                _generationOptions,
                SafeProfiles(() => _database.GetEmployeeProfiles()),
                _inspection?.PeriodStart,
                _inspection?.PeriodEnd));
        }

        _findings = findings
            .OrderBy(finding => finding.Severity)
            .Take(MaximumFindingsShown)
            .ToArray();
        _hasWarnings = WarningCount + ErrorCount > 0;
        RefreshFindings();
        UpdateWorkflowSummary();
    }

    /// <summary>Segnalazioni provenienti dalla lettura del foglio (righe scartate).</summary>
    private void SetSourceProblems(IReadOnlyList<string> problems)
    {
        _sourceProblems = problems;
        _generationWarnings = Array.Empty<string>();
    }

    /// <summary>Avvisi prodotti dalla generazione: righe escluse, sovrapposizioni, giornate piene.</summary>
    private void SetGenerationWarnings(IReadOnlyList<string> warnings) => _generationWarnings = warnings;

    private void RefreshFindings()
    {
        _findingsGrid.Rows.Clear();
        foreach (var finding in _findings)
        {
            var index = _findingsGrid.Rows.Add(finding.Label, finding.Message);
            _findingsGrid.Rows[index].Tag = finding;
        }

        var (errors, warnings, notes) = MovementChecks.CountBySeverity(_findings);
        _findingsSummaryLabel.Text = _findings.Count == 0
            ? "Nessun problema rilevato nei movimenti e nel foglio importato."
            : warnings + errors == 0
                ? $"{notes:N0} note informative · nessun problema bloccante"
                : $"{errors:N0} errori · {warnings:N0} attenzioni" +
                  (notes > 0 ? $" · {notes:N0} note" : string.Empty);
        _findingsSummaryLabel.ForeColor = errors > 0
            ? Theme.DangerInk
            : warnings > 0 ? Theme.WarningInk : Theme.SuccessInk;
        _previewTabs.SetPageText(
            ReviewTabIndex,
            _findings.Count == 0 ? "Controlli" : $"Controlli  ·  {_findings.Count:N0}");
        SetReviewButtonsEnabled();
    }

    private void SetReviewButtonsEnabled()
    {
        _copyFindingsButton.Enabled = !_isBusy && _findings.Count > 0;
    }

    private static void FormatFindingCell(object? sender, DataGridViewCellFormattingEventArgs args)
    {
        if (sender is not DataGridView grid ||
            args.RowIndex < 0 ||
            args.RowIndex >= grid.Rows.Count ||
            args.ColumnIndex < 0 ||
            args.ColumnIndex >= grid.Columns.Count ||
            grid.Rows[args.RowIndex].Tag is not MovementFinding finding)
        {
            return;
        }
        var style = args.CellStyle;
        if (style is null)
        {
            return;
        }
        var isSeverityColumn = grid.Columns[args.ColumnIndex].Name == "severity";
        style.ForeColor = finding.Severity switch
        {
            MovementSeverity.Error => Theme.DangerInk,
            MovementSeverity.Warning => Theme.WarningInk,
            _ => Theme.MutedInk
        };
        style.SelectionForeColor = Theme.Ink;
        style.Font = isSeverityColumn ? Theme.Fonts.SmallBold : Theme.Fonts.Grid;
    }

    /// <summary>Doppio clic su una segnalazione: porta alla riga corrispondente nell'anteprima.</summary>
    private void FocusMovementForFinding(int rowIndex)
    {
        if (rowIndex < 0 ||
            rowIndex >= _findingsGrid.Rows.Count ||
            _findingsGrid.Rows[rowIndex].Tag is not MovementFinding finding)
        {
            return;
        }

        // Le segnalazioni che citano dipendente e data sono quelle su cui si può
        // intervenire: si cerca il movimento corrispondente nella griglia.
        var dateMatch = System.Text.RegularExpressions.Regex.Match(
            finding.Message,
            @"\d{2}/\d{2}/\d{4}");
        DateTime? date = dateMatch.Success &&
                          DateTime.TryParseExact(
                              dateMatch.Value,
                              "dd/MM/yyyy",
                              System.Globalization.CultureInfo.InvariantCulture,
                              System.Globalization.DateTimeStyles.None,
                              out var parsed)
            ? parsed
            : null;
        var employee = _movements
            .Select(movement => movement.EmployeeName)
            .FirstOrDefault(name =>
                name.Length > 0 &&
                finding.Message.Contains(name, StringComparison.CurrentCultureIgnoreCase));
        if (employee is null)
        {
            return;
        }

        foreach (DataGridViewRow row in _movementsGrid.Rows)
        {
            if (!string.Equals(CellText(row, "employee"), employee, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }
            if (date is not null && CellText(row, "date") != date.Value.ToString("dd/MM/yyyy"))
            {
                continue;
            }
            _previewTabs.SelectedIndex = 0;
            row.Visible = true;
            _movementsGrid.ClearSelection();
            row.Selected = true;
            _movementsGrid.CurrentCell = row.Cells["employee"];
            return;
        }
    }

    private void CopyFindingsToClipboard()
    {
        if (_findings.Count == 0)
        {
            return;
        }
        var text = string.Join(
            Environment.NewLine,
            _findings.Select(finding => $"[{finding.Label}] {finding.Message}"));
        try
        {
            Clipboard.SetText(text);
            SetStatus("●   Elenco dei controlli copiato negli appunti", false);
            Notify(ToastKind.Info, "Copiato", "L'elenco dei controlli è negli appunti.");
        }
        catch (System.Runtime.InteropServices.ExternalException exception)
        {
            ShowError("Copia dei controlli non riuscita", exception);
        }
    }

    /// <summary>Azzera i controlli quando si importa un nuovo file.</summary>
    private void ResetReview()
    {
        _findings = Array.Empty<MovementFinding>();
        _generationWarnings = Array.Empty<string>();
        _findingsGrid.Rows.Clear();
        _findingsSummaryLabel.Text = "Nessun movimento generato: importa un file e crea l’anteprima.";
        _findingsSummaryLabel.ForeColor = Theme.InkSecondary;
        _previewTabs.SetPageText(ReviewTabIndex, "Controlli");
        SetReviewButtonsEnabled();
    }

    /// <summary>Le mappature vivono nel database locale: un errore non deve bloccare i controlli.</summary>
    private static IReadOnlyDictionary<string, string> SafeMappings(Func<Dictionary<string, string>> read)
    {
        try
        {
            return read();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyDictionary<string, EmployeeProfile> SafeProfiles(Func<Dictionary<string, EmployeeProfile>> read)
    {
        try
        {
            return read();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            return new Dictionary<string, EmployeeProfile>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
