using System.Drawing.Drawing2D;
using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>
/// Calendario del mese: una riga per dipendente e una colonna per giorno. Ogni giorno è colorato
/// secondo il giustificativo (presenza in verde, ogni assenza con il suo colore), così si vedono a
/// colpo d'occhio i buchi (giorni lavorativi senza nulla, con il contorno rosso tratteggiato) e le
/// sovrapposizioni. Si sfoglia mese per mese quando i movimenti ne toccano più d'uno.
/// </summary>
internal sealed class MonthCalendarView : Panel
{
    private const int NameColumn = 190;
    private const int HeaderHeight = 44;
    private const int RowHeight = 28;

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
    private readonly Legend _legend = new();
    private readonly ToolTip _tip = new();

    private readonly List<(int Year, int Month)> _months = new();
    private readonly List<string> _employees = new();
    private readonly Dictionary<string, List<DailyMovement>> _byEmployee = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly Dictionary<string, int> _codeColors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _codeNames = new(StringComparer.OrdinalIgnoreCase);
    private GenerationOptions _options = GenerationOptions.Default;
    private IReadOnlyDictionary<string, EmployeeProfile> _profiles = new Dictionary<string, EmployeeProfile>();
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
        top.Controls.AddRange([_previous, _monthLabel, _next]);

        _legend.Dock = DockStyle.Bottom;
        _legend.Height = 34;

        _canvas = new Canvas(this) { Dock = DockStyle.Fill };

