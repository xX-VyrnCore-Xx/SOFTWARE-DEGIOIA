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

/// <summary>Una riga della tabella dei movimenti.</summary>
internal sealed record MovementRow(string Employee, string Code, string Date, string Policy, string Reason, int Hours, int Minutes, string SourceRow);

/// <summary>Una riga della tabella dei controlli, con il colore della sua gravità.</summary>
internal sealed record FindingRow(string Label, string Message, Brush Tone);

/// <summary>Una riga del riepilogo mensile: ore del dipendente in un mese, contro quelle teoriche.</summary>
internal sealed record SummaryRow(string Employee, string Month, string Presence, string Absence, string Total, string Expected, string Difference, string Uncovered);

/// <summary>
/// La schermata di conversione della nuova interfaccia: a sinistra il foglio, il codice azienda e i riepiloghi, a destra
/// le schede Movimenti, XML e Controlli. Tutta la logica sta in <see cref="ConversionService"/> (la stessa della finestra
/// classica); qui c'è solo ciò che si vede.
/// </summary>
internal sealed class ConversionView : Grid
{
    private const int MaximumXmlCharacters = 400_000;

    private readonly ConversionService _service;
    private readonly Func<Window?> _owner;
    private readonly Func<IConversionPrompts> _prompts;
    private readonly DropZoneView _drop = new();
    private readonly StepperBar _stepper = new() { Margin = new Thickness(0, 18, 0, 0) };
    private readonly TextBox _company = new();
    private readonly TextBlock _status;
    private readonly System.Windows.Controls.ProgressBar _busy = new();
    private readonly Button _chooseButton = new() { Content = "Scegli il foglio" };
    private readonly Button _generateButton = new() { Content = "Genera anteprima" };
    private readonly Button _exportButton = new() { Content = "Scarica XML" };
    private readonly TextBlock _movementsValue = Ui.Text("—", 22, FontWeights.SemiBold);
    private readonly TextBlock _employeesValue = Ui.Text("—", 22, FontWeights.SemiBold);
    private readonly TextBlock _hoursValue = Ui.Text("—", 22, FontWeights.SemiBold);
    private readonly DataGrid _movementsGrid = new();
    private readonly DataGrid _findingsGrid = new();
    private readonly DataGrid _summaryGrid = new();
    private readonly TextBlock _summaryNote;
    private readonly Button _pdfButton = new() { Content = "Salva PDF" };
    private readonly Button _csvButton = new() { Content = "Salva CSV" };
    private readonly TextBox _xmlBox = new();
    private readonly TextBlock _findingsSummary;
    private readonly List<(Border Pill, TextBlock Text, FrameworkElement Page)> _tabs = new();
    private int _tab;
    private bool _working;
    private readonly WpfNotifier? _notifier;

    public ConversionView(ConversionService service, Func<Window?> owner, Func<IConversionPrompts>? prompts = null, WpfNotifier? notifier = null)
    {
        _service = service;
        _notifier = notifier;
        _owner = owner;
        _prompts = prompts ?? (() => new WpfPrompts(owner()!, service));
        _status = Ui.Text("Importa un foglio Excel per cominciare.", 13.5, ink: "Brush.Muted", wrap: true);
        _findingsSummary = Ui.Text("Nessuna anteprima ancora.", 13.5, ink: "Brush.Muted", wrap: true);
        _summaryNote = Ui.Text("Genera l'anteprima per vedere le ore del mese di ogni dipendente.", 13.5, ink: "Brush.Muted", wrap: true);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var scaffold = new PageScaffold(
            "Nuova conversione",
            "Importa le presenze, genera l'anteprima, controlla i movimenti e scarica l'XML per Zucchetti.",
            BuildBody());
        _generateButton.Style = Ui.Style("Primary");
        _exportButton.Style = Ui.Style("Primary");
        scaffold.AddAction(_chooseButton);
        scaffold.AddAction(_generateButton);
        scaffold.AddAction(_exportButton);
        Children.Add(scaffold.At(0, 0, rowSpan: 2));

        _company.Text = service.CompanyCode;
        _company.Tag = "Codice azienda (fino a 6 caratteri)";
        _company.MaxLength = 6;
        _company.TextChanged += (_, _) =>
        {
            service.CompanyCode = _company.Text;
            UpdateButtons();
        };
        service.CompanyCodeChanged += (_, _) =>
        {
            if (_company.Text != service.CompanyCode)
            {
                _company.Text = service.CompanyCode;
            }
        };
        _chooseButton.Click += async (_, _) => await ImportAsync(null);
        _drop.Clicked += async (_, _) => await ImportAsync(null);
        _drop.FileDropped += async (_, path) => await ImportAsync(path);
        _generateButton.Click += async (_, _) => await GenerateAsync();
        _exportButton.Click += (_, _) => Export();
        _pdfButton.Click += (_, _) => SaveMonthly("pdf");
        _csvButton.Click += (_, _) => SaveMonthly("csv");
        _drop.ShowEmpty();
        UpdateButtons();
    }

