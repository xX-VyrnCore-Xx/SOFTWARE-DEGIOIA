using System.Xml.Linq;

namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Zucchetti valida il file contro uno schema e respinge una radice che non conosce
/// («cvc-elt.1»). La radice e lo spazio dei nomi non sono nel tracciato: si impostano o
/// si leggono da un file accettato, e il file scritto li rispetta.
/// </summary>
public class XmlFormatTests
{
    private static readonly DailyMovement[] Sample =
    [
        new("Rossi Mario", "0000012", "IT vacation", "001", new DateTime(2026, 9, 17), 8, 0, 4),
        new("Rossi Mario", "0000012", "IT vacation", "001", new DateTime(2026, 9, 18), 4, 30, 5),
        new("Bianchi Anna", "0000034", "IT vacation", "001", new DateTime(2026, 9, 17), 8, 0, 6)
    ];

    [Fact]
    public void TheDefaultFormatHasNoNamespaceAndWrapsMovements()
    {
        var xml = XDocument.Parse(TrriPwXmlWriter.Create("000198", Sample));
        Assert.Equal(XmlFormat.Default.RootElement, xml.Root!.Name.LocalName);
        Assert.Equal(string.Empty, xml.Root.Name.NamespaceName);
        Assert.Equal(3, xml.Descendants("Movimento").Count());
    }

    [Fact]
    public void ACustomRootAndNamespaceAreWrittenOnEveryElementAndValidate()
    {
        var format = new XmlFormat("Fornitura", "http://www.esempio.it/presenze", false);
        var bytes = System.Text.Encoding.UTF8.GetBytes(TrriPwXmlWriter.Create("000198", Sample, format));
        var xml = XDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes));
        XNamespace ns = "http://www.esempio.it/presenze";

        Assert.Equal(ns + "Fornitura", xml.Root!.Name);
        Assert.All(xml.Descendants(), element => Assert.Equal(ns, element.Name.Namespace));
        Assert.Empty(TrriPwXmlValidator.Validate(bytes, format));
        // Con la forma predefinita lo stesso file non è valido: la radice non coincide.
        Assert.NotEmpty(TrriPwXmlValidator.Validate(bytes, XmlFormat.Default));
    }

    [Fact]
    public void OneMovimentiNodePerDayIsWrittenAndValidated()
    {
        var format = XmlFormat.Default with { MovementPerNode = true };
        var text = TrriPwXmlWriter.Create("000198", Sample, format);
        var xml = XDocument.Parse(text);

        Assert.Empty(xml.Descendants("Movimento"));
        Assert.Equal(3, xml.Descendants("Movimenti").Count());
        Assert.All(xml.Descendants("Movimenti"), node =>
        {
            Assert.Equal("N", (string?)node.Attribute("GenerazioneAutomaticaDaTeorico"));
            Assert.NotNull(node.Element("Data"));
        });
        Assert.Empty(TrriPwXmlValidator.Validate(System.Text.Encoding.UTF8.GetBytes(text), format));
    }

    [Fact]
    public void TheFormatIsReadFromASampleFileTheValidatorAccepts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"campione-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(
                path,
                "<?xml version=\"1.0\"?><Presenze xmlns=\"urn:zucchetti:presenze\"><Dipendente>" +
                "<CodAziendaUfficiale>000198</CodAziendaUfficiale><CodDipendenteUfficiale>0000012</CodDipendenteUfficiale>" +
                "<Movimenti><Data>2026-09-17</Data></Movimenti></Dipendente></Presenze>");

            Assert.True(XmlFormat.TryDetect(path, out var format, out var message));
            Assert.Equal("Presenze", format.RootElement);
            Assert.Equal("urn:zucchetti:presenze", format.Namespace);
            Assert.True(format.MovementPerNode);
            Assert.Contains("Presenze", message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NamesAndNamespacesAreChecked()
    {
        Assert.True(XmlFormat.IsValidElementName("Dipendenti"));
        Assert.True(XmlFormat.IsValidElementName("Rilevazione_Presenze"));
        Assert.False(XmlFormat.IsValidElementName("due parole"));
        Assert.False(XmlFormat.IsValidElementName("1numero"));
        Assert.False(XmlFormat.IsValidElementName("  "));
        Assert.True(XmlFormat.IsValidNamespace(""));
        Assert.True(XmlFormat.IsValidNamespace("http://www.esempio.it/x"));
        Assert.False(XmlFormat.IsValidNamespace("non un indirizzo"));
    }
}

/// <summary>
/// Un XML che Zucchetti ha importato davvero (esportato dal programma del cliente): fissa
/// radice, attributi di «Dipendente», «Movimenti» con un solo attributo e campi del movimento.
/// </summary>
public class ZucchettiAcceptedSampleTests
{
    private const string Accepted = """
        <?xml version="1.0" encoding="utf-8" ?>
        <Fornitura>
        <Dipendente CodAziendaUfficiale="000023" CodDipendenteUfficiale="0000001">
        <Movimenti GenerazioneAutomaticaDaTeorico="N">
        <Movimento>
        <CodGiustificativoRilPres>FE</CodGiustificativoRilPres>
        <CodGiustificativoUfficiale>FE</CodGiustificativoUfficiale>
        <Data>2026-06-01</Data>
        <NumOre>08</NumOre>
        </Movimento>
        <Movimento>
        <CodGiustificativoRilPres>334</CodGiustificativoRilPres>
        <CodGiustificativoUfficiale>999</CodGiustificativoUfficiale>
        <Data>2026-06-03</Data>
        <NumOre>08</NumOre>
        </Movimento>
        </Movimenti>
        </Dipendente>
        </Fornitura>
        """;

    [Fact]
    public void TheAcceptedSampleValidatesAndMatchesTheDefaultFormat()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(Accepted);
        Assert.Empty(TrriPwXmlValidator.Validate(bytes));
        var path = Path.Combine(Path.GetTempPath(), $"accettato-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(path, Accepted);
            Assert.True(XmlFormat.TryDetect(path, out var format, out _));
            Assert.Equal(XmlFormat.Default, format with { MovementPerNode = false });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheWriterProducesTheSameShapeAsTheAcceptedSample()
    {
        var movements = new[]
        {
            new DailyMovement("Rossi", "0000001", "Ferie", "FE", new DateTime(2026, 6, 1), 8, 0, 4),
            new DailyMovement("Rossi", "0000001", "Ferie", "FE", new DateTime(2026, 6, 5), 4, 0, 5)
        };
        var accepted = System.Xml.Linq.XDocument.Parse(Accepted);
        var written = System.Xml.Linq.XDocument.Parse(TrriPwXmlWriter.Create("000023", movements));

        Assert.Equal(accepted.Root!.Name, written.Root!.Name);
        var employee = written.Root.Element("Dipendente")!;
        Assert.Equal(
            accepted.Root.Element("Dipendente")!.Attributes().Select(attribute => attribute.Name.LocalName),
            employee.Attributes().Select(attribute => attribute.Name.LocalName));
        Assert.Empty(employee.Elements().Where(element => element.Name != "Movimenti"));
        var movement = employee.Element("Movimenti")!.Elements("Movimento").First();
        Assert.Equal(
            accepted.Root.Descendants("Movimento").First().Elements().Select(element => element.Name.LocalName),
            movement.Elements().Select(element => element.Name.LocalName));
        Assert.Equal("08", (string?)movement.Element("NumOre"));
    }
}
