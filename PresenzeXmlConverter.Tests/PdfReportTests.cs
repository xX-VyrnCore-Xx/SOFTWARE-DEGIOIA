using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

/// <summary>Il report PDF è un file valido e contiene i numeri del riepilogo.</summary>
public class PdfReportTests
{
    private static string AllText(byte[] pdf)
    {
        // I flussi sono compressi (zlib): si decomprimono tutti e si uniscono, per cercarci dentro.
        var latin = Encoding.Latin1.GetString(pdf);
        var builder = new StringBuilder();
        foreach (Match match in Regex.Matches(latin, @"/Length (\d+) /Filter /FlateDecode >>\nstream\n"))
        {
            var start = match.Index + match.Length;
            var length = int.Parse(match.Groups[1].Value);
            using var input = new MemoryStream(pdf, start, length);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(zlib, Encoding.Latin1);
            builder.Append(reader.ReadToEnd());
        }
        return builder.ToString();
    }

    private static List<MonthlySummaryRow> Rows(int employees)
    {
        var rows = new List<MonthlySummaryRow>();
        for (var index = 1; index <= employees; index++)
        {
            rows.Add(new MonthlySummaryRow(
                $"Dipendente {index:00}",
                $"{index:0000000}",
                new DateTime(2026, 9, 1),
                PresenceMinutes: 19 * 480,
                AbsenceMinutes: 3 * 480,
                ExpectedMinutes: 22 * 480,
                UncoveredDays: 0,
                Absences: [("FE", "Ferie", 3 * 480)]));
        }
        return rows;
    }

    [Fact]
    public void ThePdfHasAValidStructureWithCorrectCrossReferenceOffsets()
    {
        var pdf = MonthlyReport.Build("Azienda AZ001", Rows(3), [], new DateTime(2026, 10, 1, 9, 30, 0));
        var text = Encoding.Latin1.GetString(pdf);

        Assert.StartsWith("%PDF-1.4", text);
        Assert.EndsWith("%%EOF\n", text);
        var startXref = int.Parse(Regex.Match(text, @"startxref\n(\d+)\n").Groups[1].Value);
        Assert.StartsWith("xref", text[startXref..]);
        // Ogni voce della tabella punta davvero a «N 0 obj».
        var entries = Regex.Matches(text[startXref..], @"(\d{10}) 00000 n ");
        Assert.NotEmpty(entries);
        for (var index = 0; index < entries.Count; index++)
        {
            var offset = int.Parse(entries[index].Groups[1].Value);
            Assert.StartsWith($"{index + 1} 0 obj", text[offset..]);
        }
        Assert.Contains($"/Size {entries.Count + 1}", text);
    }

    [Fact]
    public void TheReportShowsEmployeesTotalsAndTheCompany()
    {
        var pdf = MonthlyReport.Build("Azienda AZ001 · Rossi Srl", Rows(2), [], new DateTime(2026, 10, 1, 9, 30, 0));
        var content = AllText(pdf);

        Assert.Contains("(Dipendente 01)", content);
        Assert.Contains("(Dipendente 02)", content);
        // 19 giorni di presenza = 152 h, 3 di ferie = 24 h, totale 176 h = teoriche.
        Assert.Contains("(152 h)", content);
        Assert.Contains("(24 h)", content);
        Assert.Contains("(176 h)", content);
        Assert.Contains("Settembre 2026", content);
        Assert.Contains("Azienda AZ001", content);
        Assert.Contains("(FE \\(Ferie\\): 24 h)", content);
        Assert.Contains("Nessuna anomalia", content);
    }

    [Fact]
    public void ManyEmployeesFlowOverSeveralPagesWithNumberedFooters()
    {
        var pdf = MonthlyReport.Build("Azienda AZ001", Rows(120), [], new DateTime(2026, 10, 1));
        var text = Encoding.Latin1.GetString(pdf);
        var pages = int.Parse(Regex.Match(text, @"/Count (\d+)").Groups[1].Value);

        Assert.True(pages >= 4, $"Solo {pages} pagine per 120 dipendenti.");
        Assert.Contains($"(Pagina {pages} di {pages})", AllText(pdf));
    }

    [Fact]
    public void AlertsAreListedWithTheirReason()
    {
        var alerts = new[]
        {
            new CalendarAlert("Fab Patacca", new DateTime(2026, 9, 7), AlertSeverity.Check, "13h 20m in un giorno: oltre la giornata di 8h."),
            new CalendarAlert("Laura Zomparelli", new DateTime(2026, 9, 2), AlertSeverity.Notice, "più giustificativi nello stesso giorno (FE, PF).")
        };

        var content = AllText(MonthlyReport.Build("Azienda AZ001", Rows(1), alerts, new DateTime(2026, 10, 1)));

        Assert.Contains("Fab Patacca", content);
        Assert.Contains("13h 20m", content);
        // «più» esce come byte WinAnsi (ù = 0xF9 = \371).
        Assert.Contains("pi\\371 giustificativi", content);
        Assert.DoesNotContain("Nessuna anomalia", content);
    }

    [Fact]
    public void TextIsEscapedAndAccentsUseWinAnsi()
    {
        Assert.Equal("a\\(b\\)c\\\\d", PdfPage.EscapeText("a(b)c\\d"));
        Assert.Equal("citt\\340", PdfPage.EscapeText("città"));
        Assert.Equal("\\205", PdfPage.EscapeText("…"));
        Assert.True(PdfPage.TextWidth("Mario Rossi", 10) > PdfPage.TextWidth("Mario", 10));
        Assert.True(PdfPage.TextWidth("Mario", 10, bold: true) > PdfPage.TextWidth("Mario", 10));
    }

    [Fact]
    public void LongNamesAreShortenedToTheColumn()
    {
        var shortened = PdfPage.Fit("Giovanni Battista Maria Alessandro De Santis Rossi", 80, 9);

        Assert.True(PdfPage.TextWidth(shortened, 9) <= 80);
        Assert.EndsWith("…", shortened);
    }
}
