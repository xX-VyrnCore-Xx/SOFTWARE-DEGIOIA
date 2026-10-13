using System.Text;

namespace PresenzeXmlConverter.Tests;

public class TrriPwXmlValidatorTests
{
    private static readonly DailyMovement[] Sample =
    [
        new("Mario Rossi", "1234567", "Ferie", "01", new DateTime(2026, 8, 17), 8, 0),
        new("Mario Rossi", "1234567", "Ferie", "01", new DateTime(2026, 8, 18), 4, 30),
        new("Anna Verdi", "7654321", "Permesso", "02", new DateTime(2026, 8, 17), 1, 0)
    ];

    private static byte[] Bytes(string xml) => new UTF8Encoding(false).GetBytes(xml);

    [Fact]
    public void WhatTheWriterProducesAlwaysPassesTheValidator()
    {
        var xml = TrriPwXmlWriter.Create("AZ001", Sample);

        Assert.Empty(TrriPwXmlValidator.Validate(Bytes(xml)));
    }

    [Fact]
    public void AFileWrittenToDiskPassesAfterBeingReadBack()
    {
        var path = Path.Combine(Path.GetTempPath(), $"trripw-{Guid.NewGuid():N}.xml");
        try
        {
            TrriPwXmlWriter.SaveFile(path, "AZ001", Sample);

            Assert.Empty(TrriPwXmlValidator.ValidateFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ABomATruncatedFileAndAMissingFileAreReported()
    {
        var xml = TrriPwXmlWriter.Create("AZ001", Sample);
        var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Bytes(xml)).ToArray();

        Assert.Contains(TrriPwXmlValidator.Validate(withBom), issue => issue.Contains("BOM", StringComparison.Ordinal));
        Assert.Contains(
            TrriPwXmlValidator.Validate(Bytes(xml[..(xml.Length / 2)])),
            issue => issue.Contains("non ben formato", StringComparison.Ordinal));
        Assert.NotEmpty(TrriPwXmlValidator.ValidateFile(Path.Combine(Path.GetTempPath(), $"assente-{Guid.NewGuid():N}.xml")));
    }

    [Fact]
    public void TheAutomaticTheoreticalGenerationAttributeBelongsToTheMovementsNode()
    {
        const string wrongPlace = """
            <TRRIPW><Dipendente GenerazioneAutomaticaDaTeorico="N">
            <CodAziendaUfficiale>AZ001</CodAziendaUfficiale><CodDipendenteUfficiale>1234567</CodDipendenteUfficiale>
            <Movimenti><Movimento><CodGiustificativoUfficiale>01</CodGiustificativoUfficiale><Data>2026-08-17</Data><NumOre>8</NumOre><NumMinuti>0</NumMinuti></Movimento></Movimenti>
            </Dipendente></TRRIPW>
            """;
        const string teoretical = wrongPlace;

        Assert.Contains(TrriPwXmlValidator.Validate(Bytes(teoretical)), issue => issue.Contains("Movimenti", StringComparison.Ordinal));
        Assert.Contains(
            TrriPwXmlValidator.Validate(Bytes(teoretical.Replace(
                "<Dipendente GenerazioneAutomaticaDaTeorico=\"N\">", "<Dipendente>").Replace(
                "<Movimenti>", "<Movimenti GenerazioneAutomaticaDaTeorico=\"S\">"))),
            issue => issue.Contains("scarterebbe", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("<NumOre>8</NumOre>", "<NumOre>100</NumOre>")]
    [InlineData("<NumMinuti>0</NumMinuti>", "<NumMinuti>60</NumMinuti>")]
    [InlineData("<Data>2026-08-17</Data>", "<Data>17/08/2026</Data>")]
    [InlineData("<CodGiustificativoUfficiale>01</CodGiustificativoUfficiale>", "<CodGiustificativoUfficiale>0123</CodGiustificativoUfficiale>")]
    public void FieldsOutsideTheLayoutAreReported(string original, string broken)
    {
        var xml = TrriPwXmlWriter.Create("AZ001", Sample);

        var problems = TrriPwXmlValidator.Validate(Bytes(xml.Replace(original, broken)));

        Assert.NotEmpty(problems);
    }

    [Fact]
    public void ADuplicatedMovementIsReported()
    {
        var xml = TrriPwXmlWriter.Create("AZ001", Sample);
        var movement = xml[xml.IndexOf("<Movimento>", StringComparison.Ordinal)..(xml.IndexOf("</Movimento>", StringComparison.Ordinal) + "</Movimento>".Length)];
        var duplicated = xml.Replace(movement, movement + movement);

        Assert.Contains(TrriPwXmlValidator.Validate(Bytes(duplicated)), issue => issue.Contains("duplicato", StringComparison.Ordinal));
    }
}
