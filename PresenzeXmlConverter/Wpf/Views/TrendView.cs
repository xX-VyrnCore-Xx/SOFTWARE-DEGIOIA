using System.Windows;
using System.Windows.Controls;

namespace PresenzeXmlConverter.Wpf;

using TextBlock = System.Windows.Controls.TextBlock;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using DataGrid = System.Windows.Controls.DataGrid;
using Binding = System.Windows.Data.Binding;
using ComboBox = System.Windows.Controls.ComboBox;

/// <summary>Una riga del confronto fra l'ultimo mese e quello prima.</summary>
internal sealed record TrendRow(string Employee, string Total, string Previous, string Delta, string Percent, string Absence);

/// <summary>
/// «Andamento» nello storico: ore consegnate mese per mese per un'azienda (grafico a colonne) e, sotto, chi è cambiato di
/// più fra l'ultimo mese e quello prima. I dati si conservano a ogni esportazione; senza consegne la pagina lo dice.
/// </summary>
internal sealed class TrendView : Grid
{
    private readonly LocalDatabase _database;
    private readonly Func<string> _currentCompany;
    private readonly ComboBox _companies = new() { MinWidth = 180, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _summary;
    private readonly Grid _chart = new() { Height = 190, Margin = new Thickness(18, 4, 18, 14) };
    private readonly DataGrid _grid = new();

    public TrendView(LocalDatabase database, Func<string> currentCompany)
    {
        _database = database;
        _currentCompany = currentCompany;
        _summary = Ui.Text(string.Empty, 13.5, FontWeights.SemiBold, "Brush.InkSecondary", wrap: true);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { Margin = new Thickness(18, 14, 18, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(_summary.At(0, 0));
        header.Children.Add(_companies.WithMargin(new Thickness(16, 0, 0, 0)).At(0, 1));
        Children.Add(header.At(0, 0));
        Children.Add(_chart.At(1, 0));

        _grid.Columns.Add(Column("Dipendente", nameof(TrendRow.Employee), star: 2));
        foreach (var (title, property) in new[]
                 {
                     ("Ultimo mese", nameof(TrendRow.Total)),
                     ("Mese prima", nameof(TrendRow.Previous)),
                     ("Differenza", nameof(TrendRow.Delta)),
                     ("%", nameof(TrendRow.Percent)),
                     ("Di cui assenza", nameof(TrendRow.Absence))
                 })
        {
            _grid.Columns.Add(Column(title, property, pixels: title == "Di cui assenza" ? 130 : 110).RightAligned());
        }
        Children.Add(_grid.At(2, 0));
        _companies.SelectionChanged += (_, _) => ShowSelected();
    }

    private static DataGridTextColumn Column(string header, string property, double star = 0, double pixels = 0) => new()
    {
        Header = header,
        Binding = new Binding(property),
        Width = star > 0 ? new DataGridLength(star, DataGridLengthUnitType.Star) : new DataGridLength(pixels)
    };

    /// <summary>Rilegge le aziende con dati e mostra quella in uso (o la prima).</summary>
    public void Refresh()
    {
        IReadOnlyList<string> companies;
        try
        {
            companies = _database.GetMonthlyCompanies();
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            companies = Array.Empty<string>();
        }
        var selected = _companies.SelectedItem as string;
        _companies.ItemsSource = companies;
        _companies.Visibility = companies.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        var current = _currentCompany().Trim();
        _companies.SelectedItem = companies.FirstOrDefault(code => string.Equals(code, selected, StringComparison.OrdinalIgnoreCase))
            ?? companies.FirstOrDefault(code => string.Equals(code, current, StringComparison.OrdinalIgnoreCase))
            ?? companies.FirstOrDefault();
        ShowSelected();
    }

    /// <summary>Il testo di sintesi mostrato in alto (per i test).</summary>
    internal string SummaryText => _summary.Text;

    /// <summary>Le righe del confronto mostrate (per i test).</summary>
    internal IReadOnlyList<TrendRow> Rows => (_grid.ItemsSource as IEnumerable<TrendRow>)?.ToList() ?? [];

    /// <summary>Quante colonne ha il grafico (per i test).</summary>
    internal int ChartColumns => _chart.ColumnDefinitions.Count;

    private void ShowSelected()
    {
        _chart.Children.Clear();
        _chart.ColumnDefinitions.Clear();
        if (_companies.SelectedItem is not string company)
        {
            _summary.Text = "Dopo la prima esportazione qui compare l'andamento delle ore mese su mese.";
            _grid.ItemsSource = null;
            return;
        }
        IReadOnlyList<MonthlyTotal> totals;
        try
        {
            totals = _database.GetMonthlyTotals(company);
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            _summary.Text = "Impossibile leggere l'andamento: " + exception.Message;
            return;
        }
        var series = MonthlyTrend.Series(totals);
        DrawChart(series);
        var months = MonthlyTrend.LatestMonths(totals);
        _summary.Text = months is null
            ? "Nessun dato per questa azienda."
            : months.Value.Previous is { } previous
                ? $"Azienda {company} · {series.Count} {(series.Count == 1 ? "mese" : "mesi")} consegnati. Ultimo mese: {MonthlyTrend.MonthName(months.Value.Latest)} contro {MonthlyTrend.MonthName(previous)}."
                : $"Azienda {company} · un solo mese consegnato ({MonthlyTrend.MonthName(months.Value.Latest)}): dal prossimo comparirà il confronto.";
        _grid.ItemsSource = MonthlyTrend.Changes(totals)
            .Select(change => new TrendRow(
                change.EmployeeName,
                MonthlyTrend.Hours(change.TotalMinutes),
                change.PreviousMinutes > 0 ? MonthlyTrend.Hours(change.PreviousMinutes) : "—",
                change.PreviousMinutes > 0 ? MonthlyTrend.SignedHours(change.Delta) : "nuovo",
                MonthlyTrend.PercentText(change.Percent),
                MonthlyTrend.Hours(change.AbsenceMinutes)))
            .ToList();
    }

    private void DrawChart(IReadOnlyList<MonthPoint> series)
    {
        var maximum = series.Count == 0 ? 0 : series.Max(point => point.TotalMinutes);
        for (var index = 0; index < series.Count; index++)
        {
            _chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var point = series[index];
            var latest = index == series.Count - 1;
            var column = new Grid { Margin = new Thickness(6, 0, 6, 0) };
            column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            column.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            column.Children.Add(Ui.Text(MonthlyTrend.Hours(point.TotalMinutes), 12, latest ? FontWeights.SemiBold : FontWeights.Normal, latest ? "Brush.BrandInk" : "Brush.Muted").WithMargin(new Thickness(0, 0, 0, 4)).At(0, 0));
            var barArea = new Grid();
            var ratio = maximum == 0 ? 0 : (double)point.TotalMinutes / maximum;
            var bar = new Border
            {
                Background = Ui.Brush(latest ? "Brush.Brand" : "Brush.BrandSoft"),
                CornerRadius = new CornerRadius(6, 6, 2, 2),
                VerticalAlignment = VerticalAlignment.Bottom,
                MinHeight = 3,
                Height = 0,
                Tag = ratio
            };
            barArea.SizeChanged += (_, _) => bar.Height = Math.Max(3, barArea.ActualHeight * (double)bar.Tag);
            barArea.Children.Add(bar);
            column.Children.Add(barArea.At(1, 0));
            column.Children.Add(Ui.Text(MonthlyTrend.ShortMonth(point.Month), 12, ink: "Brush.Muted").WithMargin(new Thickness(0, 6, 0, 0)).At(2, 0));
            System.Windows.Automation.AutomationProperties.SetName(column, $"{MonthlyTrend.MonthName(point.Month)}: {MonthlyTrend.Hours(point.TotalMinutes)} da {point.Employees} dipendenti");
            _chart.Children.Add(column.At(0, index));
        }
    }
}
