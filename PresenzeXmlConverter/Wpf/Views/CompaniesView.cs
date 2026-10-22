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

/// <summary>Una riga dell'elenco aziende.</summary>
internal sealed record CompanyRow(CompanyRecord Record, string InUse, string Code, string Name, string Rules, string Notes);

/// <summary>
/// La sezione «Aziende» della nuova interfaccia: a sinistra l'elenco (con quella in uso per la conversione), a destra
/// il modulo per creare o modificare un'azienda, impostare le sue regole proprie, sceglierla per la conversione o eliminarla.
/// </summary>
internal sealed class CompaniesView : Grid, IRefreshablePage
{
    private readonly ConversionService _conversion;
    private readonly CompaniesService _service;
    private readonly Func<Window?> _owner;
    private readonly DataGrid _grid = new();
    private readonly TextBox _code = new();
    private readonly TextBox _name = new();
    private readonly TextBox _notes = new();
    private readonly TextBlock _summary;
    private readonly TextBlock _message;
    private readonly Button _save = new() { Content = "Salva azienda" };
    private readonly Button _rules = new() { Content = "Regole…" };
    private readonly Button _use = new() { Content = "Usa per la conversione" };
    private readonly Button _delete = new() { Content = "Elimina" };
    private bool _loading;

    public CompaniesView(ConversionService conversion, Func<Window?> owner)
    {
        _conversion = conversion;
        _service = new CompaniesService(conversion.Database);
        _owner = owner;
        _summary = Ui.Text("Nessuna azienda salvata.", 13.5, ink: "Brush.Muted");
        _message = Ui.Text(string.Empty, 13, ink: "Brush.Muted", wrap: true);

        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var newButton = new Button { Content = "Nuova azienda", Style = Ui.Style("Primary") };
        newButton.Click += (_, _) => ClearForm();
        var page = new PageScaffold("Aziende", "Il codice ufficiale PAGHE, la ragione sociale e le regole di ogni azienda.", BuildBody());
        page.AddAction(newButton);
        Children.Add(page);

        _save.Style = Ui.Style("Primary");
        _delete.Style = Ui.Style("Danger");
        _code.Tag = "Codice PAGHE (1-6 caratteri)";
        _code.MaxLength = 6;
        _name.Tag = "Ragione sociale";
        _name.MaxLength = 120;
        _notes.Tag = "Note";
        _notes.MaxLength = 500;
        _notes.AcceptsReturn = true;
        _notes.TextWrapping = TextWrapping.Wrap;
        _notes.MinHeight = 76;
        _notes.VerticalContentAlignment = VerticalAlignment.Top;

        _grid.SelectionChanged += (_, _) => OnSelected();
        _save.Click += (_, _) => SaveFromForm();
        _rules.Click += (_, _) => EditRules();
        _use.Click += (_, _) => UseSelected();
        _delete.Click += (_, _) => DeleteSelected();
        _conversion.CompanyCodeChanged += (_, _) => { if (IsLoaded) { Refresh(); } };
        Refresh();
    }

    public void OnShown() => Refresh();

    private CompanyRecord? Selected => (_grid.SelectedItem as CompanyRow)?.Record;

