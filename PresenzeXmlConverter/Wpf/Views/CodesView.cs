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

/// <summary>
/// La sezione «Gestione codici»: due tabelle modificabili, Dipendenti (codice, ore al giorno e rapporto di lavoro) e
/// Giustificativi (codice PAGHE di ogni «Policy» del foglio). Si salva con il pulsante o da sé poco dopo l'ultima modifica,
/// anche a metà, come nella finestra classica: i codici inseriti non si perdono.
/// </summary>
internal sealed class CodesView : Grid, IRefreshablePage
{
    private readonly ConversionService _conversion;
    private readonly CodesService _service;
    private readonly Func<Window?> _owner;
    private readonly DataGrid _employees = new();
    private readonly DataGrid _policies = new();
    private readonly TextBlock _status;
    private readonly List<(Border Pill, TextBlock Text, FrameworkElement Page)> _tabs = new();
    private readonly System.Windows.Threading.DispatcherTimer _autosave = new() { Interval = TimeSpan.FromMilliseconds(1200) };
    private bool _dirty;
    private bool _loaded;

    public CodesView(ConversionService conversion, Func<Window?> owner, bool inDialog = false)
    {
        _conversion = conversion;
        _service = new CodesService(conversion.Database);
        _owner = owner;
        _status = Ui.Text("Codici salvati su questo PC.", 13.5, ink: "Brush.Muted", wrap: true);
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var transfer = new TransferButton(conversion, owner, text => _status.Text = text, () => Load(force: true));
        var save = new Button { Content = "Salva abbinamenti", Style = Ui.Style("Primary") };
        var reload = new Button { Content = "Ricarica" };
        var delete = new Button { Content = "Elimina riga", Style = Ui.Style("Danger") };
        if (inDialog)
        {
            // Dentro una finestra di dialogo: niente titolo di pagina; i pulsanti stanno in una riga sopra le tabelle.
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            foreach (var button in new[] { transfer.Button, reload, delete })
            {
                button.Margin = new Thickness(0, 0, 8, 0);
                bar.Children.Add(button);
            }
            var stack = new Grid();
            stack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            stack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            stack.Children.Add(bar.At(0, 0));
            stack.Children.Add(BuildBody().At(1, 0));
            Children.Add(stack);
        }
        else
        {
            var page = new PageScaffold("Gestione codici", "Gli abbinamenti fra i valori del foglio e i codici ufficiali PAGHE.", BuildBody());
            page.AddAction(transfer.Button);
            page.AddAction(reload);
            page.AddAction(delete);
            page.AddAction(save);
            Children.Add(page);
        }

        save.Click += (_, _) => SaveNow();
        reload.Click += (_, _) => Load(force: true);
        delete.Click += (_, _) => DeleteSelected();
        _autosave.Tick += (_, _) =>
        {
            _autosave.Stop();
            if (_dirty)
            {
                SaveSilently();
            }
        };
    }

    public void OnShown() => Load(force: false);

    /// <summary>Salva quanto scritto (anche a metà) e dice quanti codici richiesti dal foglio mancano ancora.</summary>
    internal int CommitAndCountMissing()
    {
        SaveNow();
        return _service.CountMissing();
    }

    private DataGrid CurrentGrid => _tabs[0].Page.Visibility == Visibility.Visible ? _employees : _policies;

