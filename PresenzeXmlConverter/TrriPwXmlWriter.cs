using System.Globalization;
using System.Text;
using System.Xml;

namespace PresenzeXmlConverter;

internal static class TrriPwXmlWriter
{
    /* --------------------- Limiti del tracciato TRRIPW --------------------- *
     * Il documento Zucchetti (Paghe Infinity, "Interfaccia rilevazione
     * presenze - tracciati record") fissa la lunghezza di ogni campo: il codice
     * azienda è di 6 caratteri, quello dipendente di 7, il giustificativo di 3,
     * e ore e minuti sono campi numerici di 2 cifre. I limiti stanno qui, in un
     * unico posto, perché la generazione e la scrittura dell'XML non possano
     * usare valori diversi da quelli dichiarati nel tracciato. */

    /// <summary>
    /// Elemento radice del documento. «TRRIPW» è il nome del tracciato e del file, NON
    /// dell'elemento: Zucchetti lo respinge con «cvc-elt.1: impossibile trovare la
    /// dichiarazione dell'elemento "TRRIPW"». Il tracciato descrive il nodo
    /// «Dipendente» come testata di ogni serie di dati e chiama «Dipendenti» il nodo
    /// che li raccoglie, come per «Commesse» e «Movimenti»: la radice è quel contenitore.
    /// </summary>
    public const string RootElement = "Dipendenti";

    /// <summary>CodAziendaUfficiale: fino a 6 caratteri.</summary>
    public const int MaxCompanyCodeLength = 6;

    /// <summary>CodDipendenteUfficiale: fino a 7 caratteri.</summary>
    public const int MaxEmployeeCodeLength = 7;

    /// <summary>CodGiustificativoUfficiale: fino a 3 caratteri.</summary>
    public const int MaxReasonCodeLength = 3;

    /// <summary>NumOre: campo numerico di 2 cifre (0-99).</summary>
    public const int MaxHoursPerMovement = 99;

    /// <summary>NumMinuti: campo numerico di 2 cifre (0-59).</summary>
    public const int MaxMinutesPerMovement = 59;