    private UIElement BuildBody()
    {
        _grid.Columns.Add(new DataGridTextColumn { Header = "", Binding = new Binding(nameof(CompanyRow.InUse)), Width = new DataGridLength(44) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Codice", Binding = new Binding(nameof(CompanyRow.Code)), Width = new DataGridLength(96) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Ragione sociale", Binding = new Binding(nameof(CompanyRow.Name)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Regole", Binding = new Binding(nameof(CompanyRow.Rules)), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });

        var listCard = new Grid();
        listCard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        listCard.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        listCard.Children.Add(_summary.WithMargin(new Thickness(18, 14, 18, 12)).At(0, 0));
        listCard.Children.Add(_grid.At(1, 0));
        var list = new Border
        {
            Background = Ui.Brush("Brush.Surface"),
            BorderBrush = Ui.Brush("Brush.Divider"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Child = listCard
        };

        var form = new StackPanel();
        form.Children.Add(Ui.Text("Dati dell'azienda", 16, FontWeights.SemiBold).WithMargin(new Thickness(0, 0, 0, 14)));
        form.Children.Add(Ui.Text("Codice", 13, FontWeights.SemiBold).WithMargin(new Thickness(0, 0, 0, 6)));
        form.Children.Add(_code);
        form.Children.Add(Ui.Text("Ragione sociale", 13, FontWeights.SemiBold).WithMargin(new Thickness(0, 14, 0, 6)));
        form.Children.Add(_name);
        form.Children.Add(Ui.Text("Note", 13, FontWeights.SemiBold).WithMargin(new Thickness(0, 14, 0, 6)));
        form.Children.Add(_notes);
        var buttons = new WrapPanel { Margin = new Thickness(0, 18, 0, 0) };
        foreach (var button in new[] { _save, _rules, _use, _delete })
        {
            button.Margin = new Thickness(0, 0, 8, 8);
            buttons.Children.Add(button);
        }
        form.Children.Add(buttons);
        form.Children.Add(_message.WithMargin(new Thickness(0, 6, 0, 0)));
        var formScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = form };

        var body = Ui.Columns(Ui.Star(1.5), Ui.Px(20), Ui.Px(330));
        body.Margin = new Thickness(0, 0, 0, 20);
        body.Children.Add(list.At(0, 0));
        body.Children.Add(Ui.Card(formScroll, new Thickness(22)).At(0, 2));
        return body;
    }

    private void Refresh()
    {
        var selectedCode = Selected?.Code;
        IReadOnlyList<CompanyRecord> companies;
        try
        {
            companies = _service.Load();
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            _message.Text = "Impossibile leggere le aziende: " + exception.Message;
            return;
        }
        var inUse = _conversion.CompanyCode.Trim();
        _loading = true;
        _grid.ItemsSource = companies
            .Select(company => new CompanyRow(
                company,
                string.Equals(company.Code, inUse, StringComparison.OrdinalIgnoreCase) ? "●" : string.Empty,
                company.Code,
                company.Name,
                CompaniesService.DescribeRules(company),
                company.Notes))
            .ToList();
        if (selectedCode is not null)
        {
            _grid.SelectedItem = ((IEnumerable<CompanyRow>)_grid.ItemsSource)
                .FirstOrDefault(row => string.Equals(row.Code, selectedCode, StringComparison.OrdinalIgnoreCase));
        }
        _loading = false;
        _summary.Text = companies.Count == 0
            ? "Nessuna azienda salvata."
            : $"{companies.Count:N0} aziende salvate" + (inUse.Length == 0 ? string.Empty : $" · in uso: {inUse}");
        UpdateButtons();
    }

    private void OnSelected()
    {
        if (_loading)
        {
            return;
        }
        if (Selected is { } company)
        {
            _code.Text = company.Code;
            _name.Text = company.Name;
            _notes.Text = company.Notes;
        }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var has = Selected is not null;
        _rules.IsEnabled = has;
        _use.IsEnabled = has;
        _delete.IsEnabled = has;
    }

    private void ClearForm()
    {
        _grid.SelectedItem = null;
        _code.Text = string.Empty;
        _name.Text = string.Empty;
        _notes.Text = string.Empty;
        _message.Text = string.Empty;
        _code.Focus();
        UpdateButtons();
    }

    private void SaveFromForm()
    {
        try
        {
            var code = _code.Text.Trim();
            _service.Save(code, _name.Text, _notes.Text);
            Refresh();
            _grid.SelectedItem = ((IEnumerable<CompanyRow>)_grid.ItemsSource)
                .FirstOrDefault(row => string.Equals(row.Code, code, StringComparison.OrdinalIgnoreCase));
            _message.Text = $"Azienda {code} salvata.";
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            MessageBox.Show(_owner(), exception.Message, "Impossibile salvare l'azienda", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void EditRules()
    {
        if (Selected is not { } company || _owner() is not { } owner)
        {
            return;
        }
        try
        {
            using var dialog = new CompanyRulesDialog(company, _conversion.GlobalOptions, DateTime.Today.Year);
            if (dialog.ShowDialog(WpfInterop.Owner(owner)) != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            _service.SaveRules(company, dialog.Rules);
            Refresh();
            _message.Text = dialog.Rules is null
                ? $"L'azienda {company.Code} usa le regole generali."
                : $"Regole dell'azienda {company.Code} salvate.";
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            MessageBox.Show(owner, exception.Message, "Impossibile salvare le regole", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UseSelected()
    {
        if (Selected is not { } company)
        {
            return;
        }
        try
        {
            _service.UseForConversion(company);
            _conversion.CompanyCode = company.Code;
            Refresh();
            _message.Text = $"Azienda {company.Label} in uso per la conversione.";
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            MessageBox.Show(_owner(), exception.Message, "Impossibile scegliere l'azienda", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DeleteSelected()
    {
        if (Selected is not { } company)
        {
            return;
        }
        var answer = MessageBox.Show(
            _owner(),
            $"Eliminare l'azienda {company.Label} dall'elenco?{Environment.NewLine}{Environment.NewLine}I file XML già esportati non cambiano.",
            "Elimina azienda",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }
        try
        {
            _service.Delete(company);
            ClearForm();
            Refresh();
            _message.Text = $"Azienda {company.Code} eliminata dall'elenco.";
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception))
        {
            MessageBox.Show(_owner(), exception.Message, "Impossibile eliminare l'azienda", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Per i test: seleziona un'azienda come farebbe un clic sulla riga.</summary>
    internal void SelectCode(string code) =>
        _grid.SelectedItem = (_grid.ItemsSource as IEnumerable<CompanyRow>)?.FirstOrDefault(row => row.Code == code);
}