    /// <summary>Importa un foglio (se <paramref name="path"/> è nullo chiede quale).</summary>
    public async Task ImportAsync(string? path)
    {
        if (_working)
        {
            return;
        }
        if (path is null)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Scegli il foglio Excel delle presenze",
                Filter = WorkbookDrop.OpenFileFilter
            };
            var last = _service.Database.GetSetting("last-source-dir");
            if (!string.IsNullOrEmpty(last) && Directory.Exists(last))
            {
                dialog.InitialDirectory = last;
            }
            if (dialog.ShowDialog(_owner()) != true)
            {
                return;
            }
            path = dialog.FileName;
        }
        await Work("Lettura del file Excel in corso…", async () =>
        {
            var loaded = await _service.LoadAsync(path, _prompts());
            if (!loaded)
            {
                return;
            }
            _drop.ShowLoaded(Path.GetFileName(path), $"{_service.Requests.Count:N0} richieste · foglio «{_service.Inspection?.SheetName}»");
            _stepper.Current = 1;
            ShowPreview();
            SetStatus("File importato. Controlla il codice azienda e genera l'anteprima.");
        }, "Importazione Excel non riuscita");
    }

    internal async Task GenerateAsync()
    {
        if (_working || !_service.HasWorkbook)
        {
            return;
        }
        await Work("Generazione dei movimenti e dell'XML in corso…", async () =>
        {
            var outcome = await _service.GenerateAsync(_prompts());
            switch (outcome)
            {
                case PreviewOutcome.Cancelled:
                    SetStatus("Conversione annullata: gli abbinamenti richiesti non sono stati salvati.");
                    return;
                case PreviewOutcome.StillMissingCodes:
                    SetStatus("Abbinamenti salvati, ma mancano ancora dei codici: completali per creare l'anteprima.");
                    return;
            }
            ShowPreview();
            _stepper.Current = 2;
            var (errors, warnings, _) = _service.FindingCounts;
            SelectTab(errors > 0 ? 2 : 0);
            SetStatus(errors > 0
                ? $"{errors:N0} errori e {warnings:N0} attenzioni da verificare nella scheda Controlli."
                : warnings > 0
                    ? $"{warnings:N0} segnalazioni da verificare: le trovi nella scheda Controlli."
                    : "Anteprima pronta. Controlla i movimenti e l'XML prima di scaricare.");
            NotifyConversionCompleted(_service.Movements.Count);
        }, "Generazione XML non riuscita");
    }

    private void Export()
    {
        if (_working || !_service.HasPreview)
        {
            return;
        }
        var previous = _service.FindPreviousDelivery();
        if (previous is not null && !_prompts().ConfirmAlreadyDelivered(previous))
        {
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Salva il file XML TRRIPW",
            Filter = "File XML (*.xml)|*.xml",
            DefaultExt = "xml",
            AddExtension = true,
            FileName = _service.SuggestedFileName,
            OverwritePrompt = true
        };
        var last = _service.Database.GetSetting("last-export-dir");
        if (!string.IsNullOrEmpty(last) && Directory.Exists(last))
        {
            dialog.InitialDirectory = last;
        }
        if (dialog.ShowDialog(_owner()) != true)
        {
            return;
        }
        try
        {
            _service.Export(dialog.FileName);
            _stepper.Current = 3;
            SetStatus($"XML salvato: {dialog.FileName}");
            var saved = dialog.FileName;
            _notifier?.Notify(ToastKind.Success, "File salvato", $"{Path.GetFileName(saved)} · clic per aprirlo.", () => ShellActions.OpenFile(saved));
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception) || exception is AggregateException)
        {
            _prompts().ShowProblem("Salvataggio XML non riuscito", exception.Message);
        }
    }

    private async Task Work(string message, Func<Task> action, string failureTitle)
    {
        _working = true;
        _busy.Visibility = Visibility.Visible;
        SetStatus(message);
        UpdateButtons();
        try
        {
            await action();
        }
        catch (Exception exception) when (ConversionService.IsExpectedInputError(exception) ||
                                          exception is InvalidOperationException or System.Net.Http.HttpRequestException)
        {
            SetStatus(exception.Message);
            _prompts().ShowProblem(failureTitle, exception.Message);
        }
        finally
        {
            _working = false;
            _busy.Visibility = Visibility.Collapsed;
            UpdateButtons();
        }
    }

    private void SetStatus(string text) => _status.Text = text;

    /// <summary>
    /// Conversione finita: con la finestra in uso basta il cartellino; altrimenti l'avviso invita a tornare e chiede anche
    /// il lampeggio nella barra e il fumetto di Windows.
    /// </summary>
    private void NotifyConversionCompleted(int movementCount)
    {
        if (_notifier is null)
        {
            return;
        }
        if (_notifier.IsWindowInUse)
        {
            _notifier.Notify(ToastKind.Success, "Conversione completata", $"{movementCount:N0} movimenti pronti. Controlla l'anteprima ed esporta l'XML.");
            return;
        }
        _notifier.Notify(
            ToastKind.Success,
            "Conversione completata",
            $"{movementCount:N0} movimenti pronti. Apri Vyrn Presence per revisionare ed esportare l'XML.",
            systemNotification: true);
    }

    private void UpdateButtons()
    {
        _chooseButton.IsEnabled = !_working;
        _generateButton.IsEnabled = !_working && _service.HasWorkbook && ConversionService.IsValidCompanyCode(_company.Text);
        _exportButton.IsEnabled = !_working && _service.HasPreview;
        _pdfButton.IsEnabled = !_working && _service.HasPreview;
        _csvButton.IsEnabled = !_working && _service.HasPreview;
    }

    /// <summary>Riempie le tre schede con lo stato corrente del servizio.</summary>
    private void ShowPreview()
    {
        var movements = _service.Movements;
        _movementsGrid.ItemsSource = movements
            .Select(movement => new MovementRow(
                movement.EmployeeName,
                movement.EmployeeCode,
                movement.Date.ToString("dd/MM/yyyy"),
                movement.Policy,
                movement.ReasonCode,
                movement.Hours,
                movement.Minutes,
                movement.SourceRow == 0 ? string.Empty : movement.SourceRow.ToString()))
            .ToList();
        var xml = _service.Xml ?? string.Empty;
        _xmlBox.Text = xml.Length <= MaximumXmlCharacters
            ? xml
            : xml[..MaximumXmlCharacters] + Environment.NewLine + "… (anteprima accorciata: il file salvato è completo)";
        var totals = movements.Count == 0 ? null : MovementChecks.Summarize(movements.ToList());
        _movementsValue.Text = totals is null ? "—" : totals.Movements.ToString("N0");
        _employeesValue.Text = totals is null ? "—" : totals.Employees.ToString("N0");
        _hoursValue.Text = totals is null ? "—" : $"{totals.TotalMinutes / 60:N0} h";
        var rows = _service.Findings
            .Select(finding => new FindingRow(
                finding.Label,
                finding.Message,
                Ui.Brush(finding.Severity switch
                {
                    MovementSeverity.Error => "Brush.DangerInk",
                    MovementSeverity.Warning => "Brush.WarningInk",
                    _ => "Brush.Muted"
                })))
            .ToList();
        _findingsGrid.ItemsSource = rows;
        var (errors, warnings, notes) = _service.FindingCounts;
        _findingsSummary.Text = !_service.HasPreview
            ? "Nessuna anteprima ancora."
            : rows.Count == 0
                ? "Nessun problema rilevato nei movimenti e nel foglio importato."
                : $"{errors:N0} errori · {warnings:N0} attenzioni · {notes:N0} note";
        _tabs[2].Text.Text = rows.Count == 0 ? "Controlli" : $"Controlli ({rows.Count:N0})";
        FillSummary();
        UpdateButtons();
    }

    private UIElement BuildBody()
    {
        _busy.IsIndeterminate = true;
        _busy.Height = 3;
        _busy.BorderThickness = new Thickness(0);
        _busy.Foreground = Ui.Brush("Brush.Brand");
        _busy.Background = Ui.Brush("Brush.Sunken");
        _busy.Visibility = Visibility.Collapsed;
        _busy.Margin = new Thickness(0, 14, 0, 0);

        _drop.MinHeight = 112;
        _company.MaxWidth = 280;
        _company.HorizontalAlignment = HorizontalAlignment.Left;
        _company.MinWidth = 200;
        var info = new StackPanel { Margin = new Thickness(24, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(Ui.Text("Codice azienda", 13, FontWeights.SemiBold).WithMargin(new Thickness(0, 0, 0, 6)));
        info.Children.Add(_company);
        info.Children.Add(_stepper.WithMargin(new Thickness(0, 14, 0, 0)));
        info.Children.Add(_busy);
        info.Children.Add(_status.WithMargin(new Thickness(0, 10, 0, 0)));

        var stats = new UniformGrid { Columns = 3, Margin = new Thickness(0, 18, 0, 0) };
        stats.Children.Add(Stat("Movimenti", _movementsValue));
        stats.Children.Add(Stat("Dipendenti", _employeesValue));
        stats.Children.Add(Stat("Ore totali", _hoursValue));

        var topCard = Ui.Columns(Ui.Px(300), Ui.Star());
        topCard.Children.Add(_drop.At(0, 0));
        topCard.Children.Add(info.At(0, 1));
        var topStack = new StackPanel();
        topStack.Children.Add(topCard);
        topStack.Children.Add(stats);

        ConfigureGrids();
        ConfigureSummaryGrid();
        _xmlBox.IsReadOnly = true;
        _xmlBox.Style = null;
        _xmlBox.BorderThickness = new Thickness(0);
        _xmlBox.Background = System.Windows.Media.Brushes.Transparent;
        _xmlBox.Foreground = Ui.Brush("Brush.Ink");
        _xmlBox.FontFamily = new FontFamily("Cascadia Mono, Consolas");
        _xmlBox.FontSize = 12.5;
        _xmlBox.Padding = new Thickness(16);
        _xmlBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _xmlBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _xmlBox.TextWrapping = TextWrapping.NoWrap;

        var findingsPage = new Grid();
        findingsPage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        findingsPage.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        findingsPage.Children.Add(_findingsSummary.WithMargin(new Thickness(16, 12, 16, 12)).At(0, 0));
        findingsPage.Children.Add(_findingsGrid.At(1, 0));

        var summaryActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 0) };
        _pdfButton.Margin = new Thickness(0, 0, 8, 0);
        summaryActions.Children.Add(_pdfButton);
        summaryActions.Children.Add(_csvButton);
        var summaryHeader = new Grid { Margin = new Thickness(16, 12, 16, 12) };
        summaryHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summaryHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        summaryHeader.Children.Add(_summaryNote.At(0, 0));
        summaryHeader.Children.Add(summaryActions.At(0, 1));
        var summaryPage = new Grid();
        summaryPage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        summaryPage.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        summaryPage.Children.Add(summaryHeader.At(0, 0));
        summaryPage.Children.Add(_summaryGrid.At(1, 0));

        var tabBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        var content = new Grid();
        AddTab(tabBar, content, "Movimenti", _movementsGrid);
        AddTab(tabBar, content, "XML", _xmlBox);
        AddTab(tabBar, content, "Controlli", findingsPage);
        AddTab(tabBar, content, "Riepilogo", summaryPage);

        var contentCard = new Border
        {
            Background = Ui.Brush("Brush.Surface"),
            BorderBrush = Ui.Brush("Brush.Divider"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Child = content
        };
        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.Children.Add(tabBar.At(0, 0));
        right.Children.Add(contentCard.At(1, 0));

        var body = new Grid { Margin = new Thickness(0, 0, 0, 20) };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(Ui.Card(topStack, new Thickness(22, 20, 22, 20)).WithMargin(new Thickness(0, 0, 0, 18)).At(0, 0));
        body.Children.Add(right.At(1, 0));
        SelectTab(0);
        return body;
    }

    private static FrameworkElement Stat(string label, TextBlock value)
    {
        var stack = new StackPanel();
        stack.Children.Add(Ui.Text(label, 12.5, ink: "Brush.Muted"));
        value.Margin = new Thickness(0, 2, 0, 0);
        stack.Children.Add(value);
        return stack;
    }

    private void ConfigureGrids()
    {
        _movementsGrid.Columns.Add(Column("Dipendente", nameof(MovementRow.Employee), star: 2));
        _movementsGrid.Columns.Add(Column("Codice", nameof(MovementRow.Code), pixels: 84));
        _movementsGrid.Columns.Add(Column("Data", nameof(MovementRow.Date), pixels: 104));
        _movementsGrid.Columns.Add(Column("Giustificativo", nameof(MovementRow.Policy), star: 1.5));
        _movementsGrid.Columns.Add(Column("Causale", nameof(MovementRow.Reason), pixels: 84));
        _movementsGrid.Columns.Add(Column("Ore", nameof(MovementRow.Hours), pixels: 60));
        _movementsGrid.Columns.Add(Column("Min", nameof(MovementRow.Minutes), pixels: 60));
        _movementsGrid.Columns.Add(Column("Riga", nameof(MovementRow.SourceRow), pixels: 64));

        var tone = new Style(typeof(TextBlock));
        tone.Setters.Add(new Setter(TextBlock.ForegroundProperty, new Binding(nameof(FindingRow.Tone))));
        tone.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        _findingsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Gravità",
            Binding = new Binding(nameof(FindingRow.Label)),
            Width = new DataGridLength(110),
            ElementStyle = tone
        });
        var wrap = new Style(typeof(TextBlock));
        wrap.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        wrap.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 8, 0, 8)));
        _findingsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Segnalazione",
            Binding = new Binding(nameof(FindingRow.Message)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            ElementStyle = wrap
        });
    }

    private void ConfigureSummaryGrid()
    {
        var right = new Style(typeof(TextBlock));
        right.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        _summaryGrid.Columns.Add(Column("Dipendente", nameof(SummaryRow.Employee), star: 2));
        _summaryGrid.Columns.Add(Column("Mese", nameof(SummaryRow.Month), pixels: 130));
        foreach (var (header, property) in new[]
                 {
                     ("Presenza", nameof(SummaryRow.Presence)),
                     ("Assenza", nameof(SummaryRow.Absence)),
                     ("Totale", nameof(SummaryRow.Total)),
                     ("Teoriche", nameof(SummaryRow.Expected)),
                     ("Differenza", nameof(SummaryRow.Difference)),
                     ("Giorni scoperti", nameof(SummaryRow.Uncovered))
                 })
        {
            var column = Column(header, property, pixels: header == "Giorni scoperti" ? 130 : 96);
            column.ElementStyle = right;
            _summaryGrid.Columns.Add(column);
        }
    }

    /// <summary>Riempie la scheda Riepilogo con le ore del mese di ogni dipendente.</summary>
    private void FillSummary()
    {
        var rows = _service.MonthlySummary();
        _summaryGrid.ItemsSource = rows
            .Select(row => new SummaryRow(
                row.EmployeeName,
                MonthName(row.Month),
                HoursText(row.PresenceMinutes),
                HoursText(row.AbsenceMinutes),
                HoursText(row.TotalMinutes),
                row.ExpectedMinutes is { } expected ? HoursText(expected) : "—",
                row.ExpectedMinutes is { } theoretical ? SignedHoursText(row.TotalMinutes - theoretical) : "—",
                row.UncoveredDays?.ToString("N0") ?? "—"))
            .ToList();
        _summaryNote.Text = rows.Count == 0
            ? "Genera l'anteprima per vedere le ore del mese di ogni dipendente."
            : rows.Any(row => row.ExpectedMinutes is not null)
                ? $"{rows.Count:N0} righe: ore registrate contro quelle teoriche del mese. Salva il report in PDF o il riepilogo in CSV per Excel."
                : $"{rows.Count:N0} righe. Con le presenze automatiche accese compaiono anche le ore teoriche e i giorni scoperti.";
    }

    internal static string HoursText(int minutes) => (minutes / 60m).ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("it-IT")) + " h";

    internal static string SignedHoursText(int minutes) =>
        minutes == 0 ? "—" : (minutes > 0 ? "+" : "−") + HoursText(Math.Abs(minutes));

    private static string MonthName(DateTime month)
    {
        var text = month.ToString("MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
        return char.ToUpper(text[0], System.Globalization.CultureInfo.GetCultureInfo("it-IT")) + text[1..];
    }

    /// <summary>Salva il report del mese («pdf») o il riepilogo («csv»), chiedendo dove.</summary>
    private void SaveMonthly(string extension)
    {
        if (_working || !_service.HasPreview)
        {
            return;
        }
        var isPdf = extension == "pdf";
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = isPdf ? "Salva il report delle ore" : "Salva il riepilogo delle ore",
            Filter = isPdf ? "File PDF (*.pdf)|*.pdf" : "File CSV per Excel (*.csv)|*.csv",
            DefaultExt = extension,
            AddExtension = true,
            FileName = _service.SuggestedReportFileName(extension),
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(_owner()) != true)
        {
            return;
        }
        try
        {
            if (isPdf)
            {
                File.WriteAllBytes(dialog.FileName, _service.BuildMonthlyReportPdf(DateTime.Now));
            }
            else
            {
                File.WriteAllText(dialog.FileName, _service.BuildMonthlySummaryCsv(), new System.Text.UTF8Encoding(true));
            }
            var saved = dialog.FileName;
            SetStatus($"Salvato: {saved}");
            _notifier?.Notify(ToastKind.Success, "File salvato", $"{Path.GetFileName(saved)} · clic per aprirlo.", () => ShellActions.OpenFile(saved));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _prompts().ShowProblem("Salvataggio non riuscito", exception.Message);
        }
    }

    private static DataGridTextColumn Column(string header, string property, double star = 0, double pixels = 0) => new()
    {
        Header = header,
        Binding = new Binding(property),
        Width = star > 0
            ? new DataGridLength(star, DataGridLengthUnitType.Star)
            : new DataGridLength(pixels)
    };

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
        _tab = index;
        for (var i = 0; i < _tabs.Count; i++)
        {
            var (pill, text, page) = _tabs[i];
            var active = i == index;
            page.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            pill.Background = active ? Ui.Brush("Brush.BrandSoft") : System.Windows.Media.Brushes.Transparent;
            text.Foreground = Ui.Brush(active ? "Brush.BrandInk" : "Brush.Muted");
        }
        if (_tabs[index].Page is { } shown)
        {
            UiMotion.FadeIn(shown, rise: 6, duration: UiMotion.Quick);
        }
    }

    /// <summary>La scheda mostrata (per i test).</summary>
    internal int SelectedTab => _tab;

    internal void ShowTab(int index) => SelectTab(index);
}
