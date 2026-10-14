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
/// </summary>
internal sealed class MonthCalendarView : Panel
{
    private const int NameColumn = 190;
    private const int HeaderHeight = 44;
    private const int RowHeight = 28;
    private const int TotalColumn = 84;

    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    // Colori delle assenze (sfondo, testo): si assegnano ai giustificativi nell'ordine in cui compaiono.
    private static readonly (Color Fill, Color Ink)[] AbsencePalette =
    [
        (Color.FromArgb(207, 228, 255), Color.FromArgb(0, 88, 190)),
        (Color.FromArgb(255, 232, 200), Color.FromArgb(150, 80, 0)),
        (Color.FromArgb(236, 222, 255), Color.FromArgb(100, 50, 160)),
        (Color.FromArgb(255, 218, 216), Color.FromArgb(170, 30, 20)),
        (Color.FromArgb(205, 240, 246), Color.FromArgb(0, 96, 116)),
        (Color.FromArgb(240, 236, 200), Color.FromArgb(110, 100, 20))
    ];

    private readonly Canvas _canvas;
    private readonly Label _monthLabel = new();
    private readonly Button _previous = Theme.CreateButton("‹", Theme.ButtonTone.Neutral, compact: true);
    private readonly Button _next = Theme.CreateButton("›", Theme.ButtonTone.Neutral, compact: true);
    private readonly Button _save = Theme.CreateButton("Salva immagine", Theme.ButtonTone.Neutral, compact: true);
    private readonly Button _summary = Theme.CreateButton("Riepilogo CSV", Theme.ButtonTone.Neutral, compact: true);
    private readonly RoundedField _search = new();
    private readonly CheckBox _gapsOnly = new();
    private readonly Legend _legend = new();
    private readonly ToolTip _tip = new();

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

