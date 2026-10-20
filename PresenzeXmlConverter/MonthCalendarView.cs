using System.Drawing.Drawing2D;
using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>
/// Calendario del mese: una riga per dipendente e una colonna per giorno. Ogni giorno è colorato
/// secondo il giustificativo (presenza in verde, ogni assenza con il suo colore), così si vedono a
/// colpo d'occhio i buchi (giorni lavorativi senza nulla, con il contorno rosso tratteggiato) e le
/// sovrapposizioni. Si sfoglia mese per mese quando i movimenti ne toccano più d'uno, si filtra per
/// nome o per «giorni scoperti», ogni riga riporta il totale delle ore e l'ultima riga conta i presenti
/// di ogni giorno. L'immagine del mese si salva in PNG.
///
/// Le anomalie insolite (ore oltre la giornata, più giustificativi nello stesso giorno, movimenti in giorni
/// non lavorativi, totale del mese diverso dalle ore teoriche) hanno un segno sul giorno e una barra in alto
/// che le conta, con «Vai al prossimo» per scorrerle una a una: restano lì finché l'anteprima è aperta.
/// </summary>
internal sealed class MonthCalendarView : Panel
{
    private const int NameColumn = 190;
    private const int HeaderHeight = 44;
    private const int RowHeight = 28;
    private const int TotalColumn = 84;

    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    private readonly Canvas _canvas;
    private readonly Label _monthLabel = new();
    private readonly Button _previous = Theme.CreateButton("‹", Theme.ButtonTone.Neutral, compact: true);
    private readonly Button _next = Theme.CreateButton("›", Theme.ButtonTone.Neutral, compact: true);
    private readonly Button _export = Theme.CreateButton("Esporta  ▾", Theme.ButtonTone.Neutral, compact: true).WithIcon(Glyph.Download);
    private readonly RoundedField _search = new();
    private readonly CheckBox _gapsOnly = new();
    private readonly Panel _alertBar = new();
    private readonly Label _alertText = new();
    private readonly Button _alertNext = Theme.CreateButton("Vai al primo", Theme.ButtonTone.Neutral, compact: true);
    private readonly Button _alertChecks = Theme.CreateButton("Apri i controlli", Theme.ButtonTone.Neutral, compact: true);
    private readonly Dictionary<string, Dictionary<DateTime, List<CalendarAlert>>> _alertsByEmployee = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly List<(string Name, DateTime Date, AlertSeverity Severity)> _alertDays = [];
    private readonly Dictionary<(string Name, int Year, int Month), int> _monthDifference = new();
    private (string Name, DateTime Date)? _focus;
    private int _alertCursor = -1;
    private int _reviewErrors;
    private int _reviewWarnings;
    private Action? _openChecks;
    private readonly Legend _legend = new();
    private readonly ToolTip _tip = ThemedMenus.CreateToolTip();

    private readonly List<(int Year, int Month)> _months = new();
    private readonly List<string> _employees = new();
    private readonly List<string> _visible = new();
    private readonly Dictionary<string, Dictionary<DateTime, List<DailyMovement>>> _byEmployee = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly Dictionary<string, int> _codeColors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _codeNames = new(StringComparer.OrdinalIgnoreCase);
    private GenerationOptions _options = GenerationOptions.Default;
    private IReadOnlyDictionary<string, EmployeeProfile> _profiles = new Dictionary<string, EmployeeProfile>();
    private IReadOnlyList<DailyMovement> _movements = [];
    private DateTime? _periodStart;
    private DateTime? _periodEnd;
    private int _monthIndex;
    private string _lastTip = string.Empty;

    public MonthCalendarView()
    {
        BackColor = Theme.Surface;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        AccessibleName = "Calendario del mese";

        // Barra in alto con due gruppi che si dispongono da soli (a sinistra mese e filtri, a destra le
        // azioni): con le coordinate fisse i pulsanti, che hanno una larghezza minima, si sovrapponevano
        // all'etichetta del mese e alla ricerca.
        var top = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Theme.Surface, Padding = new Padding(12, 6, 12, 6) };
        var left = CreateBarGroup(DockStyle.Left);
        var right = CreateBarGroup(DockStyle.Right);

        ConfigureNavigationButton(_previous, "Mese precedente", new Padding(0, 2, 4, 0));
        _previous.Click += (_, _) => Step(-1);
        // Larghezza minima fissa ma l'etichetta cresce col testo: «Settembre 2026» a schermo ingrandito non si taglia.
        _monthLabel.AutoSize = true;
        _monthLabel.MinimumSize = new Size(176, 32);
        _monthLabel.Margin = new Padding(0, 2, 4, 0);
        _monthLabel.TextAlign = ContentAlignment.MiddleCenter;
        _monthLabel.Font = Theme.Fonts.DialogTitle;
        _monthLabel.ForeColor = Theme.Ink;
        ConfigureNavigationButton(_next, "Mese successivo", new Padding(0, 2, 18, 0));
        _next.Click += (_, _) => Step(1);

        _search.Size = new Size(200, 34);
        _search.Margin = new Padding(0, 1, 16, 0);
        _search.Placeholder = "Cerca dipendente";
        _search.Input.AccessibleName = "Cerca dipendente nel calendario";
        _gapsOnly.Text = "Solo da controllare";
        _gapsOnly.AutoSize = true;
        _gapsOnly.Font = Theme.Fonts.Small;
        _gapsOnly.ForeColor = Theme.MutedInk;
        _gapsOnly.BackColor = Theme.Surface;
        _gapsOnly.Margin = new Padding(0, 8, 0, 0);
        _gapsOnly.AccessibleName = "Mostra solo i dipendenti con giorni scoperti, anomalie o ore del mese che non tornano";
        left.Controls.AddRange([_previous, _monthLabel, _next, _search, _gapsOnly]);

