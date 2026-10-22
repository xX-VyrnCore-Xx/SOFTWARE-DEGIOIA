using System.Text;

namespace PresenzeXmlConverter.Tests;

public class CsvImportTests
{
    private static string WriteFile(byte[] content, string extension = ".csv")
    {
        var path = Path.Combine(Path.GetTempPath(), $"presenze-csv-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, content);
        return path;
    }

    [Theory]
    [InlineData("a;b;c\n1;2;3\n4;5;6\n", ';')]
    [InlineData("a,b,c\n1,2,3\n4,5,6\n", ',')]
    [InlineData("a\tb\tc\n1\t2\t3\n", '\t')]
    [InlineData("a|b|c\n1|2|3\n", '|')]
    [InlineData("Nome;Ore\nRossi;8,5\nBianchi;7,5\n", ';')]
    [InlineData("\"Rossi, Mario\";Ferie;8\n\"Bianchi, Anna\";Malattia;1\n", ';')]
    public void TheDelimiterIsRecognisedFromTheContent(string text, char expected) =>
        Assert.Equal(expected, CsvImport.DetectDelimiter(text));

    [Fact]
    public void QuotesEscapedQuotesAndLineBreaksInsideCellsAreRead()
    {
        var rows = CsvImport.Parse("Nome;Nota\r\n\"Rossi; Mario\";\"dice \"\"ciao\"\"\"\r\n\"Bianchi\";\"due\nrighe\"\r\n\r\n");

        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "Nome", "Nota" }, rows[0]);
        Assert.Equal(new[] { "Rossi; Mario", "dice \"ciao\"" }, rows[1]);
        Assert.Equal(new[] { "Bianchi", "due\nrighe" }, rows[2]);
    }

    [Fact]
    public void Utf8WithBomAndWindowsEncodingsAreBothDecoded()
    {
        var utf8 = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Nicolò;Perché")).ToArray();
        Assert.Equal("Nicolò;Perché", CsvImport.Decode(utf8));

        var windows = Encoding.Latin1.GetBytes("Nicolò;Perché");
        Assert.Equal("Nicolò;Perché", CsvImport.Decode(windows));
    }

    [Fact]
    public void ACsvIsReadLikeAWorkbookWithItalianDatesAndDays()
    {
        const string text =
            "Employee Name;Policy Name;Start Date;End Date;Duration\r\n" +
            "Rossi Mario;Ferie;03/03/2026;05/03/2026;24\r\n" +
            "\"Bianchi, Anna\";Malattia;10/03/2026;10/03/2026;1\r\n" +
            "Verdi Luca;Permesso;12/03/2026;12/03/2026;2,5\r\n";
        var path = WriteFile(Encoding.Latin1.GetBytes(text));
        try
        {
            var inspection = AttendanceWorkbookReader.Inspect(path);
            Assert.Equal(CsvImport.SheetName, inspection.SheetName);
            Assert.Equal(new[] { CsvImport.SheetName }, AttendanceWorkbookReader.ListSheetNames(path));
            Assert.NotNull(inspection.SuggestedColumns);

            var requests = inspection.ReadRequests(inspection.SuggestedColumns!);

            Assert.Equal(3, requests.Count);
            var ferie = requests.Single(request => request.Policy == "Ferie");
            Assert.Equal(new DateTime(2026, 3, 3), ferie.StartDate);
            Assert.Equal(new DateTime(2026, 3, 5), ferie.EndDate);
            Assert.Equal(24m, ferie.Hours);
            var malattia = requests.Single(request => request.Policy == "Malattia");
            Assert.Equal("Bianchi, Anna", malattia.EmployeeName);
            Assert.Equal(8m, malattia.Hours);
            Assert.Equal(2.5m, requests.Single(request => request.Policy == "Permesso").Hours);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ACsvWithOnlyTheHeaderIsRejectedWithAClearMessage()
    {
        var path = WriteFile(Encoding.UTF8.GetBytes("Employee Name;Policy Name;Start Date;End Date;Duration\n"));
        try
        {
            var error = Assert.Throws<InvalidDataException>(() => AttendanceWorkbookReader.Inspect(path));
            Assert.Contains("almeno", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnEmptyCsvIsRejected()
    {
        var path = WriteFile([]);
        try
        {
            Assert.Throws<InvalidDataException>(() => AttendanceWorkbookReader.Inspect(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("presenze.csv", true)]
    [InlineData("PRESENZE.TSV", true)]
    [InlineData("presenze.xlsx", false)]
    [InlineData("presenze.csv.xlsx", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyCsvAndTsvAreCsv(string? path, bool expected) =>
        Assert.Equal(expected, CsvImport.IsCsv(path));
}
