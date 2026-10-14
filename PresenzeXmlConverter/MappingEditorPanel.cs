namespace PresenzeXmlConverter;

/// <summary>
/// Editor degli abbinamenti nome → codice PAGHE e Policy → giustificativo, in
/// due schede. Era il contenuto del solo dialogo "Codici PAGHE": ora è un
/// pannello riusabile, così la stessa identica schermata si apre sia come
/// finestra sia come sezione della finestra principale, senza due copie della
/// stessa logica di validazione.
///
/// Il pannello non salva nulla da sé: compila le proprie mappe con
/// <see cref="TrySave"/> e chi lo ospita decide dove scriverle (database locale).
/// </summary>
internal sealed class MappingEditorPanel : Panel
{
    private readonly ThemedTabControl _tabs = new();
    private readonly ThemedGrid _employeesGrid = CreateGrid("Dipendente / nome Excel", "Codice ufficiale PAGHE");
    private readonly ThemedGrid _policiesGrid = CreateGrid(
        "Valore della colonna Policy",
        "Codice giustificativo PAGHE");
    private readonly Label _note = new();
    private HashSet<string> _requiredEmployees = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _requiredPolicies = new(StringComparer.OrdinalIgnoreCase);

    public MappingEditorPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Surface;
        Margin = Padding.Empty;

        var employeePage = new Panel { BackColor = Theme.Surface, Padding = new Padding(7), Margin = Padding.Empty };
        var policyPage = new Panel { BackColor = Theme.Surface, Padding = new Padding(7), Margin = Padding.Empty };
        employeePage.Controls.Add(_employeesGrid);
        policyPage.Controls.Add(_policiesGrid);
        _tabs.Dock = DockStyle.Fill;
        _tabs.Margin = Padding.Empty;
        _tabs.AddPage("Dipendenti", employeePage);
        _tabs.AddPage("Giustificativi", policyPage);
        _tabs.SelectedIndex = 0;
        Controls.Add(_tabs);

        _note.Text =
            "I campi * sono obbligatori. Controlla i codici prima di salvare.";
        _note.Dock = DockStyle.Bottom;
        _note.Height = 28;
        _note.Padding = new Padding(8, 4, 0, 0);
        _note.BackColor = Theme.Surface;
        _note.ForeColor = Theme.MutedInk;
        _note.Font = Theme.Fonts.Small;
        _note.AutoEllipsis = true;
        Controls.Add(_note);

        ((DataGridViewTextBoxColumn)_policiesGrid.Columns[1]).MaxInputLength = 3;

