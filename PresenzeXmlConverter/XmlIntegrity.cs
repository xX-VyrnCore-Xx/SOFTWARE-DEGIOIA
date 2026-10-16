using System.Globalization;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace PresenzeXmlConverter;

/// <summary>Lettura sicura dei file XML: niente DTD, niente risoluzione di risorse esterne, dimensione limitata.</summary>
internal static class XmlSafety
{
    /// <summary>Oltre questo numero di caratteri il file non è un tracciato di presenze: si rifiuta invece di riempire la memoria.</summary>
    public const long MaximumCharacters = 512L * 1024 * 1024;

    public static XmlReaderSettings ReaderSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersFromEntities = 0,
        MaxCharactersInDocument = MaximumCharacters
    };

    public static XDocument Load(Stream stream)
    {
        using var reader = XmlReader.Create(stream, ReaderSettings());
        return XDocument.Load(reader, LoadOptions.SetLineInfo);
    }

    public static XDocument Load(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Load(stream);
    }
}

/// <summary>
/// Controllo «le ore scritte nel file sono quelle volute»: rilegge i byte del file e confronta, per dipendente, giorno e
/// giustificativo, i minuti scritti con quelli attesi dai movimenti. Chiude il cerchio del validatore del tracciato, che
/// controlla la forma ma non sa quali numeri dovevano esserci.
/// </summary>
internal static class TrriPwXmlVerifier
{
    private const int MaximumReported = 12;

