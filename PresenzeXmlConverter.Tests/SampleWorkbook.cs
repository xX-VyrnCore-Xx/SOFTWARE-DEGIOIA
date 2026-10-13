using ClosedXML.Excel;

namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Foglio di esempio generato dai test: prima i test leggevano un file del cliente
/// tenuto nel repository, e cancellarlo faceva fallire la compilazione del progetto
/// di test (file da copiare non trovato). Ora il foglio si crea al volo, con la
/// stessa forma: periodo in testa, 35 dipendenti, 9 giustificativi, 72 richieste.
/// </summary>
internal static class SampleWorkbook
{
    public const string SheetName = "Italy Payroll Attendence";
    public const int RequestCount = 72;
    public const int EmployeeCount = 35;
    public const int PolicyCount = 9;

    private static readonly Lazy<string> SharedPath = new(Create);

    /// <summary>Percorso del foglio, creato una sola volta per esecuzione dei test.</summary>
    public static string Path => SharedPath.Value;

    private static string Create()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"presenze-sample-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName);
        sheet.Cell(1, 1).Value = "From Date";
        sheet.Cell(1, 2).Value = new DateTime(2026, 8, 1);
        sheet.Cell(2, 1).Value = "To Date";
        sheet.Cell(2, 2).Value = new DateTime(2026, 8, 31);

        string[] headers = ["Employee Name", "Policy Name", "Start Date", "End Date", "Duration"];
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(4, column + 1).Value = headers[column];
        }

        // Giorni feriali di agosto 2026 (esclusi i festivi): ogni richiesta è di una
        // sola giornata intera o di mezza giornata, quindi entra sempre nei feriali.
        var days = Enumerable.Range(0, 31)
            .Select(offset => new DateTime(2026, 8, 1).AddDays(offset))
            .Where(day => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !ItalianHolidays.Contains(day))
            .ToArray();
        for (var index = 0; index < RequestCount; index++)
        {
            var day = days[index % days.Length];
            var row = 5 + index;
            sheet.Cell(row, 1).Value = $"Dipendente {(index % EmployeeCount) + 1:D2}";
            sheet.Cell(row, 2).Value = $"IT policy {(index % PolicyCount) + 1}";
            sheet.Cell(row, 3).Value = day;
            sheet.Cell(row, 4).Value = day;
            sheet.Cell(row, 5).Value = index % 3 == 0 ? 4 : 8;
        }

        workbook.SaveAs(path);
        return path;
    }
}
