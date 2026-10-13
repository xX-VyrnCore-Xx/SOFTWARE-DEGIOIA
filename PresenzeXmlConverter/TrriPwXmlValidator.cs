using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace PresenzeXmlConverter;

/// <summary>
/// Controllo del file XML già scritto, fatto sui byte che vanno a Zucchetti e non sui
/// dati da cui nascono: rilegge il file e verifica le regole del tracciato TRRIPW
/// (Paghe Infinity), così un file troncato, con BOM o con un campo fuori misura viene
/// scoperto prima della consegna e non dall'importazione in PAGHE.
/// </summary>
internal static class TrriPwXmlValidator
{
    /// <summary>Elenco dei problemi trovati; vuoto = il file rispetta il tracciato.</summary>
    public static IReadOnlyList<string> ValidateFile(string path, XmlFormat? format = null)
    {
        try
        {
            return Validate(File.ReadAllBytes(path), format);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [$"Il file salvato non si può rileggere: {exception.Message}"];
        }
    }

    public static IReadOnlyList<string> Validate(byte[] bytes, XmlFormat? format = null)
    {
        format ??= XmlFormat.Active;
        var issues = new List<string>();
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            issues.Add("Il file inizia con il BOM UTF-8, che il tracciato non supporta.");
        }

        XDocument document;
        try
        {
            using var stream = new MemoryStream(bytes);
            document = XDocument.Load(stream);
        }
        catch (XmlException exception)
        {
            issues.Add($"XML non ben formato (riga {exception.LineNumber}): {exception.Message}");
            return issues;
        }

        var root = document.Root;
        if (root is null ||
            root.Name.LocalName != format.RootElement ||
            root.Name.NamespaceName != format.Namespace)
        {
            var expected = format.Namespace.Length == 0
                ? $"«{format.RootElement}»"
                : $"«{format.RootElement}» (spazio dei nomi {format.Namespace})";
            issues.Add($"L'elemento radice deve essere {expected}.");
            return issues;
        }

        var ns = root.Name.Namespace;
        var employees = root.Elements(ns + "Dipendente").ToArray();
        if (employees.Length == 0)
        {
            issues.Add("Manca il nodo «Dipendente»: senza, PAGHE non importa nulla.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var employee in employees)
        {
            var company = (string?)employee.Element(ns + "CodAziendaUfficiale") ?? string.Empty;
            var code = (string?)employee.Element(ns + "CodDipendenteUfficiale") ?? string.Empty;
            var who = code.Length == 0 ? "un dipendente" : $"il dipendente {code}";
            if (company.Length is 0 or > TrriPwXmlWriter.MaxCompanyCodeLength)
            {
                issues.Add($"Codice azienda mancante o oltre {TrriPwXmlWriter.MaxCompanyCodeLength} caratteri per {who}.");
            }
            if (code.Length is 0 or > TrriPwXmlWriter.MaxEmployeeCodeLength)
            {
                issues.Add($"Codice dipendente mancante o oltre {TrriPwXmlWriter.MaxEmployeeCodeLength} caratteri.");
            }
            if (employee.Attribute("GenerazioneAutomaticaDaTeorico") is not null)
            {
                issues.Add($"L'attributo GenerazioneAutomaticaDaTeorico va sul nodo «Movimenti», non su «Dipendente» ({who}).");
            }

            var movementNodes = employee.Elements(ns + "Movimenti").ToArray();
            if (movementNodes.Length == 0)
            {
                issues.Add($"Manca il nodo «Movimenti» per {who}.");
                continue;
            }
            foreach (var movements in movementNodes)
            {
                var automatic = (string?)movements.Attribute("GenerazioneAutomaticaDaTeorico");
                if (automatic is not null and not ("N" or "S" or "0" or "1"))
                {
                    issues.Add($"GenerazioneAutomaticaDaTeorico ammette S/N oppure 1/0 ({who}).");
                }
                if (automatic is "S" or "1")
                {
                    issues.Add($"GenerazioneAutomaticaDaTeorico è «{automatic}»: PAGHE scarterebbe i movimenti di {who}.");
                }

                // Il nodo «Movimenti» contiene i «Movimento» oppure è esso stesso il movimento
                // del giorno (un nodo per giorno): le due disposizioni si controllano uguale.
                var records = movements.Elements(ns + "Movimento").ToArray();
                if (records.Length == 0)
                {
                    records = [movements];
                }
                foreach (var movement in records)
                {
                    var reason = (string?)movement.Element(ns + "CodGiustificativoUfficiale") ?? string.Empty;
                    var dateText = (string?)movement.Element(ns + "Data") ?? string.Empty;
                    if (reason.Length is 0 or > TrriPwXmlWriter.MaxReasonCodeLength)
                    {
                        issues.Add($"Giustificativo mancante o oltre {TrriPwXmlWriter.MaxReasonCodeLength} caratteri per {who}.");
                    }
                    if (!DateTime.TryParseExact(
                            dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    {
                        issues.Add($"Data «{dateText}» non valida per {who}: il formato è aaaa-mm-gg.");
                    }
                    if (!TryNumber(movement.Element(ns + "NumOre"), TrriPwXmlWriter.MaxHoursPerMovement) ||
                        !TryNumber(movement.Element(ns + "NumMinuti"), TrriPwXmlWriter.MaxMinutesPerMovement))
                    {
                        issues.Add(
                            $"Ore o minuti non validi per {who} il {dateText} " +
                            $"(ore 0-{TrriPwXmlWriter.MaxHoursPerMovement}, minuti 0-{TrriPwXmlWriter.MaxMinutesPerMovement}).");
                    }
                    if (!seen.Add($"{company}|{code}|{dateText}|{reason}"))
                    {
                        issues.Add($"Movimento duplicato per {who} il {dateText} con il giustificativo {reason}.");
                    }
                }
            }
        }
        return issues;
    }

    private static bool TryNumber(XElement? element, int maximum) =>
        element is not null &&
        int.TryParse((string)element, NumberStyles.None, CultureInfo.InvariantCulture, out var value) &&
        value <= maximum;
}
