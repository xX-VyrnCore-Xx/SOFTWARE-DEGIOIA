namespace PresenzeXmlConverter.Tests;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using PresenzeXmlConverter.Wpf;

public class DialogLogicTests
{
    [Fact]
    public void ColumnsMustBeFiveDifferentOnes()
    {
        Assert.True(DialogLogic.TryBuildColumns([0, 1, 2, 3, 4], out var selection, out var error));
        Assert.Null(error);
        Assert.Equal(new ColumnSelection(0, 1, 2, 3, 4), selection);

        Assert.False(DialogLogic.TryBuildColumns([0, 1, 2, 3, -1], out _, out error));
        Assert.Contains("colonna diversa", error);
        Assert.False(DialogLogic.TryBuildColumns([0, 1, 2, 3, 3], out _, out _));
        Assert.False(DialogLogic.TryBuildColumns([0, 1, 2], out _, out _));
    }

    [Fact]
    public void ColumnsAreDescribedWithTheirNumber()
    {
        Assert.Equal("Colonna 3: Dal", DialogLogic.DescribeColumn(2, "Dal"));
        Assert.Equal("Colonna 1: (senza intestazione)", DialogLogic.DescribeColumn(0, " "));
        Assert.Equal("Seleziona una colonna", DialogLogic.DescribeColumn(-1, "Seleziona una colonna"));
    }

    [Fact]
    public void CompanyRulesAreCheckedLikeTheSettings()
    {
        // Senza regole proprie valgono quelle generali, qualunque cosa ci sia scritta nei campi.
        Assert.True(DialogLogic.TryBuildCompanyRules(false, "abc", false, true, "31/02", 2026, out var none, out var noError));
        Assert.Null(none);
        Assert.Null(noError);

        Assert.True(DialogLogic.TryBuildCompanyRules(true, "7,5", true, false, "02/06" + Environment.NewLine + "24/12", 2026, out var rules, out var error));
        Assert.Null(error);
        Assert.Equal(7.5m, rules!.HoursPerDay);
        Assert.True(rules.IncludeSaturdays);
        Assert.False(rules.AutoDetect);
        Assert.Contains("02/06", rules.Holidays);

        Assert.False(DialogLogic.TryBuildCompanyRules(true, "abc", false, true, "", 2026, out _, out error));
        Assert.Contains("ore", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(DialogLogic.TryBuildCompanyRules(true, "8", false, true, "31/02", 2026, out _, out error));
        Assert.Contains("Date non riconosciute", error);

        var (hours, holidays) = DialogLogic.FormatRules(new CompanyRules(6m, false, true, "02/06, 24/12"));
        Assert.Equal(6m.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture), hours);
        Assert.Equal("02/06" + Environment.NewLine + "24/12", holidays);
    }
}

[Collection("Wpf")]
public class WpfDialogTests
{
    private static void Shot(Window window, string name)
    {
        var bitmap = WpfShellTests.Render(window);
        Assert.True(WpfShellTests.CountMagenta(bitmap) == 0, $"{name}: colori mancanti (magenta).");
        Assert.True(WpfShellTests.DistinctColors(bitmap) > 6, $"{name}: finestra vuota.");
        var directory = Environment.GetEnvironmentVariable("PRESENZEFLOW_SCREENSHOT_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(directory, $"wpf-{name}.png"));
            encoder.Save(stream);
        }
    }

    private static void ShowOffScreen(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -4000;
        window.Top = -4000;
        window.Show();
        WpfTestHost.Pump(450);
    }

    [Fact]
    public void TheColumnDialogRefusesIncompleteChoicesAndReturnsTheSelection()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var window = ColumnMappingWindow.Create(
                ["Nome", "Tipo", "Dal", "Al", "Ore", "Note"], null, out var boxes, out var result);
            ShowOffScreen(window);
            Assert.Equal(5, boxes.Count);
            Assert.All(boxes, box => Assert.Equal(0, box.SelectedIndex));

            window.Confirm();
            Assert.False(window.Accepted);
            Assert.Contains("colonna diversa", window.ErrorText);
            Shot(window, "Dialogo-Colonne");