        // Dati propri del dipendente (facoltativi): ore della sua giornata e periodo del rapporto.
        _employeesGrid.Columns[0].FillWeight = 40;
        _employeesGrid.Columns[1].FillWeight = 18;
        foreach (var (header, weight, length) in new[]
                 {
                     ("Ore al giorno", 14f, 5),
                     ("Assunto dal", 16f, 10),
                     ("Fino al", 16f, 10)
                 })
        {
            _employeesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                FillWeight = weight,
                MaxInputLength = length,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }
    }

    /// <summary>Profili validati dall'ultimo <see cref="TrySave"/> riuscito (uno per ogni riga; vuoto = regola generale).</summary>
    public Dictionary<string, EmployeeProfile> EmployeeProfiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Abinamenti validati dall'ultimo <see cref="TrySave"/> riuscito.</summary>
    public Dictionary<string, string> EmployeeMappings { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> PolicyMappings { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Riempie le due griglie con gli abbinamenti salvati e con i valori del
    /// foglio importato che ne sono ancora privi (quelli obbligatori sono
    /// segnati con un asterisco).
    /// </summary>
    public void Load(
        IReadOnlyDictionary<string, string> employeeMappings,
        IReadOnlyDictionary<string, string> policyMappings,
        IEnumerable<string> requiredEmployees,
        IEnumerable<string> requiredPolicies,
        IReadOnlyDictionary<string, string>? policySuggestions = null,
        IReadOnlyDictionary<string, EmployeeProfile>? employeeProfiles = null)
    {
        _requiredEmployees = new HashSet<string>(requiredEmployees, StringComparer.OrdinalIgnoreCase);
        _requiredPolicies = new HashSet<string>(requiredPolicies, StringComparer.OrdinalIgnoreCase);
        Fill(_employeesGrid, employeeMappings, _requiredEmployees);
        Fill(_policiesGrid, policyMappings, _requiredPolicies, policySuggestions);
        if (employeeProfiles is not null)
        {
            foreach (DataGridViewRow row in _employeesGrid.Rows)
            {
                if (row.Tag is string name && employeeProfiles.TryGetValue(name, out var profile))
                {
                    row.Cells[2].Value = profile.DailyHours?.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
                    row.Cells[3].Value = profile.StartDate?.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                    row.Cells[4].Value = profile.EndDate?.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }
        }
    }

    /// <summary>
    /// Convalida le griglie e compila <see cref="EmployeeMappings"/> e
    /// <see cref="PolicyMappings"/>. In caso di problema restituisce false e la
    /// spiegazione, senza aprire finestre: decide chi ospita il pannello come
    /// mostrarla (un messaggio nel dialogo, lo stato nella finestra principale).
    /// </summary>
    public bool TrySave(out string? error)
    {
        error = null;
        if (!TryReadMappings(_employeesGrid, _requiredEmployees, 7, "dipendente", EmployeeMappings, out error) ||
            !TryReadMappings(_policiesGrid, _requiredPolicies, 3, "giustificativo", PolicyMappings, out error))
        {
            return false;
        }

        var duplicate = EmployeeMappings
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.Value))
            .GroupBy(mapping => mapping.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            error = $"Il codice dipendente “{duplicate.Key}” è assegnato a più nomi. Verifica gli abbinamenti.";
            return false;
        }

        return TryReadProfiles(out error);
    }

    /// <summary>Sceglie la scheda da mostrare (0 = Dipendenti, 1 = Giustificativi).</summary>
    public void SelectTab(int index) => _tabs.SelectedIndex = index;

    private static ThemedGrid CreateGrid(string nameHeader, string codeHeader, string? suggestionHeader = null)
    {
        var grid = new ThemedGrid();
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = nameHeader,
            ReadOnly = true,
            FillWeight = 64,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = codeHeader,
            FillWeight = 36,
            MaxInputLength = 7,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        if (suggestionHeader is not null)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = suggestionHeader,
                ReadOnly = true,
                FillWeight = 36,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }
        return grid;
    }

    private static void Fill(
        ThemedGrid grid,
        IReadOnlyDictionary<string, string> mappings,
        IEnumerable<string> required,
        IReadOnlyDictionary<string, string>? suggestions = null)
    {
        grid.Rows.Clear();
        var requiredSet = required.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in mappings.Keys.Concat(requiredSet)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase))
        {
            var code = mappings.TryGetValue(name, out var existing) ? existing : string.Empty;
            var suggestion = suggestions is not null && suggestions.TryGetValue(name, out var proposed)
                ? proposed
                : string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                code = suggestion;
            }
            var rowIndex = grid.Columns.Count == 3
                ? grid.Rows.Add(name, code, suggestion)
                : grid.Rows.Add(name, code);
            if (requiredSet.Contains(name))
            {
                grid.Rows[rowIndex].Cells[0].Value = $"{name} *";
            }
            grid.Rows[rowIndex].Tag = name;
        }
    }

    private static bool TryReadMappings(
        ThemedGrid grid,
        IReadOnlySet<string> required,
        int maximumLength,
        string label,
        IDictionary<string, string> output,
        out string? error)
    {
        error = null;
        output.Clear();
        grid.EndEdit();
        foreach (DataGridViewRow row in grid.Rows)
        {
            var name = row.Tag as string ?? string.Empty;
            var code = Convert.ToString(row.Cells[1].Value)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                if (required.Contains(name))
                {
                    error = $"Inserisci il codice {label} richiesto per “{name}”.";
                    return false;
                }
                continue;
            }
            if (code.Length > maximumLength)
            {
                error =
                    $"Il codice {label} “{code}” supera il limite di {maximumLength} caratteri del tracciato.";
                return false;
            }
            output[name] = code;
        }
        return true;
    }

    /// <summary>Legge ore e date di ogni dipendente: un valore scritto male blocca il salvataggio con un messaggio chiaro.</summary>
    private bool TryReadProfiles(out string? error)
    {
        error = null;
        EmployeeProfiles.Clear();
        _employeesGrid.EndEdit();
        foreach (DataGridViewRow row in _employeesGrid.Rows)
        {
            var name = row.Tag as string ?? string.Empty;
            if (name.Length == 0)
            {
                continue;
            }

            decimal? hours = null;
            var hoursText = Convert.ToString(row.Cells[2].Value)?.Trim() ?? string.Empty;
            if (hoursText.Length > 0)
            {
                if (!GenerationSettings.TryParseHoursPerDay(hoursText, out var parsed))
                {
                    error = $"Le ore al giorno di “{name}” (“{hoursText}”) devono essere un numero fra " +
                            $"{GenerationSettings.MinimumHoursPerDay:0} e {GenerationSettings.MaximumHoursPerDay:0}, per esempio 4 oppure 6,5.";
                    return false;
                }
                hours = parsed;
            }

            if (!TryReadDate(row.Cells[3].Value, out var start, out var startText))
            {
                error = $"La data “{startText}” di “{name}” non è valida: scrivi giorno/mese/anno, per esempio 01/03/2026.";
                return false;
            }
            if (!TryReadDate(row.Cells[4].Value, out var end, out var endText))
            {
                error = $"La data “{endText}” di “{name}” non è valida: scrivi giorno/mese/anno, per esempio 01/03/2026.";
                return false;
            }
            if (start is not null && end is not null && end < start)
            {
                error = $"Per “{name}” la data di fine rapporto precede quella di inizio.";
                return false;
            }
            EmployeeProfiles[name] = new EmployeeProfile(hours, start, end);
        }
        return true;
    }

    private static bool TryReadDate(object? cell, out DateTime? date, out string? invalidText)
    {
        date = null;
        invalidText = null;
        var text = Convert.ToString(cell)?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return true;
        }
        string[] formats = ["dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "dd-MM-yyyy", "dd.MM.yyyy", "yyyy-MM-dd"];
        if (DateTime.TryParseExact(
                text, formats, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed))
        {
            date = parsed.Date;
            return true;
        }
        invalidText = text;
        return false;
    }
}