        var top = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.Surface, Padding = new Padding(12, 6, 12, 4) };
        _previous.Size = new Size(38, 32);
        _previous.Location = new Point(12, 6);
        _previous.AccessibleName = "Mese precedente";
        _previous.Click += (_, _) => Step(-1);
        _monthLabel.AutoSize = false;
        _monthLabel.Size = new Size(190, 32);
        _monthLabel.Location = new Point(56, 6);
        _monthLabel.TextAlign = ContentAlignment.MiddleCenter;
        _monthLabel.Font = Theme.Fonts.DialogTitle;
        _monthLabel.ForeColor = Theme.Ink;
        _next.Size = new Size(38, 32);
        _next.Location = new Point(252, 6);
        _next.AccessibleName = "Mese successivo";
        _next.Click += (_, _) => Step(1);
        _search.Size = new Size(200, 34);
        _search.Location = new Point(312, 5);
        _search.Placeholder = "Cerca dipendente";
        _search.Input.AccessibleName = "Cerca dipendente nel calendario";
        _gapsOnly.Text = "Solo giorni scoperti";
        _gapsOnly.AutoSize = true;
        _gapsOnly.Font = Theme.Fonts.Small;
        _gapsOnly.ForeColor = Theme.MutedInk;
        _gapsOnly.BackColor = Theme.Surface;
        _gapsOnly.Location = new Point(528, 12);
        _gapsOnly.AccessibleName = "Mostra solo i dipendenti con giorni lavorativi senza movimenti";
        _save.Size = new Size(140, 32);
        _save.Dock = DockStyle.Right;
        _save.AccessibleName = "Salva l'immagine del calendario";
        _save.Click += (_, _) => SaveImage();
        top.Controls.AddRange([_previous, _monthLabel, _next, _search, _gapsOnly]);
        _summary.Size = new Size(130, 32);
        _summary.Dock = DockStyle.Right;
        _summary.AccessibleName = "Esporta il riepilogo delle ore in un file CSV";
        _summary.Click += (_, _) => SaveSummary();
        top.Controls.Add(_save);
        top.Controls.Add(_summary);

        _legend.Dock = DockStyle.Bottom;
        _legend.Height = 34;

        _canvas = new Canvas(this) { Dock = DockStyle.Fill };

        // Prima il corpo, poi barra in alto e legenda in basso: l'ordine decide chi prende lo spazio.
        Controls.Add(_canvas);
        Controls.Add(_legend);
        Controls.Add(top);
        Clear();
        _search.TextChanged += (_, _) => ApplyFilter();
        _gapsOnly.CheckedChanged += (_, _) => ApplyFilter();
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
        _monthIndex = 0;
        UpdateNavigation();
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
                _codeColors[movement.ReasonCode] = _codeColors.Count % AbsencePalette.Length;
                _codeNames[movement.ReasonCode] = movement.Policy;
            }
        }
        _employees.AddRange(_byEmployee.Keys.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase));
        _months.AddRange(movements
            .Select(movement => (movement.Date.Year, movement.Date.Month))
            .Distinct()
            .OrderBy(month => month));
        _monthIndex = 0;

        var entries = new List<(Color Fill, Color Ink, string Text, bool Dashed)>
        {
            (Theme.SuccessSoft, Theme.SuccessInk, "Presenza", false)
        };
        foreach (var code in _codeColors)
        {
            var colors = AbsencePalette[code.Value];
            var name = _codeNames.TryGetValue(code.Key, out var policy) && policy.Length > 0 ? $"{code.Key} · {policy}" : code.Key;
            entries.Add((colors.Fill, colors.Ink, name, false));
        }
        if (_options.FillPresence)
        {
            entries.Add((Theme.Surface, Theme.Danger, "Giorno senza movimenti", true));
        }
        _legend.SetEntries(entries);
        UpdateNavigation();
        ApplyFilter();
    }

    private void Step(int delta)
    {
        var target = Math.Clamp(_monthIndex + delta, 0, Math.Max(0, _months.Count - 1));
        if (target == _monthIndex)
        {
            return;
        }
        _monthIndex = target;
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
            var gapsOnly = _gapsOnly.Checked && _options.FillPresence;
            foreach (var name in _employees)
            {
                if (text.Length > 0 && name.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) < 0)
                {
                    continue;
                }
                if (gapsOnly && StatsOf(name, year, month).Gaps == 0)
                {
                    continue;
                }
                _visible.Add(name);
            }
        }
        _canvas.RefreshLayout();
    }

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
            _save.Enabled = false;
            _summary.Enabled = false;
            _gapsOnly.Enabled = false;
            return;
        }
        _save.Enabled = true;
        _summary.Enabled = true;
        _gapsOnly.Enabled = _options.FillPresence;
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
            return header + Environment.NewLine + string.Join(Environment.NewLine, lines);
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
        if (picker.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }
        try
        {
            var rows = MonthlySummary.Build(_movements, _options, _profiles, _periodStart, _periodEnd);
            File.WriteAllText(picker.FileName, MonthlySummary.ToCsv(rows), new System.Text.UTF8Encoding(true));
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
        if (picker.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }
        try
        {
            using var image = _canvas.RenderImage(_monthLabel.Text);
            image.Save(picker.FileName, System.Drawing.Imaging.ImageFormat.Png);
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
            TextRenderer.DrawText(
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
                TextRenderer.DrawText(
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
                    DrawDay(graphics, cell, items);
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
                    TextRenderer.DrawText(
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
                TextRenderer.DrawText(
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
                TextRenderer.DrawText(
                    graphics,
                    _owner.FooterLabel,
                    Theme.Fonts.SmallBold,
                    new Rectangle(12, top, NameColumn - 18, RowHeight),
                    Theme.MutedInk,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            else
            {
                TextRenderer.DrawText(
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
                TextRenderer.DrawText(
                    graphics,
                    day.ToString(CultureInfo.InvariantCulture),
                    Theme.Fonts.SmallBold,
                    numberArea,
                    ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(
                    graphics,
                    date.ToString("ddd", Italian)[..1].ToUpper(Italian),
                    Theme.Fonts.Tiny,
                    new Rectangle(left, 26, cellWidth, 14),
                    holiday ? Theme.DangerInk : date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? Theme.FaintInk : Theme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            var totalLeft = NameColumn + (days * cellWidth) + scroll.X;
            TextRenderer.DrawText(
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
            TextRenderer.DrawText(
                graphics,
                MonthCalendarView.FormatTotal(minutes),
                Theme.Fonts.SmallBold,
                new Rectangle(left, top, 52, RowHeight),
                Theme.Ink,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (gaps == 0)
            {
                return;
            }
            var pill = new Rectangle(left + 58, top + 6, 22, RowHeight - 12);
            using var path = Theme.RoundedRectangle(pill, 6);
            using var fill = new SolidBrush(Theme.Danger);
            graphics.FillPath(fill, path);
            TextRenderer.DrawText(
                graphics,
                gaps > 99 ? "99+" : gaps.ToString(CultureInfo.InvariantCulture),
                Theme.Fonts.Tiny,
                pill,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        /// <summary>Un giorno con movimenti: assenza sopra e presenza sotto se ci sono entrambe.</summary>
        private void DrawDay(Graphics graphics, Rectangle cell, List<DailyMovement> items)
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
                TextRenderer.DrawText(
                    graphics,
                    FormatHours(minutes),
                    Theme.Fonts.Tiny,
                    cell,
                    colors.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            graphics.Restore(state);
        }

        private void FillCell(Graphics graphics, Rectangle area, DailyMovement movement)
        {
            using var brush = new SolidBrush(ColorsOf(movement).Fill);
            graphics.FillRectangle(brush, area);
        }

        private (Color Fill, Color Ink) ColorsOf(DailyMovement movement) =>
            AttendanceMovementGenerator.IsPresence(movement)
                ? (Theme.SuccessSoft, Theme.SuccessInk)
                : AbsencePalette[_owner._codeColors.TryGetValue(movement.ReasonCode, out var index) ? index : 0];

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
                TextRenderer.DrawText(
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
