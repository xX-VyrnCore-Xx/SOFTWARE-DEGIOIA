using ClosedXML.Excel;

namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Il foglio vero del cliente: intestazione alla riga 3, due colonne «Policy type» e
/// «Policy», periodo in testa di AGOSTO ma richieste di SETTEMBRE. L'app diceva
/// «nessun movimento» perché scartava tutte le richieste come fuori periodo.
/// </summary>
public class ClientWorkbookTests
{
    private static string CreateClientLikeWorkbook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cliente-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Italy Payroll Attendence");
        sheet.Cell(1, 1).Value = "From Date";
        sheet.Cell(1, 2).Value = new DateTime(2026, 8, 1);
        sheet.Cell(2, 1).Value = "To Date";
        sheet.Cell(2, 2).Value = new DateTime(2026, 8, 31);
        string[] headers =
        [
            "Display name", "Policy type", "Policy", "Request Period", "Start date", "End date",
            "Total duration", "Requested on", "Waiting for approver", "Description", "Attachment", "Duration in range"
        ];
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(3, column + 1).Value = headers[column];
        }
        (string Name, DateTime Start, DateTime End, int Hours)[] rows =
        [
            ("Antenori Vincenzo", new DateTime(2026, 9, 17), new DateTime(2026, 9, 18), 16),
            ("Antenori Vincenzo", new DateTime(2026, 9, 24), new DateTime(2026, 9, 24), 8),
            ("Bertani Sofia", new DateTime(2026, 9, 24), new DateTime(2026, 9, 24), 4),
        ];
        for (var index = 0; index < rows.Length; index++)
        {
            var row = 4 + index;
            sheet.Cell(row, 1).Value = rows[index].Name;
            sheet.Cell(row, 2).Value = "Vacation (hours)";
            sheet.Cell(row, 3).Value = "IT vacation (hours)";
            sheet.Cell(row, 4).Value = rows[index].Start;
            sheet.Cell(row, 5).Value = rows[index].Start;
            sheet.Cell(row, 6).Value = rows[index].End;
            sheet.Cell(row, 7).Value = rows[index].Hours;
            sheet.Cell(row, 12).Value = rows[index].Hours;
        }
        workbook.SaveAs(path);
        return path;
    }

    [Fact]
    public void PolicyColumnWinsOverPolicyTypeWhenTheSheetHasBoth()
    {
        var path = CreateClientLikeWorkbook();
        try
        {
            var inspection = AttendanceWorkbookReader.Inspect(path);
            var columns = inspection.SuggestedColumns;
            Assert.NotNull(columns);
            Assert.Equal(2, columns!.Policy);
            var requests = inspection.ReadRequests(columns);
            Assert.All(requests, request => Assert.Equal("IT vacation (hours)", request.Policy));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ASheetPeriodThatContainsNoRequestIsIgnoredInsteadOfDiscardingEverything()
    {
        var path = CreateClientLikeWorkbook();
        try
        {
            var inspection = AttendanceWorkbookReader.Inspect(path);
            Assert.Equal(new DateTime(2026, 8, 1), inspection.PeriodStart);
            var requests = inspection.ReadRequests(inspection.SuggestedColumns!);

            var result = AttendanceMovementGenerator.Generate(
                requests,
                new Dictionary<string, string> { ["Antenori Vincenzo"] = "0000001", ["Bertani Sofia"] = "0000002" },
                new Dictionary<string, string> { ["IT vacation (hours)"] = "001" },
                inspection.PeriodStart,
                inspection.PeriodEnd);

            Assert.Equal(4, result.Movements.Count);
            Assert.Contains(result.Warnings, warning => warning.Contains("non contiene nessuna richiesta", StringComparison.Ordinal));
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("fuori dal periodo", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ARequestOutsideAPeriodThatOtherRequestsShareIsStillExcluded()
    {
        var inside = new AttendanceRequest("A", "P", new DateTime(2026, 9, 7), new DateTime(2026, 9, 7), 8m, 4);
        var outside = new AttendanceRequest("A", "P", new DateTime(2026, 10, 5), new DateTime(2026, 10, 5), 8m, 5);

        var result = AttendanceMovementGenerator.Generate(
            [inside, outside],
            new Dictionary<string, string> { ["A"] = "0000001" },
            new Dictionary<string, string> { ["P"] = "001" },
            new DateTime(2026, 9, 1),
            new DateTime(2026, 9, 30));

        Assert.Single(result.Movements);
        Assert.Contains(result.Warnings, warning => warning.Contains("fuori dal periodo", StringComparison.Ordinal));
    }
}
