using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;

namespace PresenzeXmlConverter;

/// <summary>Esito della lettura del codice in una pagina.</summary>
internal enum PdfCodeStatus
{
    /// <summary>Un solo codice nel piè di pagina e della stessa forma degli altri.</summary>
    Ok,

    /// <summary>Codice trovato ma da controllare (più candidati, forma diversa, doppio, fuori dal piè di pagina).</summary>
    Check,

    /// <summary>Nessun codice: va scritto a mano o la pagina saltata.</summary>
    Missing
}

/// <summary>Una pagina di un PDF con il codice cliente letto e il nome che avrà il file.</summary>
internal sealed class PdfRenameItem
{
    public PdfRenameItem(string sourceName, int page, byte[] data, string pageText, string? code, PdfCodeStatus status, string note)
    {
        SourceName = sourceName;
        Page = page;
        Data = data;
        PageText = pageText;
        Code = code;
        Status = status;
        Note = note;
    }

    public string SourceName { get; }

    public int Page { get; }

    public byte[] Data { get; }

    /// <summary>Il testo della pagina, per confrontarlo con quello del file scritto.</summary>
    public string PageText { get; }

    /// <summary>Il codice letto dal programma (null se non trovato).</summary>
    public string? Code { get; }

    /// <summary>Il codice che si userà come nome: parte da quello letto, l'utente lo può correggere.</summary>
    public string? FinalCode { get; set; }

    public PdfCodeStatus Status { get; set; }

    public string Note { get; set; }

    /// <summary>Il nome del file di uscita, deciso dopo i controlli (doppioni numerati).</summary>
    public string? FileName { get; set; }
}

/// <summary>
/// Trova nel piè di pagina il codice interno del cliente: cifre separate da trattini (per esempio 123-456-7).
/// Sceglie il codice sulla riga più in basso; se quella riga ne ha più di uno non indovina, lo segnala.
/// </summary>
internal static class PdfCodeFinder
{
    private static readonly Regex CodePattern = new(@"(?<![\d-])\d+(?:\s*-\s*\d+)+(?![\d-])", RegexOptions.Compiled);

    /// <summary>La parte bassa della pagina dove si cerca per prima il codice.</summary>
    private const double FooterFraction = 0.2;

    public static (string? Code, PdfCodeStatus Status, string Note) Find(Page page)
    {
        var words = page.GetWords().Where(word => !string.IsNullOrWhiteSpace(word.Text)).ToList();
        if (words.Count == 0)
        {
            return (null, PdfCodeStatus.Missing, "Pagina senza testo leggibile (forse una scansione).");
        }
        var bottom = page.CropBox.Bounds.Bottom;
        var footerTop = bottom + (page.Height * FooterFraction);
        var lines = Group(words);

        foreach (var (limit, inFooter) in new[] { (footerTop, true), (double.MaxValue, false) })
        {
            foreach (var line in lines.Where(line => line.Bottom <= limit))
            {
                var codes = CodePattern.Matches(line.Text)
                    .Select(match => Normalize(match.Value))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (codes.Count == 0)
                {
                    continue;
                }
                if (codes.Count > 1)
                {
                    return (null, PdfCodeStatus.Check, $"Più codici sulla stessa riga ({string.Join(", ", codes)}): scegli quello giusto.");
                }
                return inFooter
                    ? (codes[0], PdfCodeStatus.Ok, string.Empty)
                    : (codes[0], PdfCodeStatus.Check, "Codice trovato fuori dal piè di pagina.");
            }
        }
        return (null, PdfCodeStatus.Missing, "Nessun codice con i trattini nella pagina.");
    }

    public static string Normalize(string code) => Regex.Replace(code, @"\s+", string.Empty);

    /// <summary>La «forma» del codice: quante cifre ha ogni gruppo (123-45 diventa 3-2).</summary>
    public static string Shape(string code) => string.Join("-", code.Split('-').Select(group => group.Length));

    private sealed record Line(double Bottom, string Text);

