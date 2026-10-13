namespace PresenzeXmlConverter.Tests;

/// <summary>Il tracciato TRRIPA è posizionale: ogni campo deve stare dove il PDF lo mette.</summary>
public class TrripaWriterTests
{
    private static readonly DailyMovement[] Sample =
    [
        new("Rossi Mario", "0000012", "IT vacation", "01", new DateTime(2026, 9, 17), 8, 0, 4),
        new("Rossi Mario", "0000012", "IT vacation", "01", new DateTime(2026, 9, 18), 4, 30, 5),
        new("Bianchi Anna", "0000034", "IT vacation", "7", new DateTime(2026, 9, 17), 8, 0, 6)
    ];

    [Fact]
    public void EveryRecordIsOneHundredCharactersWithHeaderBeforeItsMovements()
    {
        var lines = TrripaWriter.Create("000198", Sample)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(5, lines.Length);
        Assert.All(lines, line => Assert.Equal(TrripaWriter.RecordLength, line.Length));
        Assert.Equal(["00", "01", "01", "00", "01"], lines.Select(line => line[..2]));
    }

    [Fact]
    public void FieldsSitAtThePositionsOfThePdf()
    {
        var lines = TrripaWriter.Create("000198", Sample)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        // Testata RIPA00: codice rilevazione a 3-23, soggetto 24-31 vuoto, rapporto 32-34 a zero, teorico "N".
        Assert.Equal("0000012", lines[0].Substring(2, 21).Trim());
        Assert.Equal(new string(' ', 8), lines[0].Substring(23, 8));
        Assert.Equal("000", lines[0].Substring(31, 3));
        Assert.Equal('N', lines[0][34]);
        // Movimento RIPA01: giustificativo Paghe 8-9, data 10-17, ore 18-19, minuti 20-21.
        Assert.Equal("     ", lines[1].Substring(2, 5));
        Assert.Equal("01", lines[1].Substring(7, 2));
        Assert.Equal("20260917", lines[1].Substring(9, 8));
        Assert.Equal("08", lines[1].Substring(17, 2));
        Assert.Equal("00", lines[1].Substring(19, 2));
        Assert.Equal("30", lines[2].Substring(19, 2));
        Assert.StartsWith("0000034", lines[3].Substring(2, 21));
        // Un codice di una cifra diventa a due cifre.
        Assert.Equal("07", lines[4].Substring(7, 2));
    }

    [Fact]
    public void AReasonCodeLongerThanTwoCharactersIsRefused()
    {
        var movements = new[]
        {
            new DailyMovement("Rossi Mario", "0000012", "p", "001", new DateTime(2026, 9, 17), 8, 0, 4)
        };
        var error = Assert.Throws<InvalidDataException>(() => TrripaWriter.Create("000198", movements));
        Assert.Contains("001", error.Message);
    }

    [Fact]
    public void ASavedFileValidatesAndABrokenOneDoesNot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"trripa-{Guid.NewGuid():N}.txt");
        try
        {
            TrripaWriter.SaveFile(path, "000198", Sample);
            Assert.Empty(TrripaWriter.ValidateFile(path));
            File.AppendAllText(path, "01 troppo corto\r\n");
            Assert.NotEmpty(TrripaWriter.ValidateFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheSuggestedNameUsesTheTrripaPrefixAndTxt()
    {
        var name = ExportHistory.SuggestFileName("000198", null, Sample, trripa: true);
        Assert.Equal("TRRIPA_000198_2026-09.txt", name);
    }
}
