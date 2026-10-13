namespace PresenzeXmlConverter.Tests;

public class XmlRtfTests
{
    private static readonly XmlRtf.Palette Palette = new((226, 232, 240), (125, 211, 252), (190, 242, 100), (167, 243, 208));

    [Fact]
    public void TheDocumentHasAColorTableAndColoursTagsAttributesAndValues()
    {
        var xml = "<Movimenti GenerazioneAutomaticaDaTeorico=\"N\">\r\n  <NumOre>8</NumOre>\r\n</Movimenti>";

        var rtf = XmlRtf.Build(xml, Palette, "Consolas", 10f);

        Assert.StartsWith(@"{\rtf1", rtf, StringComparison.Ordinal);
        Assert.EndsWith("}", rtf, StringComparison.Ordinal);
        Assert.Contains(@"\red125\green211\blue252;", rtf, StringComparison.Ordinal);
        Assert.Contains(@"\fs20", rtf, StringComparison.Ordinal);
        Assert.Contains(@"\cf2 <Movimenti GenerazioneAutomaticaDaTeorico=", rtf, StringComparison.Ordinal);
        Assert.Contains("\\cf3 \"N\"", rtf, StringComparison.Ordinal);
        Assert.Contains(@"\cf4 8", rtf, StringComparison.Ordinal);
        Assert.Contains("\\par\r\n", rtf, StringComparison.Ordinal);
    }

    [Fact]
    public void TextThatIsSpecialInRtfIsEscaped()
    {
        var rtf = XmlRtf.Build("<Nota>a\\b {c} è</Nota>", Palette, "Consolas", 10f);

        Assert.Contains(@"a\\b \{c\} \u232?", rtf, StringComparison.Ordinal);
    }

    [Fact]
    public void AMegabyteOfXmlIsConvertedQuickly()
    {
        var movements = Enumerable.Range(0, 6000)
            .Select(index => new DailyMovement(
                $"Dipendente {index % 300}", (1000000 + (index % 300)).ToString(), "Ferie", "01",
                new DateTime(2026, 8, 1).AddDays(index / 300), 8, 0))
            .ToArray();
        var xml = TrriPwXmlWriter.Create("AZ001", movements);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var rtf = XmlRtf.Build(xml, Palette, "Consolas", 10f);
        stopwatch.Stop();

        Assert.True(rtf.Length > xml.Length);
        Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"Troppo lento: {stopwatch.ElapsedMilliseconds} ms.");
    }
}
