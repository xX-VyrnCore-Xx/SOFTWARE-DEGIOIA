using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PresenzeXmlConverter.Wpf;

using TextBlock = System.Windows.Controls.TextBlock;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Cursors = System.Windows.Input.Cursors;
using DragDropEffects = System.Windows.DragDropEffects;
using Button = System.Windows.Controls.Button;
using Control = System.Windows.Controls.Control;
using Label = System.Windows.Controls.Label;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using Window = System.Windows.Window;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Orientation = System.Windows.Controls.Orientation;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Brush = System.Windows.Media.Brush;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using DataGrid = System.Windows.Controls.DataGrid;
using Binding = System.Windows.Data.Binding;

/// <summary>Una riga dello storico delle esportazioni.</summary>
internal sealed record ExportRow(ExportRecord Record, string When, string File, string Folder, string Movements, string Hours);

/// <summary>
/// «Storico esportazioni»: ogni XML salvato con data, contenuto e cartella. Si può aprire la cartella (anche con un doppio
/// clic), copiare il percorso, verificare che il file sul disco sia identico a quello consegnato e rispetti il tracciato,
/// risalvare la copia conservata nel database o svuotare l'elenco (i file su disco restano).
/// </summary>
internal sealed class HistoryView : Grid, IRefreshablePage
{
    private readonly HistoryService _service;
    private readonly Func<Window?> _owner;
    private readonly DataGrid _grid = new();
    private readonly TextBlock _summary;
    private readonly TextBlock _message;
    private readonly Button _open = new() { Content = "Apri cartella" };
    private readonly Button _verify = new() { Content = "Verifica file" };
    private readonly Button _saveCopy = new() { Content = "Salva copia…" };
    private readonly Button _copyPath = new() { Content = "Copia percorso" };
    private readonly Button _clear = new() { Content = "Svuota storico" };
    private IReadOnlyList<ExportRecord> _exports = Array.Empty<ExportRecord>();
    private readonly PillBar _tabs = new(["Consegne", "Andamento", "Residui"]);
    private readonly TrendView _trend;
    private readonly BalanceView _balance;

    public HistoryView(ConversionService conversion, Func<Window?> owner)
    {
        _service = new HistoryService(conversion.Database);
        _owner = owner;
        _summary = Ui.Text(ExportHistory.DescribeSummary([], DateTime.Now), 13.5, FontWeights.SemiBold, "Brush.InkSecondary");
        _message = Ui.Text("Doppio clic su una riga: apre la cartella.", 13, ink: "Brush.Muted", wrap: true);
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _trend = new TrendView(conversion.Database, () => conversion.CompanyCode);
        _balance = new BalanceView(conversion);
        Children.Add(new PageScaffold("Storico esportazioni", "Ogni file XML salvato, con data, contenuto e cartella di destinazione, e l'andamento delle ore mese per mese.", BuildTabs()));

        _open.Style = Ui.Style("Primary");
        _clear.Style = Ui.Style("Danger");
        _grid.SelectionChanged += (_, _) => UpdateButtons();
        _grid.MouseDoubleClick += (_, _) => OpenFolder();
        _open.Click += (_, _) => OpenFolder();
        _verify.Click += (_, _) => Verify();
        _saveCopy.Click += (_, _) => SaveCopy();
        _copyPath.Click += (_, _) => CopyPath();
        _clear.Click += (_, _) => Clear();
    }

    public void OnShown()
    {
        Refresh();
        _trend.Refresh();
        _balance.Refresh();
    }

    /// <summary>Le due viste dello storico: consegne e andamento (per i test).</summary>
    internal TrendView Trend => _trend;

    internal BalanceView Balance => _balance;

    internal PillBar Tabs => _tabs;

    private UIElement BuildTabs()
    {
        var deliveries = BuildBody();
        var trendCard = new Border
        {
            Background = Ui.Brush("Brush.Surface"),
            BorderBrush = Ui.Brush("Brush.Divider"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 0, 18),
            Child = _trend,
            Visibility = Visibility.Collapsed
        };
        var balanceCard = new Border
        {
            Background = Ui.Brush("Brush.Surface"),
            BorderBrush = Ui.Brush("Brush.Divider"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 0, 18),
            Child = _balance,
            Visibility = Visibility.Collapsed
        };
        var host = new Grid();
        host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        host.Children.Add(_tabs.WithMargin(new Thickness(0, 0, 0, 10)).At(0, 0));
        host.Children.Add(deliveries.At(1, 0));
        host.Children.Add(trendCard.At(1, 0));
        host.Children.Add(balanceCard.At(1, 0));
        _tabs.SelectionChanged += (_, _) =>
        {
            var tab = _tabs.SelectedIndex;
            deliveries.Visibility = tab == 0 ? Visibility.Visible : Visibility.Collapsed;
            trendCard.Visibility = tab == 1 ? Visibility.Visible : Visibility.Collapsed;
            balanceCard.Visibility = tab == 2 ? Visibility.Visible : Visibility.Collapsed;
            if (tab == 1)
            {
                _trend.Refresh();
            }
            else if (tab == 2)
            {
                _balance.Refresh();
            }
        };
        return host;
    }