            for (var field = 0; field < boxes.Count; field++)
            {
                boxes[field].SelectedIndex = field + 1;
            }
            // Due campi sulla stessa colonna: ancora rifiutato.
            boxes[4].SelectedIndex = 1;
            window.Confirm();
            Assert.False(window.Accepted);
            boxes[4].SelectedIndex = 5;
            window.Confirm();
            Assert.True(window.Accepted);
            Assert.Equal(new ColumnSelection(0, 1, 2, 3, 4), result());
        });
    }

    [Fact]
    public void TheColumnDialogStartsFromTheSuggestedColumns()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var window = ColumnMappingWindow.Create(
                ["Nome", "Tipo", "Dal", "Al", "Ore"], new ColumnSelection(4, 3, 2, 1, 0), out var boxes, out var result);
            ShowOffScreen(window);
            Assert.Equal(new[] { 5, 4, 3, 2, 1 }, boxes.Select(box => box.SelectedIndex).ToArray());
            window.Confirm();
            Assert.True(window.Accepted);
            Assert.Equal(new ColumnSelection(4, 3, 2, 1, 0), result());
        });
    }

    [Fact]
    public void TheCompanyRulesDialogShowsTheGeneralRulesUntilOwnRulesAreChosen()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var company = new CompanyRecord("AZ001", "Rossi Srl", string.Empty);
            var general = new GenerationOptions(8m, false, [new DateTime(2026, 6, 2)], true);
            var dialog = new CompanyRulesWindow(company, general, 2026);
            ShowOffScreen(dialog.Window);
            var controls = dialog.Controls;
            Assert.False(controls.Own);
            Assert.False(controls.Hours.IsEnabled);
            Assert.Equal(8m.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture), controls.Hours.Text);
            Shot(dialog.Window, "Dialogo-RegoleAzienda");

            // Confermando senza regole proprie: valgono quelle generali (regole nulle).
            dialog.Window.Confirm();
            Assert.True(dialog.Window.Accepted);
            Assert.Null(dialog.Rules);
        });
    }

    [Fact]
    public void TheCompanyRulesDialogChecksOwnRulesBeforeSaving()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var company = new CompanyRecord("AZ002", "Bianchi", string.Empty, new CompanyRules(6m, true, false, "02/06"));
            var dialog = new CompanyRulesWindow(company, GenerationOptions.Default, 2026);
            ShowOffScreen(dialog.Window);
            var controls = dialog.Controls;
            Assert.True(controls.Own);
            Assert.True(controls.Hours.IsEnabled);
            Assert.Equal(6m.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture), controls.Hours.Text);

            controls.Hours.Text = "abc";
            dialog.Window.Confirm();
            Assert.False(dialog.Window.Accepted);
            Assert.Contains("ore", dialog.Window.ErrorText, StringComparison.OrdinalIgnoreCase);

            controls.Hours.Text = "7,5";
            controls.Holidays.Text = "02/06" + Environment.NewLine + "24/12";
            dialog.Window.Confirm();
            Assert.True(dialog.Window.Accepted);
            Assert.Equal(7.5m, dialog.Rules!.HoursPerDay);
            Assert.True(dialog.Rules.IncludeSaturdays);
            Assert.Contains("24/12", dialog.Rules.Holidays);
        });
    }

    [Fact]
    public void TheCompanyChecksDialogShowsTheDefaultsAndChecksTheFieldsBeforeSaving()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var company = new CompanyRecord("AZ001", "Rossi Srl", string.Empty);
            var dialog = new CompanyChecksWindow(company);
            ShowOffScreen(dialog.Window);
            var controls = dialog.Controls;
            Assert.Equal("30", controls.Illness.Text);
            Assert.Equal("30", controls.Percent.Text);
            Assert.Equal(string.Empty, controls.Absence.Text);
            Assert.True(controls.Overlaps.IsChecked);
            Shot(dialog.Window, "Dialogo-ControlliAzienda");

            controls.Illness.Text = "0";
            dialog.Window.Confirm();
            Assert.False(dialog.Window.Accepted);
            Assert.Contains("malattia", dialog.Window.ErrorText, StringComparison.OrdinalIgnoreCase);

            controls.Illness.Text = "45";
            controls.Absence.Text = "15";
            controls.MaxHours.Text = "180";
            controls.Overlaps.IsChecked = false;
            controls.Expected.Text = "Rossi Mario";
            dialog.Window.Confirm();
            Assert.True(dialog.Window.Accepted);
            Assert.Equal(new ValidationRules(45, 15, 30, false, 180m, "Rossi Mario"), dialog.Rules);
        });
    }

    [Fact]
    public void TheMissingCodesDialogShowsTheSheetNamesAndKeepsWhatIsTyped()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var folder = Directory.CreateTempSubdirectory().FullName;
            var database = new LocalDatabase(Path.Combine(folder, "app.db"));
            var service = new ConversionService(database) { CompanyCode = "AZ001" };
            var loading = service.LoadAsync(SampleWorkbook.Path, new FakePromptsForDialogs());
            WpfTestHost.Await(loading);
            Assert.True(loading.Result);

            var window = CodesDialog.Create(service, () => null, out var view);
            ShowOffScreen(window);
            Shot(window, "Dialogo-Codici");

            // Confermare con i codici ancora mancanti non blocca: si salva quel che c'è e la conversione decide.
            window.Confirm();
            Assert.True(window.Accepted);
        });
    }

    private sealed class FakePromptsForDialogs : IConversionPrompts
    {
        public ColumnSelection? AskColumns(IReadOnlyList<string> headers) => throw new InvalidOperationException("Nessuna domanda attesa.");

        public bool ConfirmCodeReplace(string employee, string fileCode, string savedCode) => true;

        public bool CompleteMappings(IReadOnlyList<string> missingEmployees, IReadOnlyList<string> missingPolicies, LocalDatabase database) => false;

        public bool ConfirmAlreadyDelivered(ExportRecord previous) => true;

        public void ShowProblem(string title, string message) => throw new InvalidOperationException(message);
    }
}
