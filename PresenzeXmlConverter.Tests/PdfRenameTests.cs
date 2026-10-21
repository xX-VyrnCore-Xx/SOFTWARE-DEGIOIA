namespace PresenzeXmlConverter.Tests;

using System.IO.Compression;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

public class PdfRenameTests
{
    private static byte[] MakePdf(params (string Body, string Footer)[] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var (body, footer) in pages)
        {
            var page = builder.AddPage(595, 842);
            page.AddText(body, 12, new PdfPoint(60, 760), font);
            page.AddText("Totale 1.234,00 del 21/10/2026", 10, new PdfPoint(60, 90), font);
            page.AddText(footer, 10, new PdfPoint(60, 40), font);
        }
        return builder.Build();
    }

    private static string MakeZip(string directory)
    {
        var path = Path.Combine(directory, "in.zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string name, byte[] data)
        {
            using var stream = zip.CreateEntry(name).Open();
            stream.Write(data);
        }
        Add("a.pdf", MakePdf(("Cliente Rossi", "Cod. 123-456-7"), ("Cliente Bianchi", "123-457-0"), ("Cliente Verdi", "123-456-7")));
        Add("sub/b.pdf", MakePdf(("Cliente Neri", "nessun codice"), ("Cliente Gialli", "999-1-2 e 888-3-4"), ("Cliente X", "124-111-9")));
        Add("__MACOSX/._a.pdf", [1, 2, 3]);
        Add("nota.txt", [65]);
        return path;
    }

    [Fact]
    public void ItReadsTheCodeAtTheBottomOfEveryPageAndFlagsWhatDoesNotAddUp()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var unreadable = new List<string>();
            var items = PdfRenamePlanner.Build(PdfRenamePlanner.LoadFiles([MakeZip(directory)]), unreadable);

            Assert.Empty(unreadable);
            Assert.Equal(6, items.Count);
            Assert.Equal("123-456-7", items[0].Code);
            Assert.Equal(PdfCodeStatus.Check, items[0].Status);
            Assert.Equal("123-457-0.pdf", items[1].FileName);
            Assert.Equal(PdfCodeStatus.Ok, items[1].Status);
            Assert.Equal("123-456-7 (2).pdf", items[2].FileName);
            Assert.Equal(PdfCodeStatus.Missing, items[3].Status);
            Assert.Null(items[3].FileName);
            Assert.Null(items[4].Code);
            Assert.Contains("Più codici", items[4].Note);
            Assert.Equal("124-111-9.pdf", items[5].FileName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ItWritesOnePagePerFileAndVerifiesTheResult()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var items = PdfRenamePlanner.Build(PdfRenamePlanner.LoadFiles([MakeZip(directory)]), []);
            items[3].FinalCode = "555-5-5";
            items[4].FinalCode = "555-5-6";
            PdfRenamePlanner.Recheck(items);
            var output = Path.Combine(directory, "out.zip");

            Assert.Equal(6, PdfRenameWriter.WriteZip(items, output));
            Assert.Empty(PdfRenameWriter.Verify(items, output));
            using var zip = ZipFile.OpenRead(output);
            Assert.Equal(
                ["123-456-7 (2).pdf", "123-456-7.pdf", "123-457-0.pdf", "124-111-9.pdf", "555-5-5.pdf", "555-5-6.pdf"],
                zip.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ItSkipsPagesWithoutACodeAndDetectsATamperedArchive()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var items = PdfRenamePlanner.Build(PdfRenamePlanner.LoadFiles([MakeZip(directory)]), []);
            var output = Path.Combine(directory, "out.zip");
            Assert.Equal(4, PdfRenameWriter.WriteZip(items, output));
            using (var zip = ZipFile.Open(output, ZipArchiveMode.Update))
            {
                zip.GetEntry("123-457-0.pdf")!.Delete();
            }
            Assert.NotEmpty(PdfRenameWriter.Verify(items, output));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("123-456-7", "3-3-1")]
    [InlineData("12-3", "2-1")]
    public void TheShapeCountsTheDigitsOfEachGroup(string code, string shape) =>
        Assert.Equal(shape, PdfCodeFinder.Shape(code));
}
