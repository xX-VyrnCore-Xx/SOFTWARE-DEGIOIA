using System.Windows;
using System.Windows.Media.Imaging;
using PresenzeXmlConverter.Wpf;

namespace PresenzeXmlConverter.Tests;

public class CalendarModelTests
{
    private static DailyMovement Presence(string name, string code, int day, int hours, int month = 3) =>
        new(name, code, AttendanceMovementGenerator.PresencePolicy, "01", new DateTime(2026, month, day), hours, 0);

    private static DailyMovement Absence(string name, string code, int day, int hours) =>
        new(name, code, "Ferie", "FE", new DateTime(2026, 3, day), hours, 0);

    internal static IReadOnlyList<DailyMovement> Sample() =>
    [
        Presence("Rossi Mario", "E001", 2, 8),
        Absence("Rossi Mario", "E001", 3, 8),
        Absence("Rossi Mario", "E001", 4, 4),
        Presence("Rossi Mario", "E001", 4, 4),
        Presence("Rossi Mario", "E001", 5, 10),
        Presence("Bianchi Anna", "E002", 2, 8),
        Presence("Bianchi Anna", "E002", 7, 8, month: 4)
    ];

    internal static GenerationOptions WithPresence => GenerationOptions.Default with { FillPresence = true };

    [Fact]
    public void EachDayOfAnEmployeeGetsItsKind()
    {
        var model = CalendarModel.Create(Sample(), WithPresence, null);

        Assert.Equal(new[] { (2026, 3), (2026, 4) }, model.Months);
        var rows = model.RowsFor(2026, 3);
        Assert.Equal(2, rows.Count);
        var rossi = rows.Single(row => row.EmployeeCode == "E001");
        Assert.Equal(31, rossi.Days.Count);
        Assert.Equal(CalendarCellKind.NonWorking, rossi.Days[0].Kind);
        Assert.Equal(CalendarCellKind.Presence, rossi.Days[1].Kind);
        Assert.Equal(CalendarCellKind.Absence, rossi.Days[2].Kind);
        Assert.Equal("FE", rossi.Days[2].AbsenceCode);
        Assert.Equal(CalendarCellKind.Mixed, rossi.Days[3].Kind);
        Assert.Equal(CalendarCellKind.Presence, rossi.Days[4].Kind);
        Assert.Equal(CalendarCellKind.Gap, rossi.Days[5].Kind);
        Assert.Equal(CalendarCellKind.NonWorking, rossi.Days[6].Kind);
        Assert.Equal(34 * 60, rossi.TotalMinutes);
        Assert.Equal(18, rossi.Gaps);
        Assert.True(rossi.NeedsChecking);
        Assert.NotEqual(0, rossi.DifferenceMinutes);
    }

    [Fact]
    public void WithoutAutomaticPresenceThereAreNoGaps()
    {
        var model = CalendarModel.Create(Sample(), GenerationOptions.Default, null);

        var rossi = model.RowsFor(2026, 3).Single(row => row.EmployeeCode == "E001");
        Assert.Equal(CalendarCellKind.Empty, rossi.Days[5].Kind);
        Assert.Equal(0, rossi.Gaps);
        Assert.Equal(0, rossi.DifferenceMinutes);
        Assert.False(model.FillsPresence);
    }

    [Fact]
    public void TheDayOverTheStandardOneIsAnAlertAndCanBeReachedOneByOne()
    {
        var model = CalendarModel.Create(Sample(), WithPresence, null);

        var alert = Assert.Single(model.AlertDays);
        Assert.Equal("Rossi Mario", alert.EmployeeName);
        Assert.Equal(new DateTime(2026, 3, 5), alert.Date);
        Assert.Equal(AlertSeverity.Check, alert.Severity);
        Assert.Equal(1, model.CheckDayCount);
        Assert.Equal(0, model.NoticeDayCount);
        Assert.Equal(AlertSeverity.Check, model.RowsFor(2026, 3).Single(row => row.EmployeeCode == "E001").Days[4].Alert);
        Assert.True(model.DifferenceCount > 0);
    }

    [Fact]
    public void SearchAndTheNeedsCheckingFilterNarrowTheRows()
    {
        var model = CalendarModel.Create(Sample(), WithPresence, null);

        Assert.Equal("Bianchi Anna", Assert.Single(model.RowsFor(2026, 3, "bianchi")).EmployeeName);
        Assert.Empty(model.RowsFor(2026, 3, "nessuno"));
        Assert.Equal(2, model.RowsFor(2026, 3, null, needsCheckingOnly: true).Count);
        var plain = CalendarModel.Create(Sample(), GenerationOptions.Default, null);
        Assert.Empty(plain.RowsFor(2026, 4, null, needsCheckingOnly: true));
    }

