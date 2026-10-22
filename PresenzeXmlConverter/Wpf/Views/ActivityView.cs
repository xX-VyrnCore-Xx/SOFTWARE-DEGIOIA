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

/// <summary>Una riga del registro attività.</summary>
internal sealed record ActivityRow(ActivityEntry Entry, string When, string Kind, string Title, string Detail);

/// <summary>
/// «Registro attività»: cosa è stato fatto con il programma, con ricerca per testo, filtro per tipo, esportazione in CSV
/// delle voci mostrate e svuotamento.
/// </summary>
internal sealed class ActivityView : Grid, IRefreshablePage
{
    private readonly ActivityService _service;
    private readonly Func<Window?> _owner;
    private readonly DataGrid _grid = new();
    private readonly TextBox _search = new();
    private readonly PillBar _filter;
    private readonly TextBlock _summary;
    private readonly TextBlock _message;
    private readonly Button _export = new() { Content = "Esporta CSV…" };
    private readonly Button _clear = new() { Content = "Svuota registro" };
    private IReadOnlyList<ActivityEntry> _entries = Array.Empty<ActivityEntry>();
    private IReadOnlyList<ActivityEntry> _shown = Array.Empty<ActivityEntry>();

    public ActivityView(ConversionService conversion, Func<Window?> owner)
    {
        _service = new ActivityService(conversion.Database);
        _owner = owner;
        _summary = Ui.Text(ActivityLog.Summarize([], DateTime.Now), 13.5, FontWeights.SemiBold, "Brush.InkSecondary");
        _message = Ui.Text($"Si conservano le ultime {ActivityLog.MaximumEntries:N0} voci.", 13, ink: "Brush.Muted");
        _filter = new PillBar(new[] { "Tutte" }.Concat(Enum.GetValues<ActivityKind>().Select(ActivityLog.Label)));
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Children.Add(new PageScaffold(
            "Registro attività",
            "Cosa è stato fatto: importazioni, esportazioni, modifiche ai codici e alle aziende, backup e aggiornamenti.",
            BuildBody()));

        _export.Style = Ui.Style("Primary");
        _clear.Style = Ui.Style("Danger");
        _search.Tag = "Cerca nel registro…";
        _search.TextChanged += (_, _) => ApplyFilter();
        _filter.SelectionChanged += (_, _) => ApplyFilter();
        _export.Click += (_, _) => ExportCsv();
        _clear.Click += (_, _) => Clear();
    }

    public void OnShown() => Refresh();

    private UIElement BuildBody()
    {
        _grid.Columns.Add(new DataGridTextColumn { Header = "Quando", Binding = new Binding(nameof(ActivityRow.When)), Width = new DataGridLength(130) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Tipo", Binding = new Binding(nameof(ActivityRow.Kind)), Width = new DataGridLength(120) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Azione", Binding = new Binding(nameof(ActivityRow.Title)), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Dettaglio", Binding = new Binding(nameof(ActivityRow.Detail)), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star) });

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
        var buttons = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
        _export.Margin = new Thickness(0, 0, 8, 0);
        buttons.Children.Add(_export);
        buttons.Children.Add(_clear);

        var body = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.Children.Add(_search.WithMargin(new Thickness(0, 0, 0, 10)).At(0, 0));
        body.Children.Add(_filter.At(1, 0));
        body.Children.Add(card.At(2, 0));
        var footer = new StackPanel();
        footer.Children.Add(buttons);
        footer.Children.Add(_message.WithMargin(new Thickness(2, 8, 0, 0)));
        body.Children.Add(footer.At(3, 0));
        return body;
    }

    private void Refresh()
    {
        try
        {
            _entries = _service.Load();
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            _message.Text = "Impossibile leggere il registro: " + exception.Message;
            return;
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        ActivityKind? kind = _filter.SelectedIndex > 0 ? Enum.GetValues<ActivityKind>()[_filter.SelectedIndex - 1] : null;
        _shown = ActivityService.Filter(_entries, _search.Text, kind);
        var now = DateTime.Now;
        _grid.ItemsSource = _shown
            .Select(entry => new ActivityRow(entry, ActivityLog.DescribeWhen(entry.LocalTime, now), ActivityLog.Label(entry.Kind), entry.Title, entry.Detail))
            .ToList();
        var summary = ActivityLog.Summarize(_shown, now);
        _summary.Text = _shown.Count == _entries.Count || _entries.Count == 0
            ? summary
            : $"{_shown.Count:N0} di {_entries.Count:N0} · {summary}";
        _export.IsEnabled = _shown.Count > 0;
        _clear.IsEnabled = _entries.Count > 0;
    }

    private void ExportCsv()
    {
        if (_shown.Count == 0)
        {
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Esporta il registro delle attività",
            Filter = "File CSV (*.csv)|*.csv",
            DefaultExt = "csv",
            AddExtension = true,
            FileName = $"registro-attivita-{DateTime.Now:yyyyMMdd}.csv",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(_owner()) != true)
        {
            return;
        }
        try
        {
            File.WriteAllText(dialog.FileName, ActivityService.ToCsv(_shown), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            _message.Text = $"Registro esportato: {dialog.FileName}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(_owner(), exception.Message, "Esportazione del registro non riuscita", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Clear()
    {
        var answer = MessageBox.Show(
            _owner(),
            "Vuoi eliminare tutte le voci del registro delle attività?" + Environment.NewLine +
            "Codici, aziende ed esportazioni non vengono toccati.",
            "Svuota registro",
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
            _message.Text = "Registro delle attività svuotato.";
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            MessageBox.Show(_owner(), exception.Message, "Impossibile svuotare il registro", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