    /// <summary>Raggruppa le parole in righe (stessa altezza, entro qualche punto) dalla più bassa alla più alta.</summary>
    private static List<Line> Group(List<Word> words)
    {
        var rows = new List<(double Bottom, List<Word> Words)>();
        foreach (var word in words.OrderBy(word => word.BoundingBox.Bottom))
        {
            var row = rows.FindIndex(candidate => Math.Abs(candidate.Bottom - word.BoundingBox.Bottom) <= 3);
            if (row < 0)
            {
                rows.Add((word.BoundingBox.Bottom, new List<Word> { word }));
            }
            else
            {
                rows[row].Words.Add(word);
            }
        }
        return rows
            .OrderBy(row => row.Bottom)
            .Select(row => new Line(row.Bottom, string.Join(" ", row.Words.OrderBy(word => word.BoundingBox.Left).Select(word => word.Text))))
            .ToList();
    }
}

/// <summary>Costruisce il piano di rinomina: una voce per pagina, con i controlli di coerenza.</summary>
internal static class PdfRenamePlanner
{
    /// <summary>Legge i PDF (anche dentro un file ZIP) in coppie nome/contenuto, in ordine alfabetico.</summary>
    public static List<(string Name, byte[] Data)> LoadFiles(IEnumerable<string> paths)
    {
        var files = new List<(string Name, byte[] Data)>();
        foreach (var path in paths)
        {
            if (string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var archive = ZipFile.OpenRead(path);
                foreach (var entry in archive.Entries)
                {
                    var name = entry.FullName;
                    if (entry.Length == 0 ||
                        name.StartsWith("__MACOSX/", StringComparison.Ordinal) ||
                        Path.GetFileName(name).StartsWith("._", StringComparison.Ordinal) ||
                        !name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    using var stream = entry.Open();
                    using var memory = new MemoryStream();
                    stream.CopyTo(memory);
                    files.Add((name, memory.ToArray()));
                }
            }
            else if (string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                files.Add((Path.GetFileName(path), File.ReadAllBytes(path)));
            }
        }
        return files.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Legge ogni pagina di ogni PDF. I file illeggibili finiscono in <paramref name="unreadable"/>.</summary>
    public static List<PdfRenameItem> Build(IEnumerable<(string Name, byte[] Data)> files, List<string> unreadable)
    {
        var items = new List<PdfRenameItem>();
        foreach (var (name, data) in files)
        {
            try
            {
                using var document = PdfDocument.Open(data);
                foreach (var page in document.GetPages())
                {
                    var (code, status, note) = PdfCodeFinder.Find(page);
                    var item = new PdfRenameItem(name, page.Number, data, PageText(page), code, status, note) { FinalCode = code };
                    items.Add(item);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                unreadable.Add($"{name}: {exception.Message}");
            }
        }
        Recheck(items);
        return items;
    }

    /// <summary>
    /// Rifà i controlli sull'insieme (da richiamare dopo ogni correzione a mano): forma diversa dalla maggioranza,
    /// codici ripetuti (il secondo prende «(2)», «(3)»…) e nomi non validi. Stabilisce il nome di ogni file.
    /// </summary>
    public static void Recheck(List<PdfRenameItem> items)
    {
        var shapes = items
            .Where(item => !string.IsNullOrWhiteSpace(item.FinalCode))
            .GroupBy(item => PdfCodeFinder.Shape(item.FinalCode!))
            .OrderByDescending(group => group.Count())
            .ToList();
        var common = shapes.Count > 0 && items.Count >= 3 && shapes[0].Count() * 10 >= items.Count(item => !string.IsNullOrWhiteSpace(item.FinalCode)) * 6
            ? shapes[0].Key
            : null;

        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            item.FileName = null;
            var code = item.FinalCode?.Trim();
            if (string.IsNullOrEmpty(code))
            {
                item.Status = PdfCodeStatus.Missing;
                item.Note = string.IsNullOrEmpty(item.Note) ? "Codice mancante." : item.Note;
                continue;
            }
            if (code.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                item.Status = PdfCodeStatus.Missing;
                item.Note = "Il codice contiene caratteri non ammessi in un nome di file.";
                continue;
            }
            var edited = !string.Equals(code, item.Code, StringComparison.Ordinal);
            var status = edited ? PdfCodeStatus.Ok : item.Status;
            var note = edited ? "Corretto a mano." : item.Note;
            if (!edited && common is not null && PdfCodeFinder.Shape(code) != common)
            {
                status = PdfCodeStatus.Check;
                note = Join(note, $"Forma diversa dagli altri codici ({common.Replace('-', '·')}).");
            }
            used.TryGetValue(code, out var seen);
            used[code] = seen + 1;
            if (seen > 0)
            {
                status = PdfCodeStatus.Check;
                note = Join(note, $"Codice già usato da un'altra pagina: il file si chiamerà «{code} ({seen + 1})».");
                item.FileName = $"{code} ({seen + 1}).pdf";
            }
            else
            {
                item.FileName = code + ".pdf";
            }
            item.Status = status;
            item.Note = note;
        }
        // Il primo di un gruppo di codici uguali va controllato come gli altri.
        foreach (var group in items.Where(item => item.FileName is not null).GroupBy(item => item.FinalCode!.Trim(), StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
        {
            var first = group.First();
            first.Status = PdfCodeStatus.Check;
            first.Note = Join(first.Note, "Lo stesso codice compare su più pagine.");
        }
    }

    private static string Join(string first, string second) => string.IsNullOrEmpty(first) ? second : first + " " + second;

    internal static string PageText(Page page) =>
        Regex.Replace(string.Join(" ", page.GetWords().Select(word => word.Text)), @"\s+", " ").Trim();
}

/// <summary>Scrive lo ZIP con una pagina per file e lo riesamina: ogni file deve avere una pagina, il suo codice e il suo testo.</summary>
internal static class PdfRenameWriter
{
    /// <summary>Scrive i file con un nome (le pagine senza codice restano fuori). Restituisce quanti ne ha scritti.</summary>
    public static int WriteZip(IReadOnlyList<PdfRenameItem> items, string zipPath)
    {
        var count = 0;
        var tempPath = zipPath + ".tmp";
        try
        {
            using (var stream = File.Create(tempPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var documents = new Dictionary<byte[], PdfDocument>();
                try
                {
                    foreach (var item in items.Where(item => item.FileName is not null))
                    {
                        if (!documents.TryGetValue(item.Data, out var document))
                        {
                            document = PdfDocument.Open(item.Data);
                            documents[item.Data] = document;
                        }
                        var builder = new PdfDocumentBuilder();
                        builder.AddPage(document, item.Page);
                        var bytes = builder.Build();
                        var entry = archive.CreateEntry(item.FileName!, CompressionLevel.Optimal);
                        using var entryStream = entry.Open();
                        entryStream.Write(bytes, 0, bytes.Length);
                        count++;
                    }
                }
                finally
                {
                    foreach (var document in documents.Values)
                    {
                        document.Dispose();
                    }
                }
            }
            File.Move(tempPath, zipPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        return count;
    }

    /// <summary>Riapre lo ZIP scritto e confronta ogni file con la pagina da cui viene. Elenco vuoto = tutto giusto.</summary>
    public static List<string> Verify(IReadOnlyList<PdfRenameItem> items, string zipPath)
    {
        var problems = new List<string>();
        var expected = items.Where(item => item.FileName is not null).ToList();
        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count != expected.Count)
        {
            problems.Add($"Nello ZIP ci sono {archive.Entries.Count} file, ne erano previsti {expected.Count}.");
        }
        foreach (var item in expected)
        {
            var entry = archive.GetEntry(item.FileName!);
            if (entry is null)
            {
                problems.Add($"Manca {item.FileName}.");
                continue;
            }
            try
            {
                using var stream = entry.Open();
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                using var document = PdfDocument.Open(memory.ToArray());
                if (document.NumberOfPages != 1)
                {
                    problems.Add($"{item.FileName} ha {document.NumberOfPages} pagine invece di una.");
                    continue;
                }
                var text = PdfRenamePlanner.PageText(document.GetPage(1));
                if (!string.Equals(text, item.PageText, StringComparison.Ordinal))
                {
                    problems.Add($"{item.FileName}: il testo della pagina non coincide con l'originale ({item.SourceName}, pagina {item.Page}).");
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                problems.Add($"{item.FileName}: non si riapre ({exception.Message}).");
            }
        }
        return problems;
    }
}