    /// <summary>Numero massimo di movimenti in un singolo file.</summary>
    public const int MaxMovementsPerFile = 250_000;

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
    public static void SaveFile(string path, string companyCode, IReadOnlyList<DailyMovement> movements, XmlFormat? format = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        format ??= XmlFormat.Active;
        var (company, normalizedMovements) = Validate(companyCode, movements);
        ReplaceAtomically(path, stream =>
        {
            using (var writer = CreateWriter(stream))
            {
                WriteDocument(writer, company, normalizedMovements, format);
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

    public static string Create(string companyCode, IReadOnlyList<DailyMovement> movements, XmlFormat? format = null)
    {
        format ??= XmlFormat.Active;
        var (company, normalizedMovements) = Validate(companyCode, movements);

        using var output = new MemoryStream();
        using (var writer = CreateWriter(output))
        {
            WriteDocument(writer, company, normalizedMovements, format);
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static (string Company, DailyMovement[] Movements) Validate(
        string companyCode,
        IReadOnlyList<DailyMovement> movements)
    {
        ArgumentNullException.ThrowIfNull(movements);
        companyCode = ValidateCode(companyCode, MaxCompanyCodeLength, "codice azienda");
        if (movements.Count == 0)
        {
            throw new InvalidDataException("Non è possibile generare un XML senza movimenti.");
        }
        if (movements.Count > MaxMovementsPerFile)
        {
            throw new InvalidDataException(
                $"Il numero di movimenti ({movements.Count:N0}) supera il limite di " +
                $"{MaxMovementsPerFile:N0} record per file XML.");
        }

        var normalizedMovements = new DailyMovement[movements.Count];
        for (var index = 0; index < movements.Count; index++)
        {
            var movement = movements[index];
            ArgumentNullException.ThrowIfNull(movement);
            // Il nominativo si ripulisce prima di comporre i messaggi: un errore
            // sui codici deve poter dire a chi appartiene la riga, non solo che
            // "un codice" è lungo. Il codice dipendente è quello che l'utente
            // corregge in griglia, quindi il messaggio lo identifica per nome.
            var employeeName = movement.EmployeeName?.Trim() ?? string.Empty;
            normalizedMovements[index] = movement with
            {
                EmployeeName = employeeName,
                EmployeeCode = ValidateCode(
                    movement.EmployeeCode,
                    MaxEmployeeCodeLength,
                    Describe("codice PAGHE", employeeName)),
                ReasonCode = ValidateCode(
                    movement.ReasonCode,
                    MaxReasonCodeLength,
                    Describe($"codice giustificativo della richiesta “{movement.Policy}”", employeeName))
            };
        }
        ValidateEmployeeCodes(normalizedMovements);
        var outOfRange = normalizedMovements.FirstOrDefault(movement =>
            movement.Hours is < 0 or > MaxHoursPerMovement ||
            movement.Minutes is < 0 or > MaxMinutesPerMovement);
        if (outOfRange is not null)
        {
            var subject = outOfRange.EmployeeName.Length == 0
                ? "un movimento"
                : $"“{outOfRange.EmployeeName}”";
            throw new InvalidDataException(
                $"Ore e minuti di {subject} ({outOfRange.Date:dd/MM/yyyy}) " +
                $"non rientrano nei limiti del tracciato TRRIPW " +
                $"(ore 0-{MaxHoursPerMovement}, minuti 0-{MaxMinutesPerMovement}).");
        }
        return (companyCode, MergeSameDayMovements(normalizedMovements));
    }

    /// <summary>
    /// Il tracciato descrive «una singola movimentazione giornaliera» per giorno e
    /// giustificativo: due righe dello stesso dipendente con la stessa data e lo stesso
    /// giustificativo (per esempio due richieste di ferie sovrapposte) diventano un solo
    /// movimento con le ore sommate, invece di due nodi identici che PAGHE può scartare
    /// o contare male.
    /// </summary>
    internal static DailyMovement[] MergeSameDayMovements(IReadOnlyList<DailyMovement> movements)
    {
        var merged = new List<DailyMovement>(movements.Count);
        foreach (var group in movements.GroupBy(movement => (
                     movement.EmployeeCode.ToUpperInvariant(),
                     movement.Date.Date,
                     movement.ReasonCode.ToUpperInvariant())))
        {
            var first = group.OrderBy(movement => movement.SourceRow).First();
            if (group.Count() == 1)
            {
                merged.Add(first);
                continue;
            }
            var totalMinutes = group.Sum(movement => movement.Hours * 60 + movement.Minutes);
            var hours = totalMinutes / 60;
            if (hours > MaxHoursPerMovement)
            {
                throw new InvalidDataException(
                    $"Le ore di “{first.EmployeeName}” del {first.Date:dd/MM/yyyy} con il giustificativo " +
                    $"{first.ReasonCode} sommano oltre {MaxHoursPerMovement} ore: controlla le richieste sovrapposte.");
            }
            merged.Add(first with { Hours = hours, Minutes = totalMinutes % 60 });
        }
        return merged.ToArray();
    }

    private static XmlWriter CreateWriter(Stream output) => XmlWriter.Create(output, new XmlWriterSettings
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        Indent = true,
        OmitXmlDeclaration = false,
        NewLineChars = "\r\n",
        CheckCharacters = true
    });

    private static void WriteDocument(
        XmlWriter writer,
        string companyCode,
        IReadOnlyList<DailyMovement> normalizedMovements,
        XmlFormat format)
    {
        var ns = format.NamespaceOrNull;
        writer.WriteStartDocument();
        writer.WriteStartElement(format.RootElement, ns);
        foreach (var employeeGroup in normalizedMovements
                     .GroupBy(movement => movement.EmployeeCode, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteStartElement("Dipendente", ns);
            WriteElement(writer, "CodAziendaUfficiale", companyCode, ns);
            WriteElement(writer, "CodDipendenteUfficiale", employeeGroup.Key, ns);
            var ordered = employeeGroup
                .OrderBy(movement => movement.Date.Date)
                .ThenBy(movement => movement.ReasonCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(movement => movement.SourceRow)
                .ToArray();
            if (format.MovementPerNode)
            {
                // Un nodo «Movimenti» per ogni giorno: i campi del movimento sono suoi figli.
                foreach (var movement in ordered)
                {
                    writer.WriteStartElement("Movimenti", ns);
                    writer.WriteAttributeString("GenerazioneAutomaticaDaTeorico", "N");
                    WriteMovementFields(writer, movement, ns);
                    writer.WriteEndElement();
                }
            }
            else
            {
                writer.WriteStartElement("Movimenti", ns);
                // Il tracciato (Paghe Infinity, nodo "Movimenti" → "Attributi") definisce
                // GenerazioneAutomaticaDaTeorico su Movimenti, non su Dipendente: "N" =
                // leggi i movimenti giornalieri comunicati. Su Dipendente l'attributo non
                // è previsto e PAGHE potrebbe ignorarlo, applicando l'orario teorico.
                writer.WriteAttributeString("GenerazioneAutomaticaDaTeorico", "N");
                foreach (var movement in ordered)
                {
                    writer.WriteStartElement("Movimento", ns);
                    WriteMovementFields(writer, movement, ns);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteMovementFields(XmlWriter writer, DailyMovement movement, string? ns)
    {
        WriteElement(writer, "CodGiustificativoUfficiale", movement.ReasonCode, ns);
        WriteElement(writer, "Data", movement.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ns);
        WriteElement(writer, "NumOre", movement.Hours.ToString(CultureInfo.InvariantCulture), ns);
        WriteElement(writer, "NumMinuti", movement.Minutes.ToString(CultureInfo.InvariantCulture), ns);
    }

    /// <summary>
    /// Compone il nome del campo nei messaggi di errore, aggiungendo il
    /// dipendente quando è noto: "codice PAGHE di “Mario Rossi”".
    /// </summary>
    private static string Describe(string field, string employeeName) =>
        employeeName.Length == 0 ? field : $"{field} di “{employeeName}”";

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

    private static void WriteElement(XmlWriter writer, string name, string value, string? ns)
    {
        writer.WriteElementString(name, ns, value);
    }
}