        // Un solo pulsante con il menu delle esportazioni: sul portatile la barra non si affolla.
        _export.Margin = new Padding(0, 2, 0, 0);
        _export.AccessibleName = "Esporta il mese: immagine, riepilogo CSV o report PDF";
        _export.Click += (_, _) => ShowExportMenu();
        right.Controls.AddRange([_export]);

        top.Controls.Add(left);
        top.Controls.Add(right);

        _legend.Dock = DockStyle.Bottom;
        _legend.Height = 34;

        _canvas = new Canvas(this) { Dock = DockStyle.Fill };

        // Prima il corpo, poi barra in alto e legenda in basso: l'ordine decide chi prende lo spazio.
        BuildAlertBar();
        Controls.Add(_canvas);
        Controls.Add(_legend);
        Controls.Add(_alertBar);
        Controls.Add(top);
        Clear();
        _search.TextChanged += (_, _) => ApplyFilter();
        _gapsOnly.CheckedChanged += (_, _) => ApplyFilter();
    }

    private void BuildAlertBar()
    {
        _alertBar.Dock = DockStyle.Top;
        _alertBar.Height = 46;
        _alertBar.Padding = new Padding(14, 6, 12, 6);
        _alertBar.Visible = false;
        _alertBar.AccessibleName = "Anomalie da controllare nel calendario";
        _alertBar.AccessibleRole = AccessibleRole.Alert;

        var actions = CreateBarGroup(DockStyle.Right);
        actions.BackColor = Color.Transparent;
        _alertNext.Margin = new Padding(8, 2, 0, 0);
        _alertNext.AccessibleName = "Vai alla prossima anomalia del calendario";
        _alertNext.Click += (_, _) => GoToNextAlert();
        _alertChecks.Margin = new Padding(8, 2, 0, 0);
        _alertChecks.AccessibleName = "Apri la scheda dei controlli";
        _alertChecks.Click += (_, _) => _openChecks?.Invoke();
        actions.Controls.AddRange([_alertNext, _alertChecks]);

        _alertText.Dock = DockStyle.Fill;
        _alertText.AutoEllipsis = true;
        _alertText.TextAlign = ContentAlignment.MiddleLeft;
        _alertText.Font = Theme.Fonts.SmallBold;
        _alertBar.Controls.Add(_alertText);
        _alertBar.Controls.Add(actions);
    }

    /// <summary>
    /// Conteggio dei controlli della finestra principale (errori e attenzioni) con il comando per aprirli:
    /// il calendario li ricorda in alto finché ci sono.
    /// </summary>
    public void SetReviewSummary(int errors, int warnings, Action? openChecks)
    {
        _reviewErrors = errors;
        _reviewWarnings = warnings;
        _openChecks = openChecks;
        UpdateAlertBar();
    }

    /// <summary>Giorni con ore oltre la giornata: sono quelli da controllare davvero.</summary>
    public int CheckDayCount => _alertDays.Count(day => day.Severity == AlertSeverity.Check);

    /// <summary>Giorni insoliti ma spesso legittimi (sabato lavorato, più giustificativi).</summary>
    public int NoticeDayCount => _alertDays.Count(day => day.Severity == AlertSeverity.Notice);

    private void UpdateAlertBar()
    {
        var checks = CheckDayCount;
        var notices = NoticeDayCount;
        var differences = _monthDifference.Count;
        var hasReview = _reviewErrors + _reviewWarnings > 0;
        if (checks + notices + differences == 0 && !hasReview)
        {
            _alertBar.Visible = false;
            return;
        }

        var parts = new List<string>();
        if (checks > 0)
        {
            parts.Add(checks == 1 ? "1 giorno con ore oltre la giornata" : $"{checks} giorni con ore oltre la giornata");
        }
        if (notices > 0)
        {
            parts.Add(notices == 1 ? "1 giorno insolito" : $"{notices} giorni insoliti");
        }
        if (differences > 0)
        {
            parts.Add(differences == 1 ? "1 totale del mese diverso dal teorico" : $"{differences} totali del mese diversi dal teorico");
        }
        if (hasReview)
        {
            parts.Add($"controlli: {_reviewErrors:N0} errori e {_reviewWarnings:N0} attenzioni");
        }
        var severe = checks > 0 || _reviewErrors > 0;
        _alertText.Text = "⚠  Da controllare prima di esportare: " + string.Join("  ·  ", parts) + ".";
        _alertText.ForeColor = severe ? Theme.DangerInk : Theme.WarningInk;
        _alertBar.BackColor = severe ? Theme.DangerSoft : Theme.WarningSoft;
        _alertNext.Visible = _alertDays.Count > 0;
        _alertNext.Text = _alertCursor < 0 ? "Vai al primo" : "Vai al prossimo";
        _alertChecks.Visible = hasReview && _openChecks is not null;
        _alertBar.Visible = true;
    }

    /// <summary>Porta in vista il prossimo giorno segnalato: cambia mese, toglie i filtri se lo nascondono, lo cerchia.</summary>
    private void GoToNextAlert()
    {
        if (_alertDays.Count == 0)
        {
            return;
        }
        _alertCursor = (_alertCursor + 1) % _alertDays.Count;
        var (name, date, _) = _alertDays[_alertCursor];
        var monthIndex = _months.IndexOf((date.Year, date.Month));
        if (monthIndex >= 0)
        {
            _monthIndex = monthIndex;
            UpdateNavigation();
        }
        ApplyFilter();
        if (!_visible.Contains(name, StringComparer.CurrentCultureIgnoreCase))
        {
            // Il filtro nasconde proprio quel dipendente: si tolgono i filtri (ognuno riapplica l'elenco).
            _gapsOnly.Checked = false;
            _search.Value = string.Empty;
            ApplyFilter();
        }
        _focus = (name, date);
        var row = _visible.FindIndex(visible => string.Equals(visible, name, StringComparison.CurrentCultureIgnoreCase));
        _canvas.ScrollToCell(row, date.Day);
        UpdateAlertBar();
    }

    private static FlowLayoutPanel CreateBarGroup(DockStyle dock) => new()
    {
        Dock = dock,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = false,
        FlowDirection = FlowDirection.LeftToRight,
        BackColor = Theme.Surface,
        Margin = Padding.Empty,
        Padding = Padding.Empty
    };

    /// <summary>Freccia del mese: i pulsanti compatti hanno una larghezza minima, qui va abbassata prima della misura.</summary>
    private static void ConfigureNavigationButton(Button button, string accessibleName, Padding margin)
    {
        button.MinimumSize = new Size(36, 30);
        button.Size = new Size(38, 32);
        button.Padding = Padding.Empty;
        button.Font = Theme.Fonts.DialogTitle;
        button.Margin = margin;
        button.AccessibleName = accessibleName;
    }

    /// <summary>Svuota il calendario (nessuna anteprima generata).</summary>
    public void Clear()
    {
        _movements = [];
        _months.Clear();
        _employees.Clear();
        _visible.Clear();
        _byEmployee.Clear();
        _codeColors.Clear();
        _codeNames.Clear();
        _alertsByEmployee.Clear();
        _alertDays.Clear();
        _monthDifference.Clear();
        _focus = null;
        _alertCursor = -1;
        _reviewErrors = 0;
        _reviewWarnings = 0;
        _openChecks = null;
        _monthIndex = 0;
        UpdateNavigation();
        UpdateAlertBar();
        _legend.SetEntries([]);
        _canvas.RefreshLayout();
    }

    /// <summary>Riempie il calendario con i movimenti generati.</summary>
    public void SetData(
        IReadOnlyList<DailyMovement> movements,
        GenerationOptions options,
        IReadOnlyDictionary<string, EmployeeProfile>? profiles,
        DateTime? periodStart = null,
        DateTime? periodEnd = null)
    {
        Clear();
        _options = options;
        _movements = movements;
        _periodStart = periodStart;
        _periodEnd = periodEnd;
        _profiles = profiles ?? new Dictionary<string, EmployeeProfile>();
        if (movements.Count == 0)
        {
            return;
        }

        foreach (var movement in movements)
        {
            if (!_byEmployee.TryGetValue(movement.EmployeeName, out var days))
            {
                days = new Dictionary<DateTime, List<DailyMovement>>();
                _byEmployee[movement.EmployeeName] = days;
            }
            if (!days.TryGetValue(movement.Date.Date, out var list))
            {
                list = new List<DailyMovement>();
                days[movement.Date.Date] = list;
            }
            list.Add(movement);
            if (!AttendanceMovementGenerator.IsPresence(movement) && !_codeColors.ContainsKey(movement.ReasonCode))
            {
                _codeColors[movement.ReasonCode] = _codeColors.Count % AbsencePalette.Colors.Length;
                _codeNames[movement.ReasonCode] = movement.Policy;
            }
        }
        _employees.AddRange(_byEmployee.Keys.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase));
        _months.AddRange(movements
            .Select(movement => (movement.Date.Year, movement.Date.Month))
            .Distinct()
            .OrderBy(month => month));
        _monthIndex = 0;
        ComputeAlerts();

        var entries = new List<(Color Fill, Color Ink, string Text, bool Dashed)>
        {
            (Theme.SuccessSoft, Theme.SuccessInk, "Presenza", false)
        };
        foreach (var code in _codeColors)
        {
            var colors = AbsencePalette.Colors[code.Value];
            var name = _codeNames.TryGetValue(code.Key, out var policy) && policy.Length > 0 ? $"{code.Key} · {policy}" : code.Key;
            entries.Add((colors.Fill, colors.Ink, name, false));
        }
        if (_options.FillPresence)
        {
            entries.Add((Theme.Surface, Theme.Danger, "Giorno senza movimenti", true));
        }
        entries.Add((Theme.Danger, Color.White, "Oltre la giornata", false));
        entries.Add((Theme.Warning, Color.White, "Giorno insolito", false));
        _legend.SetEntries(entries);
        UpdateNavigation();
        ApplyFilter();
        UpdateAlertBar();
    }

    private void ComputeAlerts()
    {
        foreach (var alert in CalendarAlerts.Find(_movements, _options, _profiles))
        {
            if (!_alertsByEmployee.TryGetValue(alert.EmployeeName, out var days))
            {
                days = new Dictionary<DateTime, List<CalendarAlert>>();
                _alertsByEmployee[alert.EmployeeName] = days;
            }
            if (!days.TryGetValue(alert.Date, out var list))
            {
                list = new List<CalendarAlert>();
                days[alert.Date] = list;
            }
            list.Add(alert);
        }
        _alertDays.AddRange(_alertsByEmployee
            .SelectMany(employee => employee.Value.Select(day => (
                Name: employee.Key,
                Date: day.Key,
                Severity: day.Value.Max(alert => alert.Severity))))
            .OrderByDescending(day => day.Severity)
            .ThenBy(day => day.Date)
            .ThenBy(day => day.Name, StringComparer.CurrentCultureIgnoreCase));

        // Totale del mese diverso dalle ore teoriche (solo con le presenze automatiche accese).
        foreach (var row in MonthlySummary.Build(_movements, _options, _profiles, _periodStart, _periodEnd))
        {
            if (row.ExpectedMinutes is { } expected && row.TotalMinutes != expected)
            {
                _monthDifference[(row.EmployeeName, row.Month.Year, row.Month.Month)] = row.TotalMinutes - expected;
            }
        }
    }

    private AlertSeverity? SeverityOf(string name, DateTime date) =>
        _alertsByEmployee.TryGetValue(name, out var days) && days.TryGetValue(date, out var list)
            ? list.Max(alert => alert.Severity)
            : null;

    private int DifferenceOf(string name, int year, int month) =>
        _monthDifference.TryGetValue((name, year, month), out var difference) ? difference : 0;

    private void Step(int delta)
    {
        var target = Math.Clamp(_monthIndex + delta, 0, Math.Max(0, _months.Count - 1));
        if (target == _monthIndex)
        {
            return;
        }
        _monthIndex = target;
        _focus = null;
        UpdateNavigation();
        ApplyFilter();
    }

    /// <summary>Ricostruisce l'elenco dei dipendenti visibili secondo ricerca e filtro dei giorni scoperti.</summary>
    private void ApplyFilter()
    {
        _visible.Clear();
        if (_months.Count > 0)
        {
            var (year, month) = _months[_monthIndex];
            var text = _search.Value.Trim();
            var gapsOnly = _gapsOnly.Checked;
            foreach (var name in _employees)
            {
                if (text.Length > 0 && name.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) < 0)
                {
                    continue;
                }
                if (gapsOnly && !NeedsChecking(name, year, month))
                {
                    continue;
                }
                _visible.Add(name);
            }
        }
        _canvas.RefreshLayout();
    }

    /// <summary>Giorni scoperti, anomalie o totale del mese che non torna: il dipendente va guardato.</summary>
    private bool NeedsChecking(string name, int year, int month) =>
        (_options.FillPresence && StatsOf(name, year, month).Gaps > 0) ||
        DifferenceOf(name, year, month) != 0 ||
        (_alertsByEmployee.TryGetValue(name, out var days) &&
         days.Keys.Any(date => date.Year == year && date.Month == month));

    /// <summary>Minuti registrati e giorni lavorativi senza movimenti di un dipendente nel mese.</summary>
    private (int Minutes, int Gaps) StatsOf(string name, int year, int month)
    {
        var profile = ProfileOf(name);
        var days = _byEmployee[name];
        var minutes = 0;
        var gaps = 0;
        for (var day = 1; day <= DateTime.DaysInMonth(year, month); day++)
        {
            var date = new DateTime(year, month, day);
            if (days.TryGetValue(date, out var items))
            {
                minutes += items.Sum(movement => (movement.Hours * 60) + movement.Minutes);
            }
            else if (_options.FillPresence && IsWorkingDay(date, profile))
            {
                gaps++;
            }
        }
        return (minutes, gaps);
    }

    private static string FormatTotal(int minutes) =>
        (minutes / 60m).ToString("0.#", Italian) + " h";

    private void UpdateNavigation()
    {
        if (_months.Count == 0)
        {
            _monthLabel.Text = "Calendario";
            _previous.Enabled = false;
            _next.Enabled = false;
            _export.Enabled = false;
            _gapsOnly.Enabled = false;
            return;
        }
        _export.Enabled = true;
        _gapsOnly.Enabled = true;
        var (year, month) = _months[_monthIndex];
        var title = new DateTime(year, month, 1).ToString("MMMM yyyy", Italian);
        _monthLabel.Text = char.ToUpper(title[0], Italian) + title[1..];
        _previous.Enabled = _monthIndex > 0;
        _next.Enabled = _monthIndex < _months.Count - 1;
    }

    private bool IsWorkingDay(DateTime day, EmployeeProfile? profile) =>
        day.DayOfWeek != DayOfWeek.Sunday &&
        (day.DayOfWeek != DayOfWeek.Saturday || _options.IncludeSaturdays) &&
        !ItalianHolidays.Contains(day, _options.ExtraHolidays ?? []) &&
        (profile is null || profile.Covers(day));

    private EmployeeProfile? ProfileOf(string name) => _profiles.TryGetValue(name, out var profile) ? profile : null;

    private string DescribeDay(string name, DateTime day)
    {
        var items = _byEmployee.TryGetValue(name, out var found) && found.TryGetValue(day.Date, out var list)
            ? list
            : new List<DailyMovement>();
        var lines = items
            .Select(movement =>
            {
                var minutes = movement.Hours * 60 + movement.Minutes;
                var text = minutes % 60 == 0 ? $"{minutes / 60}h" : $"{minutes / 60}h {minutes % 60:D2}m";
                return $"{movement.ReasonCode} · {movement.Policy}: {text}";
            })
            .ToArray();
        var header = $"{name} · {day.ToString("dddd d MMMM", Italian)}";
        if (lines.Length > 0)
        {
            var warnings = _alertsByEmployee.TryGetValue(name, out var alertDays) && alertDays.TryGetValue(day.Date, out var alerts)
                ? alerts.Select(alert => (alert.Severity == AlertSeverity.Check ? "⚠ Da controllare: " : "● ") + alert.Text)
                : [];
            return header + Environment.NewLine + string.Join(Environment.NewLine, lines.Concat(warnings));
        }
        if (day.DayOfWeek == DayOfWeek.Sunday || day.DayOfWeek == DayOfWeek.Saturday ||
            ItalianHolidays.Contains(day, _options.ExtraHolidays ?? []))
        {
            return header + Environment.NewLine + "Giorno non lavorativo";
        }
        return header + Environment.NewLine + "Nessun movimento";
    }

    private string DescribeEmployee(string name)
    {
        var (year, month) = _months[_monthIndex];
        var (minutes, gaps) = StatsOf(name, year, month);
        var text = $"{name}{Environment.NewLine}{FormatTotal(minutes)} registrate nel mese";
        var difference = DifferenceOf(name, year, month);
        if (difference != 0)
        {
            text += Environment.NewLine + $"⚠ {(difference > 0 ? "+" : "−")}{FormatTotal(Math.Abs(difference))} rispetto alle ore teoriche";
        }
        return gaps > 0
            ? text + Environment.NewLine + (gaps == 1 ? "1 giorno lavorativo senza movimenti" : $"{gaps} giorni lavorativi senza movimenti")
            : text;
    }

    private int PresentOn(DateTime day) => _visible.Count(name =>
        _byEmployee[name].TryGetValue(day.Date, out var items) &&
        (_options.FillPresence ? items.Any(AttendanceMovementGenerator.IsPresence) : items.Count > 0));

    private string FooterLabel => _options.FillPresence ? "Presenti" : "Con movimenti";

    private string DescribeFooter(DateTime day) =>
        $"{FooterLabel} · {day.ToString("dddd d MMMM", Italian)}{Environment.NewLine}{PresentOn(day)} su {_visible.Count}";

    /// <summary>Scatta dopo ogni esportazione riuscita, con il percorso del file scritto.</summary>
    public event EventHandler<string>? Exported;

    /// <summary>Azienda mostrata in testa al report PDF.</summary>
    public string CompanyLabel { get; set; } = string.Empty;

    private void ShowExportMenu()
    {
        var menu = ThemedMenus.Create();
        menu.Items.Add("Report del mese (PDF)", null, (_, _) => SaveReport());
        menu.Items.Add("Riepilogo ore (CSV per Excel)", null, (_, _) => SaveSummary());
        menu.Items.Add("Immagine del mese (PNG)", null, (_, _) => SaveImage());
        menu.Show(_export, new Point(0, _export.Height));
    }

    private void SaveReport()
    {
        if (_movements.Count == 0)
        {
            return;
        }
        using var picker = new SaveFileDialog
        {
            Title = "Salva il report del mese",
            Filter = "Documento PDF (*.pdf)|*.pdf",
            AddExtension = true,
            DefaultExt = "pdf",
            FileName = _months.Count > 0
                ? $"Report ore {_months[_monthIndex].Year}-{_months[_monthIndex].Month:00}.pdf"
                : "Report ore.pdf",
            OverwritePrompt = true
        };
        picker.ClientGuid = FileDialogs.ClientId;
        if (picker.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }
        try
        {
            var rows = MonthlySummary.Build(_movements, _options, _profiles, _periodStart, _periodEnd);
            var alerts = _alertsByEmployee.Values.SelectMany(days => days.Values).SelectMany(list => list).ToArray();
            var company = CompanyLabel.Length > 0 ? CompanyLabel : "Riepilogo dei movimenti generati";
            File.WriteAllBytes(picker.FileName, MonthlyReport.Build(company, rows, alerts, DateTime.Now));
            Exported?.Invoke(this, picker.FileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                FindForm(),
                "Non è stato possibile salvare il report: " + exception.Message,
                "Calendario",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void SaveSummary()
    {
        if (_movements.Count == 0)
        {
            return;
        }
        using var picker = new SaveFileDialog
        {
            Title = "Salva il riepilogo delle ore",
            Filter = "File CSV per Excel (*.csv)|*.csv",
            AddExtension = true,
            DefaultExt = "csv",
            FileName = "Riepilogo ore.csv",
            OverwritePrompt = true
        };
        picker.ClientGuid = FileDialogs.ClientId;
        if (picker.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }
        try
        {
            var rows = MonthlySummary.Build(_movements, _options, _profiles, _periodStart, _periodEnd);
            File.WriteAllText(picker.FileName, MonthlySummary.ToCsv(rows), new System.Text.UTF8Encoding(true));
            Exported?.Invoke(this, picker.FileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                FindForm(),
                "Non è stato possibile salvare il riepilogo: " + exception.Message,
                "Calendario",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void SaveImage()
    {
        if (_months.Count == 0)
        {
            return;
        }
        var (year, month) = _months[_monthIndex];
        using var picker = new SaveFileDialog
        {
            Title = "Salva l'immagine del calendario",
            Filter = "Immagine PNG (*.png)|*.png",
            AddExtension = true,
            DefaultExt = "png",
            FileName = $"Calendario {year}-{month:00}.png",
            OverwritePrompt = true
        };
        picker.ClientGuid = FileDialogs.ClientId;
        if (picker.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }
        try
        {
            using var image = _canvas.RenderImage(_monthLabel.Text);
            image.Save(picker.FileName, System.Drawing.Imaging.ImageFormat.Png);
            Exported?.Invoke(this, picker.FileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException)
        {
            MessageBox.Show(
                FindForm(),
                "Non è stato possibile salvare l'immagine: " + exception.Message,
                "Calendario",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    /// <summary>Superficie scorrevole dove si dipinge la griglia.</summary>
    private sealed class Canvas : ScrollableControl
    {
        private enum Part { None, Name, Day, Total }

        private const int TitleHeight = 36;

        private readonly MonthCalendarView _owner;
        private (int Row, int Day) _hover = (-1, 0);

        public Canvas(MonthCalendarView owner)
        {
            _owner = owner;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            AutoScroll = true;
            BackColor = Theme.Surface;
        }

        private int Days => _owner._months.Count == 0
            ? 30
            : DateTime.DaysInMonth(_owner._months[_owner._monthIndex].Year, _owner._months[_owner._monthIndex].Month);

        private int CellWidth => Math.Clamp((ClientSize.Width - NameColumn - TotalColumn - 8) / Math.Max(1, Days), 18, 30);

        // Una riga in più per i presenti del giorno, solo se c'è almeno un dipendente da mostrare.
        private int RowCount => _owner._visible.Count == 0 ? 0 : _owner._visible.Count + 1;

        private Size ContentSize(int cellWidth) => new(
            NameColumn + (Days * cellWidth) + TotalColumn + 8,
            HeaderHeight + (RowCount * RowHeight) + 6);

        /// <summary>Porta una cella in vista, più o meno al centro dell'area visibile.</summary>
        public void ScrollToCell(int row, int day)
        {
            if (row < 0 || _owner._months.Count == 0)
            {
                return;
            }
            var x = Math.Max(0, ((day - 1) * CellWidth) - ((ClientSize.Width - NameColumn) / 2));
            var y = Math.Max(0, (row * RowHeight) - (ClientSize.Height / 3));
            AutoScrollPosition = new Point(x, y);
            Invalidate();
        }

        public void RefreshLayout()
        {
            AutoScrollPosition = Point.Empty;
            _hover = (-1, 0);
            AutoScrollMinSize = _owner._months.Count == 0 ? Size.Empty : ContentSize(CellWidth);
            Invalidate();
        }

        protected override void OnResize(EventArgs eventArgs)
        {
            base.OnResize(eventArgs);
            if (_owner._months.Count > 0)
            {
                AutoScrollMinSize = ContentSize(CellWidth);
            }
        }

        private const int WmHScroll = 0x0114;
        private const int WmVScroll = 0x0115;
        private const int WmMouseWheel = 0x020A;
        private const int WmMouseHWheel = 0x020E;

        /// <summary>
        /// Con lo scorrimento Windows sposta i pixel già dipinti invece di ridisegnare: l'intestazione e la
        /// colonna dei nomi, che restano ferme, lasciavano copie fantasma ad ogni passo. Dopo ogni scorrimento
        /// si ridipinge tutto.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg is WmHScroll or WmVScroll or WmMouseWheel or WmMouseHWheel)
            {
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var (part, row, day) = HitTest(e.Location);
            var hover = part == Part.None ? (-1, 0) : (row, part == Part.Day ? day : 0);
            if (hover != _hover)
            {
                _hover = hover;
                Invalidate();
            }
            var text = TipAt(part, row, day);
            if (text != _owner._lastTip)
            {
                _owner._lastTip = text;
                if (text.Length == 0)
                {
                    _owner._tip.Hide(this);
                }
                else
                {
                    _owner._tip.Show(text, this, e.X + 14, e.Y + 18, 6000);
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _owner._lastTip = string.Empty;
            _owner._tip.Hide(this);
            if (_hover != (-1, 0))
            {
                _hover = (-1, 0);
                Invalidate();
            }
        }

        private (Part Part, int Row, int Day) HitTest(Point point)
        {
            var rows = _owner._visible.Count;
            if (_owner._months.Count == 0 || rows == 0 || point.Y < HeaderHeight)
            {
                return (Part.None, -1, 0);
            }
            var scroll = AutoScrollPosition;
            var row = (point.Y - scroll.Y - HeaderHeight) / RowHeight;
            if (point.Y - scroll.Y < HeaderHeight || row > rows)
            {
                return (Part.None, -1, 0);
            }
            if (point.X < NameColumn)
            {
                return (Part.Name, row, 0);
            }
            var offset = point.X - scroll.X - NameColumn;
            var cellWidth = CellWidth;
            if (offset < 0)
            {
                return (Part.None, -1, 0);
            }
            if (offset < Days * cellWidth)
            {
                return (Part.Day, row, (offset / cellWidth) + 1);
            }
            return offset < (Days * cellWidth) + TotalColumn ? (Part.Total, row, 0) : (Part.None, -1, 0);
        }

        private string TipAt(Part part, int row, int day)
        {
            if (part == Part.None)
            {
                return string.Empty;
            }
            var (year, month) = _owner._months[_owner._monthIndex];
            var footer = row == _owner._visible.Count;
            return part switch
            {
                Part.Day when footer => _owner.DescribeFooter(new DateTime(year, month, day)),
                Part.Day => _owner.DescribeDay(_owner._visible[row], new DateTime(year, month, day)),
                _ when footer => string.Empty,
                _ => _owner.DescribeEmployee(_owner._visible[row])
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            DrawGrid(e.Graphics, ClientSize, AutoScrollPosition, CellWidth, interactive: true);
        }

        /// <summary>Immagine dell'intero mese (titolo, griglia e legenda), a prescindere dalla finestra.</summary>
        public Bitmap RenderImage(string title)
        {
            const int cellWidth = 30;
            var content = ContentSize(cellWidth);
            var legendHeight = _owner._legend.Height;
            var image = new Bitmap(content.Width, TitleHeight + content.Height + legendHeight);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(Theme.Surface);
            UiText.Draw(
                graphics,
                title,
                Theme.Fonts.DialogTitle,
                new Rectangle(12, 0, content.Width - 12, TitleHeight),
                Theme.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            using (var grid = new Bitmap(content.Width, content.Height))
            using (var gridGraphics = Graphics.FromImage(grid))
            {
                gridGraphics.Clear(Theme.Surface);
                DrawGrid(gridGraphics, content, Point.Empty, cellWidth, interactive: false);
                graphics.DrawImageUnscaled(grid, 0, TitleHeight);
            }

            if (_owner._legend.Width > 0 && legendHeight > 0)
            {
                using var legend = new Bitmap(Math.Min(_owner._legend.Width, content.Width), legendHeight);
                _owner._legend.DrawToBitmap(legend, new Rectangle(Point.Empty, legend.Size));
                graphics.DrawImageUnscaled(legend, 0, TitleHeight + content.Height);
            }
            return image;
        }

        private void DrawGrid(Graphics graphics, Size size, Point scroll, int cellWidth, bool interactive)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (_owner._months.Count == 0)
            {
                UiText.Draw(
                    graphics,
                    "Genera l'anteprima per vedere il calendario del mese.",
                    Theme.Fonts.Small,
                    new Rectangle(Point.Empty, size),
                    Theme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            var (year, month) = _owner._months[_owner._monthIndex];
            var days = Days;
            var visible = _owner._visible;
            var today = DateTime.Today;
            var gridBottom = HeaderHeight + (RowCount * RowHeight) + scroll.Y;
            using var holidayFill = new SolidBrush(Theme.Sunken);
            using var gapPen = new Pen(Theme.Danger, 1.2f) { DashStyle = DashStyle.Dash };
            using var hoverBand = new SolidBrush(Theme.BrandSoft);
            using var columnBand = new SolidBrush(Color.FromArgb(26, Theme.Brand));
            using var todayBand = new SolidBrush(Color.FromArgb(14, Theme.Brand));

            // Colonne evidenziate sotto le celle: il giorno corrente e quello sotto il mouse.
            for (var day = 1; day <= days; day++)
            {
                var date = new DateTime(year, month, day);
                var hovered = interactive && _hover.Day == day;
                if (!hovered && date != today)
                {
                    continue;
                }
                var left = NameColumn + ((day - 1) * cellWidth) + scroll.X;
                graphics.FillRectangle(hovered ? columnBand : todayBand, left, HeaderHeight, cellWidth, Math.Max(0, gridBottom - HeaderHeight));
            }

            for (var row = 0; row < visible.Count; row++)
            {
                var top = HeaderHeight + (row * RowHeight) + scroll.Y;
                if (top + RowHeight < HeaderHeight || top > size.Height)
                {
                    continue;
                }
                var name = visible[row];
                var profile = _owner.ProfileOf(name);
                var byDay = _owner._byEmployee[name];
                if (interactive && _hover.Row == row)
                {
                    graphics.FillRectangle(hoverBand, 0, top, size.Width, RowHeight);
                }
                else if (row % 2 == 1)
                {
                    using var band = new SolidBrush(Theme.AltRow);
                    graphics.FillRectangle(band, 0, top, size.Width, RowHeight);
                }

                for (var day = 1; day <= days; day++)
                {
                    var date = new DateTime(year, month, day);
                    var cell = new Rectangle(NameColumn + ((day - 1) * cellWidth) + scroll.X + 1, top + 2, cellWidth - 2, RowHeight - 4);
                    var working = _owner.IsWorkingDay(date, profile);
                    if (!working)
                    {
                        graphics.FillRectangle(holidayFill, cell);
                    }
                    if (!byDay.TryGetValue(date, out var items))
                    {
                        if (working && _owner._options.FillPresence)
                        {
                            using var path = Theme.RoundedRectangle(cell, 5);
                            graphics.DrawPath(gapPen, path);
                        }
                        continue;
                    }
                    DrawDay(graphics, cell, items, _owner.SeverityOf(name, date));
                    if (interactive && _owner._focus is { } focus &&
                        focus.Date == date && string.Equals(focus.Name, name, StringComparison.CurrentCultureIgnoreCase))
                    {
                        using var ring = new Pen(Theme.Brand, 2.4f);
                        using var ringPath = Theme.RoundedRectangle(Rectangle.Inflate(cell, 2, 2), 7);
                        graphics.DrawPath(ring, ringPath);
                    }
                }

                DrawTotal(graphics, name, year, month, NameColumn + (days * cellWidth) + scroll.X, top);
            }

            // Riga dei presenti: quanti dipendenti hanno la presenza in ogni giorno.
            if (visible.Count > 0)
            {
                var top = HeaderHeight + (visible.Count * RowHeight) + scroll.Y;
                graphics.FillRectangle(holidayFill, 0, top, size.Width, RowHeight);
                for (var day = 1; day <= days; day++)
                {
                    var date = new DateTime(year, month, day);
                    var count = _owner.PresentOn(date);
                    UiText.Draw(
                        graphics,
                        count.ToString(CultureInfo.InvariantCulture),
                        Theme.Fonts.Tiny,
                        new Rectangle(NameColumn + ((day - 1) * cellWidth) + scroll.X, top, cellWidth, RowHeight),
                        count > 0 ? Theme.SuccessInk : Theme.FaintInk,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }

            // Colonna dei nomi e intestazione: restano ferme mentre il resto scorre.
            using (var back = new SolidBrush(Theme.Surface))
            {
                graphics.FillRectangle(back, 0, 0, NameColumn, size.Height);
                graphics.FillRectangle(back, 0, 0, size.Width, HeaderHeight);
            }
            for (var row = 0; row < visible.Count; row++)
            {
                var top = HeaderHeight + (row * RowHeight) + scroll.Y;
                if (top + RowHeight < HeaderHeight || top > size.Height)
                {
                    continue;
                }
                if (interactive && _hover.Row == row)
                {
                    graphics.FillRectangle(hoverBand, 0, top, NameColumn, RowHeight);
                }
                else if (row % 2 == 1)
                {
                    using var band = new SolidBrush(Theme.AltRow);
                    graphics.FillRectangle(band, 0, top, NameColumn, RowHeight);
                }
                UiText.Draw(
                    graphics,
                    visible[row],
                    Theme.Fonts.Small,
                    new Rectangle(12, top, NameColumn - 18, RowHeight),
                    Theme.Ink,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            if (visible.Count > 0)
            {
                var top = HeaderHeight + (visible.Count * RowHeight) + scroll.Y;
                graphics.FillRectangle(holidayFill, 0, top, NameColumn, RowHeight);
                UiText.Draw(
                    graphics,
                    _owner.FooterLabel,
                    Theme.Fonts.SmallBold,
                    new Rectangle(12, top, NameColumn - 18, RowHeight),
                    Theme.MutedInk,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            else
            {
                UiText.Draw(
                    graphics,
                    "Nessun dipendente corrisponde ai filtri.",
                    Theme.Fonts.Small,
                    new Rectangle(0, HeaderHeight, size.Width, 90),
                    Theme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            for (var day = 1; day <= days; day++)
            {
                var date = new DateTime(year, month, day);
                var left = NameColumn + ((day - 1) * cellWidth) + scroll.X;
                var holiday = date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) &&
                    ItalianHolidays.Contains(date, _owner._options.ExtraHolidays ?? []);
                var ink = holiday
                    ? Theme.DangerInk
                    : date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? Theme.FaintInk : Theme.MutedInk;
                var numberArea = new Rectangle(left, 6, cellWidth, 18);
                if (date == today)
                {
                    var badge = new Rectangle(left + ((cellWidth - 20) / 2), 5, 20, 20);
                    using var badgeFill = new SolidBrush(Theme.Brand);
                    graphics.FillEllipse(badgeFill, badge);
                    ink = Color.White;
                }
                UiText.Draw(
                    graphics,
                    day.ToString(CultureInfo.InvariantCulture),
                    Theme.Fonts.SmallBold,
                    numberArea,
                    ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                UiText.Draw(
                    graphics,
                    date.ToString("ddd", Italian)[..1].ToUpper(Italian),
                    Theme.Fonts.Tiny,
                    new Rectangle(left, 26, cellWidth, 14),
                    holiday ? Theme.DangerInk : date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? Theme.FaintInk : Theme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            var totalLeft = NameColumn + (days * cellWidth) + scroll.X;
            UiText.Draw(
                graphics,
                "Totale",
                Theme.Fonts.SmallBold,
                new Rectangle(totalLeft, 14, TotalColumn - 6, 18),
                Theme.MutedInk,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            using var line = new Pen(Theme.Divider);
            graphics.DrawLine(line, 0, HeaderHeight - 1, size.Width, HeaderHeight - 1);
        }

        /// <summary>Totale delle ore del mese di un dipendente e, in rosso, quanti giorni lavorativi mancano.</summary>
        private void DrawTotal(Graphics graphics, string name, int year, int month, int left, int top)
        {
            var (minutes, gaps) = _owner.StatsOf(name, year, month);
            var differs = _owner.DifferenceOf(name, year, month) != 0;
            UiText.Draw(
                graphics,
                MonthCalendarView.FormatTotal(minutes),
                Theme.Fonts.SmallBold,
                new Rectangle(left, top, 52, RowHeight),
                differs ? Theme.WarningInk : Theme.Ink,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (gaps == 0)
            {
                return;
            }
            var pill = new Rectangle(left + 58, top + 6, 22, RowHeight - 12);
            using var path = Theme.RoundedRectangle(pill, 6);
            using var fill = new SolidBrush(Theme.Danger);
            graphics.FillPath(fill, path);
            UiText.Draw(
                graphics,
                gaps > 99 ? "99+" : gaps.ToString(CultureInfo.InvariantCulture),
                Theme.Fonts.Tiny,
                pill,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        /// <summary>Un giorno con movimenti: assenza sopra e presenza sotto se ci sono entrambe.</summary>
        private void DrawDay(Graphics graphics, Rectangle cell, List<DailyMovement> items, AlertSeverity? severity)
        {
            var absences = items.Where(movement => !AttendanceMovementGenerator.IsPresence(movement)).ToArray();
            var presence = items.Where(AttendanceMovementGenerator.IsPresence).ToArray();
            var state = graphics.Save();
            using var path = Theme.RoundedRectangle(cell, 5);
            graphics.SetClip(path, CombineMode.Replace);

            if (absences.Length > 0 && presence.Length > 0)
            {
                var half = cell.Height / 2;
                FillCell(graphics, new Rectangle(cell.X, cell.Y, cell.Width, half), absences[0]);
                FillCell(graphics, new Rectangle(cell.X, cell.Y + half, cell.Width, cell.Height - half), presence[0]);
            }
            else
            {
                FillCell(graphics, cell, items[0]);
                var minutes = items.Sum(movement => (movement.Hours * 60) + movement.Minutes);
                var colors = ColorsOf(items[0]);
                UiText.Draw(
                    graphics,
                    FormatHours(minutes),
                    Theme.Fonts.Tiny,
                    cell,
                    colors.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            graphics.Restore(state);

            // Segno dell'anomalia: contorno e pallino rossi (da controllare) o pallino ambra (insolito).
            if (severity is { } level)
            {
                var color = level == AlertSeverity.Check ? Theme.Danger : Theme.Warning;
                if (level == AlertSeverity.Check)
                {
                    using var outline = new Pen(color, 1.6f);
                    using var outlinePath = Theme.RoundedRectangle(cell, 5);
                    graphics.DrawPath(outline, outlinePath);
                }
                var dot = new Rectangle(cell.Right - 7, cell.Y - 2, 8, 8);
                using var halo = new SolidBrush(Theme.Surface);
                using var fill = new SolidBrush(color);
                graphics.FillEllipse(halo, Rectangle.Inflate(dot, 1, 1));
                graphics.FillEllipse(fill, dot);
            }
        }

        private void FillCell(Graphics graphics, Rectangle area, DailyMovement movement)
        {
            using var brush = new SolidBrush(ColorsOf(movement).Fill);
            graphics.FillRectangle(brush, area);
        }

        private (Color Fill, Color Ink) ColorsOf(DailyMovement movement) =>
            AttendanceMovementGenerator.IsPresence(movement)
                ? (Theme.SuccessSoft, Theme.SuccessInk)
                : AbsencePalette.Colors[_owner._codeColors.TryGetValue(movement.ReasonCode, out var index) ? index : 0];

        private static string FormatHours(int minutes) => minutes % 60 == 0
            ? (minutes / 60).ToString(CultureInfo.InvariantCulture)
            : (minutes / 60m).ToString("0.#", CultureInfo.InvariantCulture);
    }

    /// <summary>Legenda dei colori, una riga sotto il calendario.</summary>
    private sealed class Legend : Control
    {
        private List<(Color Fill, Color Ink, string Text, bool Dashed)> _entries = new();

        public Legend()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Theme.Surface;
        }

        public void SetEntries(IEnumerable<(Color Fill, Color Ink, string Text, bool Dashed)> entries)
        {
            _entries = entries.ToList();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var line = new Pen(Theme.Divider))
            {
                e.Graphics.DrawLine(line, 0, 0, Width, 0);
            }
            var x = 12;
            foreach (var entry in _entries)
            {
                var size = TextRenderer.MeasureText(entry.Text, Theme.Fonts.Tiny, new Size(int.MaxValue, 20), TextFormatFlags.NoPadding);
                if (x + 20 + size.Width > Width - 8)
                {
                    break;
                }
                var swatch = new Rectangle(x, (Height - 14) / 2, 14, 14);
                using (var path = Theme.RoundedRectangle(swatch, 4))
                using (var fill = new SolidBrush(entry.Fill))
                {
                    e.Graphics.FillPath(fill, path);
                    if (entry.Dashed)
                    {
                        using var pen = new Pen(entry.Ink, 1.2f) { DashStyle = DashStyle.Dash };
                        e.Graphics.DrawPath(pen, path);
                    }
                }
                UiText.Draw(
                    e.Graphics,
                    entry.Text,
                    Theme.Fonts.Tiny,
                    new Rectangle(x + 20, 0, size.Width + 4, Height),
                    Theme.MutedInk,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                x += 20 + size.Width + 18;
            }
        }
    }
}
