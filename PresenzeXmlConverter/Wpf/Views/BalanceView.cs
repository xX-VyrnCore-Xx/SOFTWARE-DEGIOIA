using System.Windows;
using System.Windows.Controls;

namespace PresenzeXmlConverter.Wpf;

using TextBlock = System.Windows.Controls.TextBlock;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using DataGrid = System.Windows.Controls.DataGrid;
using Binding = System.Windows.Data.Binding;
using ComboBox = System.Windows.Controls.ComboBox;

/// <summary>Una riga del saldo di ferie e permessi.</summary>
internal sealed record BalanceRow(string Employee, string Policy, string Entitled, string Used, string Remaining, string Status, System.Windows.Media.Brush Tone);

/// <summary>
/// «Residui» nello storico: per l'anno scelto, quanto di ferie e permessi ogni dipendente ha usato contro quanto gli
/// spetta. Le spettanze si indicano nella sezione Aziende; senza, la pagina spiega come fare.
/// </summary>
internal sealed class BalanceView : Grid
{
    private readonly ConversionService _service;
    private readonly ComboBox _companies = new() { MinWidth = 150, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ComboBox _years = new() { MinWidth = 90, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _summary;
    private readonly DataGrid _grid = new();
    private bool _filling;
    private string? _company;
    private int? _year;

    public BalanceView(ConversionService service)
    {
        _service = service;
        _summary = Ui.Text(string.Empty, 13.5, FontWeights.SemiBold, "Brush.InkSecondary", wrap: true);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { Margin = new Thickness(18, 14, 18, 12) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(_summary.At(0, 0));
        header.Children.Add(_companies.WithMargin(new Thickness(16, 0, 0, 0)).At(0, 1));
        header.Children.Add(_years.WithMargin(new Thickness(8, 0, 0, 0)).At(0, 2));
        Children.Add(header.At(0, 0));

        _grid.Columns.Add(Column("Dipendente", nameof(BalanceRow.Employee), star: 2));
        _grid.Columns.Add(Column("Giustificativo", nameof(BalanceRow.Policy), star: 1.2));
        foreach (var (title, property) in new[]
                 {
                     ("Spettano", nameof(BalanceRow.Entitled)),
                     ("Usate", nameof(BalanceRow.Used)),
                     ("Restano", nameof(BalanceRow.Remaining))
                 })
        {
            var right = new Style(typeof(TextBlock));
            right.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            var column = Column(title, property, pixels: 110);
            column.ElementStyle = right;
            _grid.Columns.Add(column);
        }
        var tone = new Style(typeof(TextBlock));
        tone.Setters.Add(new Setter(TextBlock.ForegroundProperty, new Binding(nameof(BalanceRow.Tone))));
        tone.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Stato",
            Binding = new Binding(nameof(BalanceRow.Status)),
            Width = new DataGridLength(120),
            ElementStyle = tone
        });
        Children.Add(_grid.At(1, 0));
        _companies.SelectionChanged += (_, args) =>
        {
            if (!_filling && args.AddedItems.Count > 0 && args.AddedItems[0] is string company)
            {
                _company = company;
                FillYears();
            }
        };
        _years.SelectionChanged += (_, args) =>
        {
            if (!_filling && args.AddedItems.Count > 0 && args.AddedItems[0] is int year)
            {
                _year = year;
                ShowSelected();
            }
        };
    }

    private static DataGridTextColumn Column(string header, string property, double star = 0, double pixels = 0) => new()
    {
        Header = header,
        Binding = new Binding(property),
        Width = star > 0 ? new DataGridLength(star, DataGridLengthUnitType.Star) : new DataGridLength(pixels)
    };

    /// <summary>Rilegge le aziende e gli anni con ore usate e mostra l'azienda in uso (o la prima).</summary>
    public void Refresh()
    {
        _filling = true;
        try
        {
            IReadOnlyList<string> companies;
            try
            {
                companies = _service.Database.GetCompanies().Where(company => company.Leave is { Count: > 0 }).Select(company => company.Code).ToArray();
            }
            catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
            {
                companies = Array.Empty<string>();
            }
            _companies.ItemsSource = companies;
            _companies.Visibility = companies.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            var current = _service.CompanyCode.Trim();
            _company = companies.FirstOrDefault(code => string.Equals(code, _company, StringComparison.OrdinalIgnoreCase))
                ?? companies.FirstOrDefault(code => string.Equals(code, current, StringComparison.OrdinalIgnoreCase))
                ?? companies.FirstOrDefault();
            _companies.SelectedItem = _company;
        }
        finally
        {
            _filling = false;
        }
        FillYears();
    }

    /// <summary>Il testo di sintesi (per i test).</summary>
    internal string SummaryText => _summary.Text;

    /// <summary>Le righe mostrate (per i test).</summary>
    internal IReadOnlyList<BalanceRow> Rows => (_grid.ItemsSource as IEnumerable<BalanceRow>)?.ToList() ?? [];

    private void FillYears()
    {
        IReadOnlyList<int> years = Array.Empty<int>();
        if (_company is { } company)
        {
            try
            {
                years = LeaveBalance.Years(_service.Database.GetAbsenceTotals(company));
            }
            catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
            {
                years = Array.Empty<int>();
            }
        }
        _year = _year is { } keep && years.Contains(keep) ? keep : years.Count > 0 ? years[0] : (int?)null;
        _filling = true;
        try
        {
            _years.ItemsSource = years;
            _years.SelectedItem = _year;
        }
        finally
        {
            _filling = false;
        }
        ShowSelected();
    }

    private void ShowSelected()
    {
        _grid.ItemsSource = null;
        if (_company is not { } company)
        {
            _summary.Text = "Per vedere i residui indica le spettanze di ferie e permessi nella sezione Aziende (pulsante «Ferie e permessi…»).";
            return;
        }
        if (_year is not { } year)
        {
            _summary.Text = $"Azienda {company}: nessuna ora usata conservata. Si riempie con le esportazioni.";
            return;
        }
        IReadOnlyList<LeaveBalanceRow> rows;
        try
        {
            rows = _service.LeaveBalanceFor(company, year);
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            _summary.Text = "Impossibile leggere i residui: " + exception.Message;
            return;
        }
        var over = rows.Count(row => row.Status == LeaveStatus.Over);
        var low = rows.Count(row => row.Status == LeaveStatus.Low);
        _summary.Text = $"Azienda {company} · anno {year} · {rows.Count:N0} saldi: {over:N0} oltre la spettanza, {low:N0} quasi esauriti.";
        _grid.ItemsSource = rows
            .OrderBy(row => row.Status == LeaveStatus.Over ? 0 : row.Status == LeaveStatus.Low ? 1 : 2)
            .ThenBy(row => row.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
            .Select(row => new BalanceRow(
                row.EmployeeName,
                row.Rule.Policy,
                LeaveBalance.Hours(row.EntitlementMinutes),
                LeaveBalance.Hours(row.UsedMinutes),
                row.RemainingMinutes >= 0 ? LeaveBalance.Hours(row.RemainingMinutes) : LeaveBalance.Signed(row.RemainingMinutes),
                row.Status switch { LeaveStatus.Over => "Oltre", LeaveStatus.Low => "Quasi esaurite", _ => "In regola" },
                Ui.Brush(row.Status switch { LeaveStatus.Over => "Brush.DangerInk", LeaveStatus.Low => "Brush.WarningInk", _ => "Brush.Muted" })))
            .ToList();
    }
}