    /// <summary>Minuti scritti nel file per ogni dipendente/giorno/giustificativo (somma se compare più volte).</summary>
    public static Dictionary<(string Employee, DateTime Date, string Reason), int> ReadTotals(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var document = XmlSafety.Load(stream);
        var totals = new Dictionary<(string, DateTime, string), int>();
        foreach (var employee in document.Descendants().Where(element => element.Name.LocalName == "Dipendente"))
        {
            var code = ((string?)employee.Attribute("CodDipendenteUfficiale") ??
                        (string?)employee.Elements().FirstOrDefault(e => e.Name.LocalName == "CodDipendenteUfficiale") ??
                        string.Empty).Trim().ToUpperInvariant();
            // Un movimento è qualunque nodo sotto il dipendente che ha il figlio «Data».
            foreach (var movement in employee.Descendants().Where(e => e.Elements().Any(c => c.Name.LocalName == "Data")))
            {
                string Child(string name) =>
                    ((string?)movement.Elements().FirstOrDefault(c => c.Name.LocalName == name) ?? string.Empty).Trim();

                if (!DateTime.TryParseExact(Child("Data"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    continue;
                }
                _ = int.TryParse(Child("NumOre"), NumberStyles.None, CultureInfo.InvariantCulture, out var hours);
                _ = int.TryParse(Child("NumMinuti"), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes);
                var reason = Child("CodGiustificativoUfficiale").ToUpperInvariant();
                var key = (code, date.Date, reason);
                totals[key] = totals.GetValueOrDefault(key) + (hours * 60) + minutes;
            }
        }
        return totals;
    }

    /// <summary>Minuti attesi dai movimenti già normalizzati (stessa chiave del file).</summary>
    public static Dictionary<(string Employee, DateTime Date, string Reason), int> ExpectedTotals(IEnumerable<DailyMovement> movements)
    {
        var totals = new Dictionary<(string, DateTime, string), int>();
        foreach (var movement in movements)
        {
            var key = (
                movement.EmployeeCode.Trim().ToUpperInvariant(),
                movement.Date.Date,
                (movement.OfficialCode ?? movement.ReasonCode).Trim().ToUpperInvariant());
            totals[key] = totals.GetValueOrDefault(key) + (movement.Hours * 60) + movement.Minutes;
        }
        return totals;
    }

    /// <summary>Differenze fra ciò che ci si aspettava e ciò che è nel file; vuoto = il file contiene esattamente quelle ore.</summary>
    public static IReadOnlyList<string> Compare(IEnumerable<DailyMovement> expectedMovements, byte[] bytes)
    {
        Dictionary<(string Employee, DateTime Date, string Reason), int> actual;
        try
        {
            actual = ReadTotals(bytes);
        }
        catch (XmlException exception)
        {
            return [$"Il file non si può rileggere per il confronto: {exception.Message}"];
        }

        var expected = ExpectedTotals(expectedMovements);
        var problems = new List<string>();
        foreach (var (key, minutes) in expected.OrderBy(pair => pair.Key.Employee, StringComparer.Ordinal).ThenBy(pair => pair.Key.Date))
        {
            var found = actual.GetValueOrDefault(key, -1);
            if (found < 0)
            {
                problems.Add($"Manca nel file: dipendente {key.Employee}, {key.Date:dd/MM/yyyy}, giustificativo {key.Reason} ({Describe(minutes)}).");
            }
            else if (found != minutes)
            {
                problems.Add(
                    $"Ore diverse per il dipendente {key.Employee}, {key.Date:dd/MM/yyyy}, giustificativo {key.Reason}: " +
                    $"nel file {Describe(found)}, attese {Describe(minutes)}.");
            }
        }
        foreach (var (key, minutes) in actual.Where(pair => !expected.ContainsKey(pair.Key))
                     .OrderBy(pair => pair.Key.Employee, StringComparer.Ordinal).ThenBy(pair => pair.Key.Date))
        {
            problems.Add($"In più nel file: dipendente {key.Employee}, {key.Date:dd/MM/yyyy}, giustificativo {key.Reason} ({Describe(minutes)}).");
        }

        if (problems.Count > MaximumReported)
        {
            var rest = problems.Count - MaximumReported;
            problems = problems.Take(MaximumReported).ToList();
            problems.Add($"… e altre {rest} differenze.");
        }
        return problems;
    }

    private static string Describe(int minutes) => $"{minutes / 60} h {minutes % 60:00} min";
}

internal enum FileIntegrity
{
    /// <summary>Il file su disco è identico a quello consegnato.</summary>
    Identical,

    /// <summary>Il file su disco è diverso da quello consegnato.</summary>
    Modified,

    /// <summary>Il file non c'è più (spostato o cancellato).</summary>
    Missing,

    /// <summary>Lo storico non conserva la copia: non si può confrontare.</summary>
    NoCopy
}

/// <summary>Verifica che un file consegnato non sia stato modificato dopo l'esportazione, confrontandolo con la copia dello storico.</summary>
internal static class ExportIntegrity
{
    public static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static FileIntegrity Check(byte[]? stored, byte[]? current)
    {
        if (stored is null)
        {
            return FileIntegrity.NoCopy;
        }
        if (current is null)
        {
            return FileIntegrity.Missing;
        }
        return stored.AsSpan().SequenceEqual(current) ? FileIntegrity.Identical : FileIntegrity.Modified;
    }

    /// <summary>Messaggio per l'utente, con l'impronta SHA-256 quando serve a confrontare il file con altre copie.</summary>
    public static string Describe(FileIntegrity result, string fileName, byte[]? current = null) => result switch
    {
        FileIntegrity.Identical => $"«{fileName}» è identico a quello consegnato.{Fingerprint(current)}",
        FileIntegrity.Modified => $"«{fileName}» è diverso da quello consegnato: è stato modificato dopo l'esportazione.{Fingerprint(current)}",
        FileIntegrity.Missing => $"«{fileName}» non si trova più nel percorso salvato (spostato o cancellato). Puoi risalvarlo con «Salva copia».",
        _ => $"Lo storico non conserva la copia di «{fileName}»: non si può confrontare."
    };

    private static string Fingerprint(byte[]? current) =>
        current is null ? string.Empty : $"{Environment.NewLine}Impronta SHA-256: {Sha256Hex(current)}";
}