    private UIElement BuildBody()
    {
        ConfigureGrid(_employees, withProfile: true);
        ConfigureGrid(_policies, withProfile: false);
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        var content = new Grid();
        AddTab(bar, content, "Dipendenti", _employees);
        AddTab(bar, content, "Giustificativi", _policies);
        var card = new Border
        {
            Background = Ui.Brush("Brush.Surface"),
            BorderBrush = Ui.Brush("Brush.Divider"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Child = content
        };
        var body = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.Children.Add(bar.At(0, 0));
        body.Children.Add(card.At(1, 0));
        body.Children.Add(_status.WithMargin(new Thickness(2, 12, 0, 0)).At(2, 0));
        SelectTab(0);
        return body;
    }

    private void ConfigureGrid(DataGrid grid, bool withProfile)
    {
        grid.IsReadOnly = false;
        grid.SelectionUnit = DataGridSelectionUnit.FullRow;
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = withProfile ? "Dipendente / nome Excel" : "Valore della colonna Policy",
            Binding = new Binding(nameof(CodeRow.DisplayName)) { Mode = System.Windows.Data.BindingMode.OneWay },
            IsReadOnly = true,
            Width = new DataGridLength(withProfile ? 2.4 : 2, DataGridLengthUnitType.Star)
        });
        grid.Columns.Add(EditableColumn(withProfile ? "Codice PAGHE" : "Codice giustificativo", nameof(CodeRow.Code), star: 1.3));
        if (withProfile)
        {
            grid.Columns.Add(EditableColumn("Ore/giorno", nameof(CodeRow.Hours), pixels: 110));
            grid.Columns.Add(EditableColumn("Assunto dal", nameof(CodeRow.Start), pixels: 120));
            grid.Columns.Add(EditableColumn("Fino al", nameof(CodeRow.End), pixels: 120));
        }
        grid.CellEditEnding += (_, e) =>
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                MarkDirty();
            }
        };
        grid.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Delete && grid.SelectedItem is not null && !IsEditing(grid))
            {
                DeleteSelected();
                e.Handled = true;
            }
        };
    }

    private static bool IsEditing(DataGrid grid) => grid.CurrentCell.Column?.GetCellContent(grid.CurrentCell.Item) is TextBox;

    private static DataGridTextColumn EditableColumn(string header, string property, double star = 0, double pixels = 0)
    {
        var editing = new Style(typeof(TextBox));
        editing.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        editing.Setters.Add(new Setter(Control.BackgroundProperty, Ui.Brush("Brush.BrandLite")));
        editing.Setters.Add(new Setter(Control.ForegroundProperty, Ui.Brush("Brush.Ink")));
        editing.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0, 6, 0, 6)));
        editing.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(property) { Mode = System.Windows.Data.BindingMode.TwoWay, UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus },
            EditingElementStyle = editing,
            Width = star > 0 ? new DataGridLength(star, DataGridLengthUnitType.Star) : new DataGridLength(pixels)
        };
    }

    private void Load(bool force)
    {
        if (_dirty && !force)
        {
            return;
        }
        try
        {
            _service.Load(
                _conversion.Requests.Select(request => request.EmployeeName).Distinct(StringComparer.OrdinalIgnoreCase),
                _conversion.Requests.Select(request => request.Policy).Distinct(StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            _status.Text = "Impossibile leggere gli abbinamenti: " + exception.Message;
            return;
        }
        _employees.ItemsSource = _service.Employees.ToList();
        _policies.ItemsSource = _service.Policies.ToList();
        _dirty = false;
        _loaded = true;
        ShowMissing(_service.CountMissing(), "Codici salvati su questo PC.");
    }

    private void ShowMissing(int missing, string idle) =>
        _status.Text = missing > 0
            ? $"Mancano {missing:N0} codici richiesti dal foglio (segnati con *): servono per creare l'anteprima."
            : idle;

    private void MarkDirty()
    {
        _dirty = true;
        _status.Text = "Modifiche non salvate: si salvano da sole tra un attimo.";
        _autosave.Stop();
        _autosave.Start();
    }

    private void SaveNow()
    {
        if (!_loaded)
        {
            return;
        }
        CommitEdits();
        try
        {
            var result = _service.Save(allowIncomplete: true);
            _dirty = false;
            _autosave.Stop();
            _status.Text = result.Note is not null
                ? "Abbinamenti salvati in parte. " + result.Note
                : result.Warnings ?? (result.Missing > 0
                    ? $"Abbinamenti salvati. Mancano ancora {result.Missing:N0} codici richiesti dal foglio."
                    : "Abbinamenti salvati nel database locale.");
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            MessageBox.Show(_owner(), exception.Message, "Impossibile salvare gli abbinamenti", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveSilently()
    {
        CommitEdits();
        try
        {
            var result = _service.Save(allowIncomplete: true);
            _dirty = false;
            _status.Text = result.Note ?? result.Warnings ?? "Salvato.";
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            _status.Text = "Non salvato: " + exception.Message;
        }
    }

    private void CommitEdits()
    {
        _employees.CommitEdit(DataGridEditingUnit.Row, true);
        _policies.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private void DeleteSelected()
    {
        var grid = CurrentGrid;
        if (grid.SelectedItem is not CodeRow row)
        {
            _status.Text = "Seleziona la riga da eliminare.";
            return;
        }
        var isPolicy = grid == _policies;
        var what = isPolicy ? "il giustificativo" : "il dipendente";
        var extra = isPolicy ? string.Empty : " con le sue ore e il suo periodo";
        var answer = MessageBox.Show(
            _owner(),
            $"Eliminare {what} «{row.Name}»{extra} dagli abbinamenti?{Environment.NewLine}{Environment.NewLine}" +
            "I file XML già esportati non cambiano. Se compare ancora nel file Excel aperto, dovrai inserirne di nuovo il codice.",
            "Elimina abbinamento",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }
        try
        {
            _service.Delete(row, isPolicy);
            grid.ItemsSource = (isPolicy ? _service.Policies : _service.Employees).ToList();
            _status.Text = $"{(isPolicy ? "Giustificativo" : "Dipendente")} «{row.Name}» eliminato dagli abbinamenti.";
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            MessageBox.Show(_owner(), exception.Message, "Impossibile eliminare l'abbinamento", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AddTab(StackPanel bar, Grid content, string title, FrameworkElement page)
    {
        var text = Ui.Text(title, 13.5, FontWeights.SemiBold, "Brush.Muted");
        text.VerticalAlignment = VerticalAlignment.Center;
        var pill = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(0, 0, 8, 0),
            Background = System.Windows.Media.Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = text
        };
        var index = _tabs.Count;
        pill.MouseLeftButtonUp += (_, _) => SelectTab(index);
        System.Windows.Automation.AutomationProperties.SetName(pill, title);
        bar.Children.Add(pill);
        content.Children.Add(page);
        _tabs.Add((pill, text, page));
    }

    private void SelectTab(int index)
    {
        for (var i = 0; i < _tabs.Count; i++)
        {
            var (pill, text, page) = _tabs[i];
            var active = i == index;
            page.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            pill.Background = active ? Ui.Brush("Brush.BrandSoft") : System.Windows.Media.Brushes.Transparent;
            text.Foreground = Ui.Brush(active ? "Brush.BrandInk" : "Brush.Muted");
        }
    }

    internal void ShowTab(int index) => SelectTab(index);
}
