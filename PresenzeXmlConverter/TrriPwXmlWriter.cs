using System.Globalization;
using System.Text;
using System.Xml;

namespace PresenzeXmlConverter;

internal static class TrriPwXmlWriter
{
    public static void SaveFile(string path, string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(xml);
        ReplaceAtomically(path, stream =>
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(xml);
            stream.Write(bytes);
        });
    }

    /// <summary>
    /// Scrive il tracciato direttamente su disco in streaming: per file molto grandi
    /// evita di tenere in memoria sia la stringa XML sia il buffer dei byte.
    /// </summary>
    public static void SaveFile(string path, string companyCode, IReadOnlyList<DailyMovement> movements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var (company, normalizedMovements) = Validate(companyCode, movements);
        ReplaceAtomically(path, stream =>
        {
            using (var writer = CreateWriter(stream))
            {
                WriteDocument(writer, company, normalizedMovements);
            }
        });
    }

    private static void ReplaceAtomically(string path, Action<Stream> write)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Percorso del file XML non valido.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       16 * 1024,
                       FileOptions.WriteThrough))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        catch (Exception operationException)
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception cleanupException) when (
                cleanupException is IOException or UnauthorizedAccessException)
            {
                throw new AggregateException(
                    "Il salvataggio XML non è riuscito e non è stato possibile rimuovere il file temporaneo.",
                    operationException,
                    cleanupException);
            }

            throw;
        }
    }

    public static string Create(string companyCode, IReadOnlyList<DailyMovement> movements)
    {
        var (company, normalizedMovements) = Validate(companyCode, movements);

        using var output = new MemoryStream();
        using (var writer = CreateWriter(output))
        {
            WriteDocument(writer, company, normalizedMovements);
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static (string Company, DailyMovement[] Movements) Validate(
        string companyCode,
        IReadOnlyList<DailyMovement> movements)
    {
        ArgumentNullException.ThrowIfNull(movements);
        companyCode = ValidateCode(companyCode, 6, "codice azienda");
        if (movements.Count == 0)
        {
            throw new InvalidDataException("Non è possibile generare un XML senza movimenti.");
        }
        if (movements.Count > 250_000)
        {
            throw new InvalidDataException("Il numero di movimenti supera il limite di 250.000 record per file XML.");
        }

        var normalizedMovements = new DailyMovement[movements.Count];
        for (var index = 0; index < movements.Count; index++)
        {
            var movement = movements[index];
            ArgumentNullException.ThrowIfNull(movement);
            normalizedMovements[index] = movement with
            {
                EmployeeCode = ValidateCode(movement.EmployeeCode, 7, "codice dipendente"),
                ReasonCode = ValidateCode(movement.ReasonCode, 3, "codice giustificativo"),
                EmployeeName = movement.EmployeeName?.Trim() ?? string.Empty
            };
        }
        ValidateEmployeeCodes(normalizedMovements);
        if (normalizedMovements.Any(movement =>
                movement.Hours is < 0 or > 99 ||
                movement.Minutes is < 0 or > 59))
        {
            throw new InvalidDataException("Ore e minuti devono rientrare nei limiti del tracciato TRRIPW.");
        }
        return (companyCode, normalizedMovements);
    }

    private static XmlWriter CreateWriter(Stream output) => XmlWriter.Create(output, new XmlWriterSettings
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        Indent = true,
        OmitXmlDeclaration = false,
        NewLineChars = "\r\n",
        CheckCharacters = true
    });

    private static void WriteDocument(XmlWriter writer, string companyCode, IReadOnlyList<DailyMovement> normalizedMovements)
    {
        writer.WriteStartDocument();
        writer.WriteStartElement("TRRIPW");
        foreach (var employeeGroup in normalizedMovements
                     .GroupBy(movement => movement.EmployeeCode, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteStartElement("Dipendente");
            writer.WriteAttributeString("GenerazioneAutomaticaDaTeorico", "N");
            WriteElement(writer, "CodAziendaUfficiale", companyCode);
            WriteElement(writer, "CodDipendenteUfficiale", employeeGroup.Key);
            writer.WriteStartElement("Movimenti");
            foreach (var movement in employeeGroup
                         .OrderBy(movement => movement.Date.Date)
                         .ThenBy(movement => movement.ReasonCode, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(movement => movement.SourceRow))
            {
                writer.WriteStartElement("Movimento");
                WriteElement(writer, "CodGiustificativoUfficiale", movement.ReasonCode);
                WriteElement(writer, "Data", movement.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                WriteElement(writer, "NumOre", movement.Hours.ToString(CultureInfo.InvariantCulture));
                WriteElement(writer, "NumMinuti", movement.Minutes.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static string ValidateCode(string? value, int maximumLength, string fieldName)
    {
        var code = value?.Trim() ?? string.Empty;
        if (code.Length is 0 || code.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"Il {fieldName} deve contenere da 1 a {maximumLength} caratteri.");
        }

        try
        {
            XmlConvert.VerifyXmlChars(code);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException($"Il {fieldName} contiene caratteri non validi per XML.", exception);
        }
        return code;
    }

    private static void ValidateEmployeeCodes(IReadOnlyList<DailyMovement> movements)
    {
        if (movements.Any(movement => string.IsNullOrWhiteSpace(movement.EmployeeName)))
        {
            throw new InvalidDataException("Un movimento non è associato a un nominativo dipendente valido.");
        }

        var duplicateCode = movements
            .GroupBy(movement => movement.EmployeeCode, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group
                .Select(movement => movement.EmployeeName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1);
        if (duplicateCode is not null)
        {
            throw new InvalidDataException(
                $"Il codice dipendente “{duplicateCode.Key}” è associato a più nominativi.");
        }

        var conflictingName = movements
            .GroupBy(movement => movement.EmployeeName.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group
                .Select(movement => movement.EmployeeCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1);
        if (conflictingName is not null)
        {
            throw new InvalidDataException(
                $"Il dipendente “{conflictingName.Key}” è associato a più codici PAGHE.");
        }
    }

    private static void WriteElement(XmlWriter writer, string name, string value)
    {
        writer.WriteElementString(name, value);
    }
}