        // Prima il corpo, poi barra in alto e legenda in basso: l'ordine decide chi prende lo spazio.
        Controls.Add(_canvas);
        Controls.Add(_legend);
        Controls.Add(top);
        Clear();
    }

    /// <summary>Svuota il calendario (nessuna anteprima generata).</summary>
    public void Clear()
    {
        _months.Clear();
        _employees.Clear();
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
        IReadOnlyDictionary<string, EmployeeProfile>? profiles)
    {
        Clear();
        _options = options;
        _profiles = profiles ?? new Dictionary<string, EmployeeProfile>();
        if (movements.Count == 0)
        {
            return;
        }

        foreach (var movement in movements)
        {
            if (!_byEmployee.TryGetValue(movement.EmployeeName, out var list))
            {
                list = new List<DailyMovement>();
                _byEmployee[movement.EmployeeName] = list;
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
        _canvas.RefreshLayout();
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
        _canvas.RefreshLayout();
    }

    private void UpdateNavigation()
    {
        if (_months.Count == 0)
        {
            _monthLabel.Text = "Calendario";
            _previous.Enabled = false;
            _next.Enabled = false;
            return;
        }
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
        var list = _byEmployee.TryGetValue(name, out var found) ? found : new List<DailyMovement>();
        var lines = list
            .Where(movement => movement.Date.Date == day.Date)
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

    /// <summary>Superficie scorrevole dove si dipinge la griglia.</summary>
    private sealed class Canvas : ScrollableControl
    {
        private readonly MonthCalendarView _owner;

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

        private int CellWidth => Math.Clamp((ClientSize.Width - NameColumn - 8) / Math.Max(1, Days), 18, 30);

        public void RefreshLayout()
        {
            AutoScrollPosition = Point.Empty;
            AutoScrollMinSize = _owner._months.Count == 0
                ? Size.Empty
                : new Size(
                    NameColumn + (Days * CellWidth) + 8,
                    HeaderHeight + (_owner._employees.Count * RowHeight) + 6);
            Invalidate();
        }

        protected override void OnResize(EventArgs eventArgs)
        {
            base.OnResize(eventArgs);
            if (_owner._months.Count > 0)
            {
                AutoScrollMinSize = new Size(
                    NameColumn + (Days * CellWidth) + 8,
                    HeaderHeight + (_owner._employees.Count * RowHeight) + 6);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var text = TipAt(e.Location);
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
        }

        private string TipAt(Point point)
        {
            if (_owner._months.Count == 0 || point.Y < HeaderHeight)
            {
                return string.Empty;
            }
            var scroll = AutoScrollPosition;
            var row = (point.Y - scroll.Y - HeaderHeight) / RowHeight;
            var column = point.X < NameColumn ? -1 : (point.X - scroll.X - NameColumn) / CellWidth;
            if (row < 0 || row >= _owner._employees.Count || column < 0 || column >= Days)
            {
                return string.Empty;
            }
            var (year, month) = _owner._months[_owner._monthIndex];
            return _owner.DescribeDay(_owner._employees[row], new DateTime(year, month, column + 1));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (_owner._months.Count == 0)
            {
                TextRenderer.DrawText(
                    graphics,
                    "Genera l'anteprima per vedere il calendario del mese.",
                    Theme.Fonts.Small,
                    ClientRectangle,
                    Theme.MutedInk,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            var (year, month) = _owner._months[_owner._monthIndex];
            var cellWidth = CellWidth;
            var scroll = AutoScrollPosition;
            using var holidayFill = new SolidBrush(Theme.Sunken);
            using var gapPen = new Pen(Theme.Danger, 1.2f) { DashStyle = DashStyle.Dash };

            for (var row = 0; row < _owner._employees.Count; row++)
            {
                var top = HeaderHeight + (row * RowHeight) + scroll.Y;
                if (top + RowHeight < HeaderHeight || top > ClientSize.Height)
                {
                    continue;
                }
                var name = _owner._employees[row];
                var profile = _owner.ProfileOf(name);
                var list = _owner._byEmployee[name];
                if (row % 2 == 1)
                {
                    using var band = new SolidBrush(Theme.AltRow);
                    graphics.FillRectangle(band, 0, top, ClientSize.Width, RowHeight);
                }

                for (var day = 1; day <= Days; day++)
                {
                    var date = new DateTime(year, month, day);
                    var cell = new Rectangle(NameColumn + ((day - 1) * cellWidth) + scroll.X + 1, top + 2, cellWidth - 2, RowHeight - 4);
                    var working = _owner.IsWorkingDay(date, profile);
                    if (!working)
                    {
                        graphics.FillRectangle(holidayFill, cell);
                    }
                    var items = list.Where(movement => movement.Date.Date == date).ToArray();
                    if (items.Length == 0)
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
            }

            // Colonna dei nomi e intestazione: restano ferme mentre il resto scorre.
            using (var back = new SolidBrush(Theme.Surface))
            {
                graphics.FillRectangle(back, 0, 0, NameColumn, ClientSize.Height);
                graphics.FillRectangle(back, 0, 0, ClientSize.Width, HeaderHeight);
            }
            for (var row = 0; row < _owner._employees.Count; row++)
            {
                var top = HeaderHeight + (row * RowHeight) + scroll.Y;
                if (top + RowHeight < HeaderHeight || top > ClientSize.Height)
                {
                    continue;
                }
                TextRenderer.DrawText(
                    graphics,
                    _owner._employees[row],
                    Theme.Fonts.Small,
                    new Rectangle(12, top, NameColumn - 18, RowHeight),
                    Theme.Ink,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            for (var day = 1; day <= Days; day++)
            {
                var date = new DateTime(year, month, day);
                var left = NameColumn + ((day - 1) * cellWidth) + scroll.X;
                var ink = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? Theme.FaintInk : Theme.MutedInk;
                TextRenderer.DrawText(
                    graphics,
                    day.ToString(CultureInfo.InvariantCulture),
                    Theme.Fonts.SmallBold,
                    new Rectangle(left, 6, cellWidth, 18),
                    ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(
                    graphics,
                    date.ToString("ddd", Italian)[..1].ToUpper(Italian),
                    Theme.Fonts.Tiny,
                    new Rectangle(left, 24, cellWidth, 16),
                    ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            using var line = new Pen(Theme.Divider);
            graphics.DrawLine(line, 0, HeaderHeight - 1, ClientSize.Width, HeaderHeight - 1);
        }

        /// <summary>Un giorno con movimenti: assenza sopra e presenza sotto se ci sono entrambe.</summary>
        private void DrawDay(Graphics graphics, Rectangle cell, DailyMovement[] items)
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
                var minutes = items.Sum(movement => movement.Hours * 60 + movement.Minutes);
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
