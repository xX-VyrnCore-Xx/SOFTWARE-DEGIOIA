using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PresenzeXmlConverter.Wpf;

using TextBlock = System.Windows.Controls.TextBlock;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Orientation = System.Windows.Controls.Orientation;
using Cursors = System.Windows.Input.Cursors;
using ToolTip = System.Windows.Controls.ToolTip;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

/// <summary>
/// Il calendario dell'anteprima: una riga per dipendente e una colonna per giorno, ogni giorno colorato per giustificativo
/// (presenza in verde, ogni assenza con il suo colore), i buchi tratteggiati in rosso, le anomalie con un segno. Si sfoglia
/// mese per mese, si cerca per nome, si guardano solo i dipendenti da controllare e si scorrono le anomalie una a una.
/// I dati vengono da <see cref="CalendarModel"/>; qui c'è solo ciò che si vede.
/// </summary>
internal sealed class CalendarView : Grid
{
    private readonly TextBlock _month = Ui.Text(string.Empty, 17, FontWeights.SemiBold);
    private readonly TextBlock _hint = Ui.Text(string.Empty, 12.5, ink: "Brush.Muted");
    private readonly Button _previous = new() { Content = "‹", Width = 38 };
    private readonly Button _next = new() { Content = "›", Width = 38 };
    private readonly TextBox _search = new() { Tag = "Cerca un dipendente", MinWidth = 200 };
    private readonly CheckBox _onlyChecks = new() { Content = "Solo da controllare", Style = Ui.Style("Toggle") };
    private readonly Border _alertBar = new() { CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 9, 10, 9), Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed };
    private readonly TextBlock _alertText = Ui.Text(string.Empty, 13, FontWeights.SemiBold, wrap: true);
    private readonly Button _alertNext = new() { Content = "Vai al primo" };
    private readonly WrapPanel _legend = new() { Margin = new Thickness(2, 10, 0, 0) };
    private readonly TextBlock _empty = Ui.Text("Genera l'anteprima per vedere il calendario del mese.", 14, ink: "Brush.Muted", wrap: true);
    private readonly ScrollViewer _scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly CalendarSurface _surface = new();
    private CalendarModel? _model;
    private int _monthIndex;
    private int _alertCursor = -1;

    public CalendarView()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var bar = new Grid { Margin = new Thickness(16, 14, 16, 10) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var nav = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        nav.Children.Add(_previous.WithMargin(new Thickness(0, 0, 6, 0)));
        nav.Children.Add(_next);
        var title = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(_month);
        title.Children.Add(_hint);
        bar.Children.Add(nav.At(0, 0));
        bar.Children.Add(title.At(0, 1));
        bar.Children.Add(_search.WithMargin(new Thickness(0, 0, 12, 0)).At(0, 3));
        bar.Children.Add(_onlyChecks.At(0, 4));
        Children.Add(bar.At(0, 0));

        var alertLayout = new Grid();
        alertLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        alertLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        alertLayout.Children.Add(_alertText.At(0, 0));
        alertLayout.Children.Add(_alertNext.WithMargin(new Thickness(12, 0, 0, 0)).At(0, 1));
        _alertBar.Child = alertLayout;
        _alertBar.Margin = new Thickness(16, 0, 16, 10);
        Children.Add(_alertBar.At(1, 0));

        _scroll.Content = _surface;
        _scroll.SizeChanged += (_, _) => _surface.AvailableWidth = _scroll.ViewportWidth;
        _scroll.ScrollChanged += (_, args) =>
        {
            if (args.ViewportWidthChange != 0)
            {
                _surface.AvailableWidth = _scroll.ViewportWidth;
            }
        };
        Children.Add(_scroll.WithMargin(new Thickness(8, 0, 8, 0)).At(2, 0));
        Children.Add(_empty.WithMargin(new Thickness(24, 20, 24, 20)).At(2, 0));
        Children.Add(_legend.WithMargin(new Thickness(16, 4, 16, 14)).At(3, 0));

        _previous.Click += (_, _) => Step(-1);
        _next.Click += (_, _) => Step(1);
        _search.TextChanged += (_, _) => ApplyFilter();
        _onlyChecks.Checked += (_, _) => ApplyFilter();
        _onlyChecks.Unchecked += (_, _) => ApplyFilter();
        _alertNext.Click += (_, _) => GoToNextAlert();
        UiMenus.Attach(_surface, () =>
        {
            if (_surface.HoveredDescription() is not { } text)
            {
                return [];
            }
            return
            [
                new MenuEntry("Copia i dettagli del giorno", Glyph.Sheet, () => UiMenus.Copy(text)),
                new MenuEntry("Vai alla prossima anomalia", Glyph.Alert, GoToNextAlert, _model is { AlertDays.Count: > 0 })
            ];
        });
        Clear();
    }

    /// <summary>Svuota il calendario (nessuna anteprima).</summary>
    public void Clear()
    {
        _model = null;
        _monthIndex = 0;
        _alertCursor = -1;
        _surface.Show(Array.Empty<CalendarRow>(), 0, 0, null, false);
        _legend.Children.Clear();
        _alertBar.Visibility = Visibility.Collapsed;
        _scroll.Visibility = Visibility.Collapsed;
        _empty.Visibility = Visibility.Visible;
        _month.Text = "Calendario";
        _hint.Text = string.Empty;
        _previous.IsEnabled = false;
        _next.IsEnabled = false;
        _search.IsEnabled = false;
        _onlyChecks.IsEnabled = false;
    }

    /// <summary>Riempie il calendario con i movimenti generati.</summary>
    public void SetData(
        IReadOnlyList<DailyMovement> movements,
        GenerationOptions options,
        IReadOnlyDictionary<string, EmployeeProfile>? profiles,
        DateTime? periodStart = null,
        DateTime? periodEnd = null)
    {
        SetModel(movements.Count == 0 ? null : CalendarModel.Create(movements, options, profiles, periodStart, periodEnd));
    }

    /// <summary>Mostra un calendario già calcolato (nullo = nessuna anteprima).</summary>
    public void SetModel(CalendarModel? model)
    {
        Clear();
        if (model is null || model.IsEmpty)
        {
            return;
        }
        _model = model;
        _surface.Model = _model;
        _monthIndex = 0;
        _empty.Visibility = Visibility.Collapsed;
        _scroll.Visibility = Visibility.Visible;
        _search.IsEnabled = true;
        _onlyChecks.IsEnabled = true;
        BuildLegend();
        UpdateAlertBar();
        ApplyFilter();
    }

    /// <summary>Il mese mostrato (per i test).</summary>
    internal string MonthText => _month.Text;

    /// <summary>Le righe mostrate (per i test).</summary>
    internal IReadOnlyList<CalendarRow> Rows => _surface.Rows;

    /// <summary>La barra delle anomalie (per i test).</summary>
    internal string AlertText => _alertBar.Visibility == Visibility.Visible ? _alertText.Text : string.Empty;

    internal int LegendCount => _legend.Children.Count;

    internal void Select(int monthIndex) => Step(monthIndex - _monthIndex);

    internal void Search(string text) => _search.Text = text;

    internal bool OnlyChecks
    {
        get => _onlyChecks.IsChecked == true;
        set => _onlyChecks.IsChecked = value;
    }

    internal void NextAlert() => GoToNextAlert();

    internal (string Name, DateTime Date)? Focus => _surface.Focus;

    private void Step(int delta)
    {
        if (_model is null)
        {
            return;
        }
        var target = Math.Clamp(_monthIndex + delta, 0, Math.Max(0, _model.Months.Count - 1));
        if (target == _monthIndex)
        {
            return;
        }
        _monthIndex = target;
        _surface.Focus = null;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (_model is null || _model.Months.Count == 0)
        {
            return;
        }
        var (year, month) = _model.Months[_monthIndex];
        var rows = _model.RowsFor(year, month, _search.Text, _onlyChecks.IsChecked == true);
        _surface.Show(rows, year, month, _model, _model.FillsPresence);
        _month.Text = CalendarModel.MonthName(year, month);
        _hint.Text = rows.Count == 0
            ? "Nessun dipendente corrisponde ai filtri."
            : $"{rows.Count:N0} {(rows.Count == 1 ? "dipendente" : "dipendenti")} · passa sopra un giorno per i dettagli";
        _previous.IsEnabled = _monthIndex > 0;
        _next.IsEnabled = _monthIndex < _model.Months.Count - 1;
    }

    private void BuildLegend()
    {
        _legend.Children.Clear();
        if (_model is null)
        {
            return;
        }
        foreach (var entry in _model.Legend)
        {
            _legend.Children.Add(Chip(entry.Text, CalendarSurface.FillOf(entry), CalendarSurface.InkOf(entry), dashed: false));
        }
        if (_model.FillsPresence)
        {
            _legend.Children.Add(Chip("Giorno senza movimenti", Colors.Transparent, Ui.Color("Brush.DangerInk"), dashed: true));
        }
        _legend.Children.Add(Dot("Oltre la giornata", Ui.Color("Brush.Danger")));
        _legend.Children.Add(Dot("Giorno insolito", Ui.Color("Brush.Warning")));
    }

    private static FrameworkElement Chip(string text, Color fill, Color ink, bool dashed)
    {
        var swatch = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(fill),
            BorderBrush = new SolidColorBrush(dashed ? ink : Colors.Transparent),
            BorderThickness = new Thickness(dashed ? 1.4 : 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 6) };
        row.Children.Add(swatch);
        row.Children.Add(Ui.Text(text, 12.5, ink: "Brush.InkSecondary").WithMargin(new Thickness(7, 0, 0, 0)));
        return row;
    }

    private static FrameworkElement Dot(string text, Color color)
    {
        var dot = new System.Windows.Shapes.Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0) };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 6) };
        row.Children.Add(dot);
        row.Children.Add(Ui.Text(text, 12.5, ink: "Brush.InkSecondary").WithMargin(new Thickness(9, 0, 0, 0)));
        return row;
    }

    private void UpdateAlertBar()
    {
        if (_model is null)
        {
            _alertBar.Visibility = Visibility.Collapsed;
            return;
        }
        var checks = _model.CheckDayCount;
        var notices = _model.NoticeDayCount;
        var differences = _model.DifferenceCount;
        if (checks + notices + differences == 0)
        {
            _alertBar.Visibility = Visibility.Collapsed;
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
        var severe = checks > 0;
        _alertText.Text = "Da controllare prima di esportare: " + string.Join(" · ", parts) + ".";
        _alertText.Foreground = Ui.Brush(severe ? "Brush.DangerInk" : "Brush.WarningInk");
        _alertBar.Background = Ui.Brush(severe ? "Brush.DangerSoft" : "Brush.WarningSoft");
        _alertNext.Visibility = _model.AlertDays.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _alertNext.Content = _alertCursor < 0 ? "Vai al primo" : "Vai al prossimo";
        _alertBar.Visibility = Visibility.Visible;
    }

    private void GoToNextAlert()
    {
        if (_model is null || _model.AlertDays.Count == 0)
        {
            return;
        }
        _alertCursor = (_alertCursor + 1) % _model.AlertDays.Count;
        var (name, date, _) = _model.AlertDays[_alertCursor];
        var monthIndex = _model.Months.ToList().IndexOf((date.Year, date.Month));
        if (monthIndex >= 0)
        {
            _monthIndex = monthIndex;
        }
        ApplyFilter();
        if (!_surface.Rows.Any(row => string.Equals(row.EmployeeName, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            // Un filtro nasconde proprio quel dipendente: si tolgono i filtri.
            _onlyChecks.IsChecked = false;
            _search.Text = string.Empty;
            ApplyFilter();
        }
        _surface.Focus = (name, date);
        var index = _surface.Rows.ToList().FindIndex(row => string.Equals(row.EmployeeName, name, StringComparison.CurrentCultureIgnoreCase));
        if (index >= 0)
        {
            _scroll.ScrollToVerticalOffset(Math.Max(0, CalendarSurface.HeaderHeight + (index * CalendarSurface.RowHeight) - (_scroll.ViewportHeight / 2)));
        }
        UpdateAlertBar();
    }

    /// <summary>La griglia vera e propria, disegnata una volta sola (un dipendente per riga, un giorno per colonna).</summary>
    private sealed class CalendarSurface : FrameworkElement
    {
        public const double HeaderHeight = 48;
        public const double RowHeight = 32;
        private const double NameColumn = 178;
        private const double TotalColumn = 86;
        private const double FooterHeight = 40;
        private const double MinimumDay = 22;
        private const double MaximumDay = 46;

        private readonly ToolTip _tip = new() { Placement = PlacementMode.Mouse, MaxWidth = 380 };
        private IReadOnlyList<CalendarRow> _rows = Array.Empty<CalendarRow>();
        private int _year;
        private int _month;
        private bool _fillsPresence;
        private (int Row, int Day)? _hover;
        private double _available;

        public CalendarSurface()
        {
            ToolTip = _tip;
            ToolTipService.SetIsEnabled(this, false);
            ToolTipService.SetInitialShowDelay(this, 120);
            ToolTipService.SetShowDuration(this, 20000);
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
        }

        public CalendarModel? Model { get; set; }

        /// <summary>Il testo del giorno sotto il mouse (per il menù del tasto destro), nullo se non c'è.</summary>
        public string? HoveredDescription()
        {
            if (_hover is not { } cell || Model is null || cell.Row >= _rows.Count)
            {
                return null;
            }
            var row = _rows[cell.Row];
            return Model.Describe(row.EmployeeName, row.Days[cell.Day].Date);
        }

        public IReadOnlyList<CalendarRow> Rows => _rows;

        public (string Name, DateTime Date)? Focus
        {
            get => _focus;
            set
            {
                _focus = value;
                InvalidateVisual();
            }
        }

        private (string Name, DateTime Date)? _focus;

        public double AvailableWidth
        {
            set
            {
                if (Math.Abs(_available - value) > 0.5)
                {
                    _available = value;
                    InvalidateMeasure();
                }
            }
        }

        public void Show(IReadOnlyList<CalendarRow> rows, int year, int month, CalendarModel? model, bool fillsPresence)
        {
            _rows = rows;
            _year = year;
            _month = month;
            _fillsPresence = fillsPresence;
            if (model is not null)
            {
                Model = model;
            }
            _hover = null;
            ToolTipService.SetIsEnabled(this, false);
            InvalidateMeasure();
            InvalidateVisual();
        }

        public static Color FillOf(CalendarLegendEntry entry)
        {
            if (entry.IsPresence)
            {
                return Ui.Color("Brush.SuccessSoft");
            }
            var color = AbsencePalette.For(entry.ColorIndex).Fill;
            return Color.FromRgb(color.R, color.G, color.B);
        }

        public static Color InkOf(CalendarLegendEntry entry)
        {
            if (entry.IsPresence)
            {
                return Ui.Color("Brush.SuccessInk");
            }
            var color = AbsencePalette.For(entry.ColorIndex).Ink;
            return Color.FromRgb(color.R, color.G, color.B);
        }

        private int Days => _rows.Count == 0 ? 0 : _rows[0].Days.Count;

        private double DayWidth
        {
            get
            {
                if (Days == 0)
                {
                    return MinimumDay;
                }
                var free = (_available > 0 ? _available : 0) - NameColumn - TotalColumn - 4;
                return Math.Clamp(free / Days, MinimumDay, MaximumDay);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = NameColumn + (Days * DayWidth) + TotalColumn + 4;
            var height = _rows.Count == 0 ? 0 : HeaderHeight + (_rows.Count * RowHeight) + FooterHeight + 6;
            return new Size(width, height);
        }

        private (int Row, int Day)? CellAt(Point point)
        {
            var x = point.X - NameColumn;
            var y = point.Y - HeaderHeight;
            if (x < 0 || y < 0 || Days == 0)
            {
                return null;
            }
            var day = (int)(x / DayWidth);
            var row = (int)(y / RowHeight);
            return day >= 0 && day < Days && row >= 0 && row < _rows.Count ? (row, day) : null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var cell = CellAt(e.GetPosition(this));
            if (cell == _hover)
            {
                return;
            }
            _hover = cell;
            if (cell is { } found && Model is not null)
            {
                var row = _rows[found.Row];
                _tip.Content = Model.Describe(row.EmployeeName, row.Days[found.Day].Date);
                ToolTipService.SetIsEnabled(this, true);
                Cursor = Cursors.Hand;
            }
            else
            {
                ToolTipService.SetIsEnabled(this, false);
                Cursor = null;
            }
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = null;
            ToolTipService.SetIsEnabled(this, false);
            Cursor = null;
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (CellAt(e.GetPosition(this)) is { } cell)
            {
                var row = _rows[cell.Row];
                Focus = (row.EmployeeName, row.Days[cell.Day].Date);
            }
        }

        private FormattedText Text(string text, double size, Brush brush, FontWeight? weight = null, double maxWidth = double.PositiveInfinity)
        {
            var formatted = new FormattedText(
                text,
                CultureInfo.GetCultureInfo("it-IT"),
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(Ui.Font, FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal),
                size,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxLineCount = 1,
                Trimming = TextTrimming.CharacterEllipsis
            };
            if (!double.IsInfinity(maxWidth))
            {
                formatted.MaxTextWidth = Math.Max(1, maxWidth);
            }
            return formatted;
        }

        private static SolidColorBrush Solid(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (_rows.Count == 0)
            {
                return;
            }
            var width = ActualWidth;
            var dayWidth = DayWidth;
            var surface = Solid(Ui.Color("Brush.Surface"));
            var band = Solid(Ui.Color("Brush.Canvas"));
            var divider = Solid(Ui.Color("Brush.Divider"));
            var ink = Ui.Brush("Brush.Ink");
            var muted = Ui.Brush("Brush.Muted");
            var inkSecondary = Ui.Brush("Brush.InkSecondary");
            var sunken = Solid(Ui.Color("Brush.Sunken"));
            var danger = Solid(Ui.Color("Brush.Danger"));
            var dangerInk = Ui.Brush("Brush.DangerInk");
            var warning = Solid(Ui.Color("Brush.Warning"));
            var brand = Solid(Ui.Color("Brush.Brand"));
            // Intestazione: lettera del giorno della settimana e numero; weekend e festivi in tono più spento.
            var first = _rows[0];
            for (var index = 0; index < first.Days.Count; index++)
            {
                var day = first.Days[index];
                var left = NameColumn + (index * dayWidth);
                var weekend = day.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                if (weekend)
                {
                    dc.DrawRectangle(sunken, null, new Rect(left, 0, dayWidth, HeaderHeight + (_rows.Count * RowHeight)));
                }
                var letter = Text(day.Date.ToString("ddd", CultureInfo.GetCultureInfo("it-IT")).Substring(0, 1).ToUpperInvariant(), 11, weekend ? muted : inkSecondary, FontWeights.Medium);
                var number = Text(day.Date.Day.ToString(CultureInfo.InvariantCulture), 13, weekend ? muted : ink, FontWeights.SemiBold);
                dc.DrawText(letter, new Point(left + ((dayWidth - letter.Width) / 2), 9));
                dc.DrawText(number, new Point(left + ((dayWidth - number.Width) / 2), 25));
            }
            var totalHeader = Text("Totale", 11.5, muted, FontWeights.Medium);
            dc.DrawText(totalHeader, new Point(NameColumn + (Days * dayWidth) + 14, 17));
            dc.DrawLine(new Pen(divider, 1), new Point(0, HeaderHeight), new Point(width, HeaderHeight));

            for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
            {
                var row = _rows[rowIndex];
                var top = HeaderHeight + (rowIndex * RowHeight);
                if (rowIndex % 2 == 1)
                {
                    dc.DrawRoundedRectangle(band, null, new Rect(0, top + 1, width, RowHeight - 2), 6, 6);
                }
                var name = Text(row.EmployeeName, 13, ink, FontWeights.Medium, NameColumn - 18);
                dc.DrawText(name, new Point(10, top + ((RowHeight - name.Height) / 2)));

                for (var index = 0; index < row.Days.Count; index++)
                {
                    DrawDay(dc, row.Days[index], new Rect(NameColumn + (index * dayWidth) + 1.5, top + 2.5, Math.Max(4, dayWidth - 3), RowHeight - 5), danger, warning, surface);
                    if (_hover == (rowIndex, index))
                    {
                        dc.DrawRoundedRectangle(null, new Pen(brand, 1.5), new Rect(NameColumn + (index * dayWidth) + 1, top + 2, Math.Max(4, dayWidth - 2), RowHeight - 4), 6, 6);
                    }
                    if (_focus is { } focus && string.Equals(focus.Name, row.EmployeeName, StringComparison.CurrentCultureIgnoreCase) && focus.Date.Date == row.Days[index].Date)
                    {
                        dc.DrawRoundedRectangle(null, new Pen(brand, 2.2), new Rect(NameColumn + (index * dayWidth), top + 1, dayWidth, RowHeight - 2), 7, 7);
                    }
                }

                var totalLeft = NameColumn + (row.Days.Count * dayWidth) + 14;
                var total = Text(CalendarModel.CellHours(row.TotalMinutes) + " h", 13, ink, FontWeights.SemiBold);
                dc.DrawText(total, new Point(totalLeft, top + (row.Gaps > 0 || row.DifferenceMinutes != 0 ? 3 : (RowHeight - total.Height) / 2)));
                if (row.Gaps > 0)
                {
                    var gaps = Text($"{row.Gaps} scopert{(row.Gaps == 1 ? "o" : "i")}", 10.5, dangerInk);
                    dc.DrawText(gaps, new Point(totalLeft, top + 17));
                }
                else if (row.DifferenceMinutes != 0)
                {
                    var difference = Text(CalendarModel.HoursText(Math.Abs(row.DifferenceMinutes)).Replace(" ", string.Empty) + (row.DifferenceMinutes > 0 ? " in più" : " in meno"), 10.5, dangerInk);
                    dc.DrawText(difference, new Point(totalLeft, top + 17));
                }
            }

            // Piè: quanti presenti (o assenti) ogni giorno fra i dipendenti mostrati.
            var footerTop = HeaderHeight + (_rows.Count * RowHeight) + 4;
            dc.DrawLine(new Pen(divider, 1), new Point(0, footerTop - 2), new Point(width, footerTop - 2));
            var label = Text(_fillsPresence ? "Presenti" : "Assenti", 12, muted, FontWeights.Medium);
            dc.DrawText(label, new Point(10, footerTop + 10));
            for (var index = 0; index < Days; index++)
            {
                var (present, absent) = CalendarModel.CountsOn(_rows, index);
                var value = _fillsPresence ? present : absent;
                var count = Text(value.ToString(CultureInfo.InvariantCulture), 12, value == 0 ? muted : inkSecondary, FontWeights.Medium);
                dc.DrawText(count, new Point(NameColumn + (index * dayWidth) + ((dayWidth - count.Width) / 2), footerTop + 10));
            }
        }

        private void DrawDay(DrawingContext dc, CalendarDay day, Rect cell, SolidColorBrush danger, SolidColorBrush warning, SolidColorBrush surface)
        {
            const double Radius = 6;
            switch (day.Kind)
            {
                case CalendarCellKind.Gap:
                    var dashed = new Pen(danger, 1.2) { DashStyle = new DashStyle([3, 2.5], 0) };
                    dc.DrawRoundedRectangle(null, dashed, cell, Radius, Radius);
                    break;
                case CalendarCellKind.Presence:
                    Filled(dc, cell, Ui.Color("Brush.SuccessSoft"), Ui.Brush("Brush.SuccessInk"), CalendarModel.CellHours(day.Minutes));
                    break;
                case CalendarCellKind.Absence:
                    {
                        var colors = AbsencePalette.For(day.AbsenceColor);
                        Filled(dc, cell, Color.FromRgb(colors.Fill.R, colors.Fill.G, colors.Fill.B), Solid(Color.FromRgb(colors.Ink.R, colors.Ink.G, colors.Ink.B)), CalendarModel.CellHours(day.Minutes));
                        break;
                    }
                case CalendarCellKind.Mixed:
                    {
                        var colors = AbsencePalette.For(day.AbsenceColor);
                        var shape = new RectangleGeometry(cell, Radius, Radius);
                        shape.Freeze();
                        dc.PushClip(shape);
                        var half = cell.Height / 2;
                        dc.DrawRectangle(Solid(Color.FromRgb(colors.Fill.R, colors.Fill.G, colors.Fill.B)), null, new Rect(cell.X, cell.Y, cell.Width, half));
                        dc.DrawRectangle(Solid(Ui.Color("Brush.SuccessSoft")), null, new Rect(cell.X, cell.Y + half, cell.Width, cell.Height - half));
                        dc.Pop();
                        break;
                    }
            }
            if (day.Alert is { } severity)
            {
                var color = severity == AlertSeverity.Check ? danger : warning;
                if (severity == AlertSeverity.Check)
                {
                    dc.DrawRoundedRectangle(null, new Pen(danger, 1.7), cell, Radius, Radius);
                }
                dc.DrawEllipse(surface, null, new Point(cell.Right - 1, cell.Top + 1), 5.5, 5.5);
                dc.DrawEllipse(color, null, new Point(cell.Right - 1, cell.Top + 1), 4, 4);
            }
        }

        private void Filled(DrawingContext dc, Rect cell, Color fill, Brush ink, string hours)
        {
            dc.DrawRoundedRectangle(Solid(fill), null, cell, 6, 6);
            if (cell.Width < 20)
            {
                return;
            }
            var text = Text(hours, cell.Width < 26 ? 10.5 : 11.5, ink, FontWeights.SemiBold);
            dc.DrawText(text, new Point(cell.X + ((cell.Width - text.Width) / 2), cell.Y + ((cell.Height - text.Height) / 2)));
        }
    }
}
