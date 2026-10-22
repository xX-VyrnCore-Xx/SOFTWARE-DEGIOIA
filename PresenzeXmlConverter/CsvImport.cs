using System.Text;
using ClosedXML.Excel;

namespace PresenzeXmlConverter;

/// <summary>
/// Lettura di un file CSV o TSV (esportato da un gestionale o da Excel) come se fosse un foglio: stesso riconoscimento
/// delle colonne, stesse regole di lettura di date e ore. Separatore e codifica si riconoscono dal contenuto
/// («;» di Excel italiano, «,», tabulazione o «|»; UTF-8 oppure Windows). Solo testo: nessuna formula né macro.
/// </summary>
internal static class CsvImport
{
    public const string SheetName = "CSV";

    /// <summary>Stessi limiti del foglio Excel, così un CSV enorme non esaurisce la memoria.</summary>
    private const long MaximumBytes = 50L * 1024 * 1024;
    private const int MaximumRows = 100_000;
    private const int MaximumColumns = 256;
    private static readonly char[] Delimiters = [';', ',', '\t', '|'];

    public static readonly string[] Extensions = [".csv", ".tsv"];

    public static bool IsCsv(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        Extensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    /// <summary>Legge il file in righe di celle di testo.</summary>
    public static IReadOnlyList<string[]> ReadFile(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("Il file selezionato non esiste.", path);
        }
        if (file.Length is 0 or > MaximumBytes)
        {
            throw new InvalidDataException("Il file CSV è vuoto o supera il limite di 50 MB.");
        }
        return Parse(Decode(File.ReadAllBytes(path)));
    }

    /// <summary>UTF-8 (con o senza BOM) se il contenuto lo è davvero; altrimenti Windows-1252 (Latin-1), tipico di Excel italiano.</summary>
    public static string Decode(byte[] bytes)
    {
        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes, offset, bytes.Length - offset);
        }
    }

    /// <summary>Il separatore più usato nelle prime righe (fuori dalle virgolette); «;» se non si capisce.</summary>
    public static char DetectDelimiter(string text)
    {
        var best = ';';
        var bestScore = 0;
        foreach (var delimiter in Delimiters)
        {
            var counts = new List<int>();
            var inQuotes = false;
            var count = 0;
            var lines = 0;
            foreach (var character in text)
            {
                if (character == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes && character == delimiter)
                {
                    count++;
                }
                else if (!inQuotes && character == '\n')
                {
                    if (count > 0)
                    {
                        counts.Add(count);
                    }
                    count = 0;
                    if (++lines == 8)
                    {
                        break;
                    }
                }
            }
            if (count > 0 && lines < 8)
            {
                counts.Add(count);
            }
            if (counts.Count == 0)
            {
                continue;
            }
            // Meglio chi compare in più righe e con lo stesso numero di volte (struttura regolare).
            var mostCommon = counts.GroupBy(value => value).OrderByDescending(group => group.Count()).First();
            var score = mostCommon.Count() * 1000 + mostCommon.Key;
            if (score > bestScore)
            {
                bestScore = score;
                best = delimiter;
            }
        }
        return best;
    }

    /// <summary>Divide il testo in righe e celle, con virgolette, virgolette raddoppiate e a capo dentro le celle.</summary>
    public static IReadOnlyList<string[]> Parse(string text)
    {
        var delimiter = DetectDelimiter(text);
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;
        void EndCell()
        {
            row.Add(cell.ToString().Trim());
            cell.Clear();
        }
        void EndRow()
        {
            EndCell();
            if (row.Any(value => value.Length > 0))
            {
                if (row.Count > MaximumColumns)
                {
                    throw new InvalidDataException($"Il file CSV supera il limite di {MaximumColumns} colonne.");
                }
                rows.Add(row.ToArray());
                if (rows.Count > MaximumRows)
                {
                    throw new InvalidDataException($"Il file CSV supera il limite di {MaximumRows:N0} righe.");
                }
            }
            row = new List<string>();
        }

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        cell.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(character);
                }
            }
            else if (character == '"' && cell.Length == 0)
            {
                inQuotes = true;
            }
            else if (character == delimiter)
            {
                EndCell();
            }
            else if (character == '\r')
            {
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }
                EndRow();
            }
            else if (character == '\n')
            {
                EndRow();
            }
            else
            {
                cell.Append(character);
            }
        }
        if (cell.Length > 0 || row.Count > 0)
        {
            EndRow();
        }
        return rows;
    }

    /// <summary>
    /// Costruisce in memoria un foglio con il contenuto del CSV (tutto come testo: date e numeri si leggono poi con le
    /// stesse regole italiane del foglio Excel, ad esempio «1.234,5» e «03/03/2026»).
    /// </summary>
    public static XLWorkbook ToWorkbook(string path)
    {
        var rows = ReadFile(path);
        if (rows.Count < 2)
        {
            throw new InvalidDataException("Il file CSV deve avere almeno una riga di intestazioni e una di dati.");
        }
        var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var values = rows[rowIndex];
            for (var column = 0; column < values.Length; column++)
            {
                if (values[column].Length > 0)
                {
                    sheet.Cell(rowIndex + 1, column + 1).Value = values[column];
                }
            }
        }
        return workbook;
    }
}
