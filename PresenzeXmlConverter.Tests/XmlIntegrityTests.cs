using System.Text;

namespace PresenzeXmlConverter.Tests;

/// <summary>Lettura sicura, confronto fra ore volute e ore scritte, controllo del file consegnato. Il formato del file non cambia.</summary>
public class XmlIntegrityTests
{
    private static readonly DailyMovement[] Sample =
    [
        new("Mario Rossi", "1234567", "Ferie", "01", new DateTime(2026, 8, 17), 8, 0),
        new("Mario Rossi", "1234567", "Ferie", "01", new DateTime(2026, 8, 18), 4, 30),
        new("Anna Verdi", "7654321", "Permesso", "02", new DateTime(2026, 8, 17), 1, 0)
    ];

    private static byte[] Bytes(string xml) => new UTF8Encoding(false).GetBytes(xml);

    private static byte[] Written(XmlFormat? format = null) => Bytes(TrriPwXmlWriter.Create("AZ001", Sample, format));

    private static IReadOnlyList<DailyMovement> Expected() => TrriPwXmlWriter.Normalize("AZ001", Sample);

    [Fact]
    public void WhatTheWriterProducesMatchesTheMovementsInBothLayouts()
    {
        Assert.Empty(TrriPwXmlVerifier.Compare(Expected(), Written()));
        var perNode = new XmlFormat(TrriPwXmlWriter.RootElement, string.Empty, true);
        Assert.Empty(TrriPwXmlVerifier.Compare(Expected(), Written(perNode)));
        var withNamespace = new XmlFormat("Fornitura", "http://example.org/trripw", false);
        Assert.Empty(TrriPwXmlVerifier.Compare(Expected(), Written(withNamespace)));
    }

    [Fact]
    public void SameDayRequestsAreSummedBeforeTheComparison()
    {
        var overlapping = new[]
        {
            new DailyMovement("Mario Rossi", "1234567", "Ferie", "01", new DateTime(2026, 8, 17), 4, 0, 1),
            new DailyMovement("Mario Rossi", "1234567", "Ferie", "01", new DateTime(2026, 8, 17), 3, 30, 2)
        };
        var bytes = Bytes(TrriPwXmlWriter.Create("AZ001", overlapping));
        Assert.Empty(TrriPwXmlVerifier.Compare(TrriPwXmlWriter.Normalize("AZ001", overlapping), bytes));
        Assert.Equal(7 * 60 + 30, TrriPwXmlVerifier.ReadTotals(bytes).Values.Single());
    }

    [Fact]
    public void ChangedHoursAreReported()
    {
        var text = Encoding.UTF8.GetString(Written()).Replace("<NumOre>08</NumOre>", "<NumOre>07</NumOre>");
        var problems = TrriPwXmlVerifier.Compare(Expected(), Bytes(text));

        var problem = Assert.Single(problems);
        Assert.Contains("1234567", problem);
        Assert.Contains("17/08/2026", problem);
        Assert.Contains("7 h 00", problem);
        Assert.Contains("8 h 00", problem);
    }

    [Fact]
    public void MissingAndExtraMovementsAreReported()
    {
        var withoutAnna = Written();
        var text = Encoding.UTF8.GetString(withoutAnna);
        var start = text.IndexOf("<Dipendente CodAziendaUfficiale=\"AZ001\" CodDipendenteUfficiale=\"7654321\"", StringComparison.Ordinal);
        var end = text.IndexOf("</Dipendente>", start, StringComparison.Ordinal) + "</Dipendente>".Length;
        var cut = Bytes(text.Remove(start, end - start));

        Assert.Contains(TrriPwXmlVerifier.Compare(Expected(), cut), p => p.StartsWith("Manca nel file", StringComparison.Ordinal) && p.Contains("7654321"));
        Assert.Contains(
            TrriPwXmlVerifier.Compare(Expected().Take(2).ToArray(), Written()),
            p => p.StartsWith("In più nel file", StringComparison.Ordinal) && p.Contains("7654321"));
    }

    [Fact]
    public void NoiseInTheFileIsCappedAndAnUnreadableFileIsReportedNotThrown()
    {
        var many = Enumerable.Range(1, 40)
            .Select(day => new DailyMovement("Mario Rossi", "1234567", "Ferie", "01", new DateTime(2026, 1, 1).AddDays(day), 8, 0))
            .ToArray();
        var empty = Bytes("<Fornitura></Fornitura>");
        var problems = TrriPwXmlVerifier.Compare(TrriPwXmlWriter.Normalize("AZ001", many), empty);
        Assert.True(problems.Count <= 13);
        Assert.Contains("altre", problems[^1]);

        var broken = TrriPwXmlVerifier.Compare(Expected(), Bytes("<Fornitura><Dip"));
        Assert.StartsWith("Il file non si può rileggere", Assert.Single(broken));
    }