    private ExportRecord? Selected => (_grid.SelectedItem as ExportRow)?.Record;

    private UIElement BuildBody()
    {
        _grid.Columns.Add(new DataGridTextColumn { Header = "Quando", Binding = new Binding(nameof(ExportRow.When)), Width = new DataGridLength(130) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "File XML", Binding = new Binding(nameof(ExportRow.File)), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Cartella", Binding = new Binding(nameof(ExportRow.Folder)), Width = new DataGridLength(1.6, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Movimenti", Binding = new Binding(nameof(ExportRow.Movements)), Width = new DataGridLength(100) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Ore", Binding = new Binding(nameof(ExportRow.Hours)), Width = new DataGridLength(110) });

        var inner = new Grid();
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        inner.Children.Add(_summary.WithMargin(new Thickness(18, 14, 18, 12)).At(0, 0));
        inner.Children.Add(_grid.At(1, 0));
        var card = new Border
        {
            Background = Ui.Brush("Brush.Surface"),
            BorderBrush = Ui.Brush("Brush.Divider"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Child = inner
        };
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0) };
        foreach (var button in new[] { _open, _verify, _saveCopy, _copyPath, _clear })
        {
            button.Margin = new Thickness(0, 0, 8, 0);
            buttons.Children.Add(button);
        }
        var body = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.Children.Add(card.At(0, 0));
        body.Children.Add(buttons.At(1, 0));
        body.Children.Add(_message.WithMargin(new Thickness(2, 10, 0, 0)).At(2, 0));
        return body;
    }

    private void Refresh()
    {
        try
        {
            _exports = _service.Load();
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            _message.Text = "Impossibile leggere lo storico: " + exception.Message;
            return;
        }
        var now = DateTime.Now;
        _grid.ItemsSource = _exports
            .Select(record => new ExportRow(
                record,
                ExportHistory.DescribeDay(record.LocalTime, now),
                record.FileName,
                Path.GetDirectoryName(record.FilePath) ?? string.Empty,
                record.Movements.ToString("N0"),
                record.TotalText))
            .ToList();
        _summary.Text = ExportHistory.DescribeSummary(_exports, now);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var has = Selected is not null;
        _open.IsEnabled = has;
        _verify.IsEnabled = has;
        _copyPath.IsEnabled = has;
        _saveCopy.IsEnabled = Selected?.HasStoredXml == true;
        _clear.IsEnabled = _exports.Count > 0;
    }

    private void OpenFolder()
    {
        if (Selected is not { } record || record.FilePath.Length == 0)
        {
            return;
        }
        var folder = Path.GetDirectoryName(record.FilePath);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            _message.Text = $"La cartella non esiste più: {folder}";
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(File.Exists(record.FilePath)
                ? new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{record.FilePath}\"") { UseShellExecute = true }
                : new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _message.Text = $"Impossibile aprire la cartella: {exception.Message}";
        }
    }

    private void Verify()
    {
        if (Selected is not { } record)
        {
            return;
        }
        try
        {
            var result = _service.Verify(record);
            _message.Text = result.Result == FileIntegrity.Identical
                ? $"File {record.FileName}: identico a quello consegnato."
                : $"File {record.FileName}: da controllare.";
            MessageBox.Show(
                _owner(),
                result.Message,
                "Verifica del file consegnato",
                MessageBoxButton.OK,
                result.Result == FileIntegrity.Identical && result.Problems.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            MessageBox.Show(_owner(), exception.Message, "Verifica del file non riuscita", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveCopy()
    {
        if (Selected is not { } record || !record.HasStoredXml)
        {
            return;
        }
        try
        {
            var xml = _service.StoredXml(record);
            if (xml is null)
            {
                _message.Text = "La copia dell'XML non è leggibile nel database.";
                return;
            }
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Salva una copia dell'XML consegnato",
                Filter = "File XML (*.xml)|*.xml",
                DefaultExt = "xml",
                AddExtension = true,
                FileName = record.FileName,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(_owner()) != true)
            {
                return;
            }
            File.WriteAllBytes(dialog.FileName, xml);
            _message.Text = $"Copia salvata: {dialog.FileName}";
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            MessageBox.Show(_owner(), exception.Message, "Copia dell'XML non riuscita", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopyPath()
    {
        if (Selected is not { } record || record.FilePath.Length == 0)
        {
            return;
        }
        try
        {
            System.Windows.Clipboard.SetText(record.FilePath);
            _message.Text = "Percorso del file copiato negli appunti.";
        }
        catch (System.Runtime.InteropServices.ExternalException exception)
        {
            _message.Text = "Copia del percorso non riuscita: " + exception.Message;
        }
    }

    private void Clear()
    {
        if (_exports.Count == 0)
        {
            return;
        }
        var answer = MessageBox.Show(
            _owner(),
            $"Elimino l'elenco delle {_exports.Count:N0} esportazioni registrate?{Environment.NewLine}{Environment.NewLine}" +
            "I file XML già salvati sul disco NON vengono toccati.",
            "Svuota lo storico",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }
        try
        {
            _service.Clear();
            Refresh();
            _message.Text = "Storico delle esportazioni svuotato.";
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            MessageBox.Show(_owner(), exception.Message, "Impossibile svuotare lo storico", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