    [Fact]
    public void TheFooterCountsPresentAndAbsentPerDay()
    {
        var rows = CalendarModel.Create(Sample(), WithPresence, null).RowsFor(2026, 3);

        Assert.Equal((2, 0), CalendarModel.CountsOn(rows, 1));
        Assert.Equal((0, 1), CalendarModel.CountsOn(rows, 2));
        Assert.Equal((1, 1), CalendarModel.CountsOn(rows, 3));
        Assert.Equal((0, 0), CalendarModel.CountsOn(rows, 99));
    }

    [Fact]
    public void TheLegendListsPresenceAndEachPolicyAndDaysAreDescribed()
    {
        var model = CalendarModel.Create(Sample(), WithPresence, null);

        Assert.Equal(new[] { "Presenza", "FE · Ferie" }, model.Legend.Select(entry => entry.Text));
        Assert.True(model.Legend[0].IsPresence);
        Assert.Contains("FE · Ferie: 8h", model.Describe("Rossi Mario", new DateTime(2026, 3, 3)));
        Assert.Contains("oltre la giornata", model.Describe("Rossi Mario", new DateTime(2026, 3, 5)));
        Assert.Contains("Giorno lavorativo senza movimenti", model.Describe("Rossi Mario", new DateTime(2026, 3, 6)));
        Assert.Contains("Giorno non lavorativo", model.Describe("Rossi Mario", new DateTime(2026, 3, 7)));
        Assert.StartsWith("Rossi Mario · ", model.Describe("Rossi Mario", new DateTime(2026, 3, 3)));
    }

    [Fact]
    public void NoMovementsMeanAnEmptyCalendar()
    {
        var model = CalendarModel.Create([], WithPresence, null);

        Assert.True(model.IsEmpty);
        Assert.Empty(model.Months);
        Assert.Empty(model.RowsFor(2026, 3));
    }

    [Theory]
    [InlineData(480, "8", "8h")]
    [InlineData(450, "7,5", "7h 30m")]
    [InlineData(90, "1,5", "1h 30m")]
    public void HoursAreWrittenShortInTheCellAndLongInTheTooltip(int minutes, string cell, string text)
    {
        Assert.Equal(cell, CalendarModel.CellHours(minutes));
        Assert.Equal(text, CalendarModel.HoursText(minutes));
        Assert.Equal("Marzo 2026", CalendarModel.MonthName(2026, 3));
    }
}

[Collection("Wpf")]
public class WpfCalendarTests
{
    [Fact]
    public void TheCalendarViewBrowsesMonthsFiltersAndGoesToTheAlerts()
    {
        WpfTestHost.Run(() =>
        {
            WpfApp.EnsureApplication();
            var view = new CalendarView();
            var window = new Window
            {
                Width = 1100,
                Height = 560,
                Content = view,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -4000,
                Top = -4000
            };
            window.Show();
            WpfTestHost.Pump(300);
            Assert.Empty(view.Rows);

            view.SetModel(CalendarModel.Create(CalendarModelTests.Sample(), CalendarModelTests.WithPresence, null));
            WpfTestHost.Pump(450);
            Assert.Equal("Marzo 2026", view.MonthText);
            Assert.Equal(2, view.Rows.Count);
            Assert.Equal(2, view.LegendCount - 3);
            Assert.Contains("1 giorno con ore oltre la giornata", view.AlertText);

            view.Select(1);
            Assert.Equal("Aprile 2026", view.MonthText);
            Assert.Equal(30, view.Rows[0].Days.Count);
            view.Select(0);
            Assert.Equal("Marzo 2026", view.MonthText);

            view.Search("bianchi");
            Assert.Single(view.Rows);
            view.NextAlert();
            WpfTestHost.Pump(300);
            Assert.Equal(2, view.Rows.Count);
            Assert.Equal(("Rossi Mario", new DateTime(2026, 3, 5)), view.Focus);

            var bitmap = WpfShellTests.Render(window);
            Assert.True(WpfShellTests.CountMagenta(bitmap) == 0, "Calendario: colori mancanti (magenta).");
            Assert.True(WpfShellTests.DistinctColors(bitmap) > 12, "Calendario: disegno vuoto.");
            var directory = Environment.GetEnvironmentVariable("PRESENZEFLOW_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(directory, "wpf-Calendario.png"));
                encoder.Save(stream);
            }
            window.Close();

            // Senza anteprima il calendario si svuota e lo dice.
            view.SetModel(null);
            Assert.Empty(view.Rows);
            Assert.Equal("Calendario", view.MonthText);
        });
    }
}