    [Fact]
    public void ADoctypeIsRefusedInsteadOfBeingResolved()
    {
        var hostile = Bytes("<?xml version=\"1.0\"?><!DOCTYPE Fornitura [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><Fornitura>&x;</Fornitura>");
        var issues = TrriPwXmlValidator.Validate(hostile);
        Assert.Contains(issues, issue => issue.StartsWith("XML non ben formato", StringComparison.Ordinal));
        Assert.Throws<System.Xml.XmlException>(() => TrriPwXmlVerifier.ReadTotals(hostile));
    }

    [Fact]
    public void ValidatorFlagsUnexpectedNodesImplausibleYearsAndRepeatedEmployees()
    {
        var text = Encoding.UTF8.GetString(Written());
        Assert.Contains(
            TrriPwXmlValidator.Validate(Bytes(text.Replace("</Fornitura>", "<Altro/></Fornitura>"))),
            issue => issue.Contains("Elemento inatteso «Altro»", StringComparison.Ordinal));
        Assert.Contains(
            TrriPwXmlValidator.Validate(Bytes(text.Replace("2026-08-17", "1926-08-17"))),
            issue => issue.Contains("fuori dal periodo plausibile", StringComparison.Ordinal));

        var start = text.IndexOf("<Dipendente ", StringComparison.Ordinal);
        var end = text.IndexOf("</Dipendente>", start, StringComparison.Ordinal) + "</Dipendente>".Length;
        var duplicated = text.Insert(end, text[start..end]);
        Assert.Contains(
            TrriPwXmlValidator.Validate(Bytes(duplicated)),
            issue => issue.Contains("compare in più nodi", StringComparison.Ordinal));
    }

    [Fact]
    public void TheAcceptedFormatIsUnchangedByteForByte()
    {
        // Documento di riferimento: la forma accettata da Zucchetti. Se questo test cambia, è cambiato il file consegnato.
        var one = new[] { new DailyMovement("Mario Rossi", "1234567", "Ferie", "01", new DateTime(2026, 8, 18), 4, 30) };
        var expected =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
            "<Fornitura>\r\n" +
            "  <Dipendente CodAziendaUfficiale=\"AZ001\" CodDipendenteUfficiale=\"1234567\">\r\n" +
            "    <Movimenti GenerazioneAutomaticaDaTeorico=\"N\">\r\n" +
            "      <Movimento>\r\n" +
            "        <CodGiustificativoRilPres>01</CodGiustificativoRilPres>\r\n" +
            "        <CodGiustificativoUfficiale>01</CodGiustificativoUfficiale>\r\n" +
            "        <Data>2026-08-18</Data>\r\n" +
            "        <NumOre>04</NumOre>\r\n" +
            "        <NumMinuti>30</NumMinuti>\r\n" +
            "      </Movimento>\r\n" +
            "    </Movimenti>\r\n" +
            "  </Dipendente>\r\n" +
            "</Fornitura>";
        Assert.Equal(expected, TrriPwXmlWriter.Create("AZ001", one, XmlFormat.Default));
    }

    [Fact]
    public void IntegrityCheckDistinguishesTheFourCases()
    {
        var delivered = Written();
        var altered = Written().Concat(Bytes("\n")).ToArray();

        Assert.Equal(FileIntegrity.Identical, ExportIntegrity.Check(delivered, Written()));
        Assert.Equal(FileIntegrity.Modified, ExportIntegrity.Check(delivered, altered));
        Assert.Equal(FileIntegrity.Missing, ExportIntegrity.Check(delivered, null));
        Assert.Equal(FileIntegrity.NoCopy, ExportIntegrity.Check(null, delivered));
    }

    [Fact]
    public void IntegrityMessagesNameTheFileAndShowTheFingerprint()
    {
        var delivered = Written();
        var identical = ExportIntegrity.Describe(FileIntegrity.Identical, "ottobre.xml", delivered);
        Assert.Contains("ottobre.xml", identical);
        Assert.Contains("identico", identical);
        Assert.Contains(ExportIntegrity.Sha256Hex(delivered), identical);
        Assert.Contains("modificato", ExportIntegrity.Describe(FileIntegrity.Modified, "a.xml", delivered));
        Assert.Contains("non si trova più", ExportIntegrity.Describe(FileIntegrity.Missing, "a.xml"));
        Assert.Contains("non conserva la copia", ExportIntegrity.Describe(FileIntegrity.NoCopy, "a.xml"));
    }

    [Fact]
    public void FingerprintIsTheKnownSha256()
    {
        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            ExportIntegrity.Sha256Hex(Encoding.ASCII.GetBytes("abc")));
    }
}
