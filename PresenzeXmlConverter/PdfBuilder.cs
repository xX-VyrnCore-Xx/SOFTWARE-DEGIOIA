using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace PresenzeXmlConverter;

/// <summary>Colore RGB 0-255 per i disegni del PDF.</summary>
internal readonly record struct PdfColor(int R, int G, int B)
{
    public string Components => string.Create(
        CultureInfo.InvariantCulture,
        $"{R / 255.0:0.###} {G / 255.0:0.###} {B / 255.0:0.###}");
}

/// <summary>
/// Scrittore minimo di PDF (1.4) per i report: pagine A4, testo con i caratteri Helvetica standard (nessun font
/// da incorporare), righe e rettangoli. Niente librerie esterne: il file è semplice e il codice si può verificare
/// senza Windows. Le coordinate si danno dall'alto a sinistra, in punti (1/72 di pollice).
/// </summary>
internal sealed class PdfBuilder
{
    public const double A4Width = 595.28;
    public const double A4Height = 841.89;

    private readonly List<PdfPage> _pages = [];

    public string Title { get; set; } = "Documento";

    public IReadOnlyList<PdfPage> Pages => _pages;

    public PdfPage AddPage(double width = A4Width, double height = A4Height)
    {
        var page = new PdfPage(width, height);
        _pages.Add(page);
        return page;
    }

    public byte[] Build()
    {
        if (_pages.Count == 0)
        {
            AddPage();
        }

        using var output = new MemoryStream();
        var offsets = new List<long>();

        void Write(string text)
        {
            var bytes = Encoding.Latin1.GetBytes(text);
            output.Write(bytes, 0, bytes.Length);
        }

        void BeginObject(int number)
        {
            while (offsets.Count < number)
            {
                offsets.Add(0);
            }
            offsets[number - 1] = output.Position;
            Write($"{number} 0 obj\n");
        }

        // Oggetti fissi: 1 catalogo, 2 elenco delle pagine, 3 e 4 i font, 5 le informazioni. Poi due oggetti per pagina.
        Write("%PDF-1.4\n%âãÏÓ\n");
        BeginObject(1);
        Write("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        BeginObject(2);
        var kids = string.Join(" ", _pages.Select((_, index) => $"{6 + (index * 2)} 0 R"));
        Write($"<< /Type /Pages /Kids [{kids}] /Count {_pages.Count} >>\nendobj\n");

        BeginObject(3);
        Write("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");
        BeginObject(4);
        Write("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        BeginObject(5);
        Write($"<< /Title ({PdfPage.EscapeText(Title)}) /Producer (Vyrn Presence) /CreationDate (D:{DateTime.Now:yyyyMMddHHmmss}) >>\nendobj\n");

        for (var index = 0; index < _pages.Count; index++)
        {
            var page = _pages[index];
            var pageNumber = 6 + (index * 2);
            BeginObject(pageNumber);
            Write(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Format(page.Width)} {Format(page.Height)}] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {pageNumber + 1} 0 R >>\nendobj\n");

            var content = Compress(Encoding.Latin1.GetBytes(page.Content));
            BeginObject(pageNumber + 1);
            Write($"<< /Length {content.Length} /Filter /FlateDecode >>\nstream\n");
            output.Write(content, 0, content.Length);
            Write("\nendstream\nendobj\n");
        }

        var xrefOffset = output.Position;
        Write($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            Write($"{offset:0000000000} 00000 n \n");
        }
        Write($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R /Info 5 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return output.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        using var buffer = new MemoryStream();
        using (var zlib = new ZLibStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }
        return buffer.ToArray();
    }

    internal static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>Una pagina: i comandi di disegno si accumulano in un flusso di contenuto.</summary>
internal sealed class PdfPage
{
    private readonly StringBuilder _content = new();

    internal PdfPage(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public double Width { get; }

    public double Height { get; }

    internal string Content => _content.ToString();

    private double Flip(double top) => Height - top;

    /// <summary>Testo con la linea di base a <paramref name="baseline"/> dall'alto.</summary>
    public void Text(double x, double baseline, string text, double size, bool bold = false, PdfColor? color = null)
    {
        if (text.Length == 0)
        {
            return;
        }
        var paint = (color ?? new PdfColor(29, 29, 31)).Components;
        _content.Append(CultureInfo.InvariantCulture, $"BT {paint} rg /{(bold ? "F2" : "F1")} {PdfBuilder.Format(size)} Tf {PdfBuilder.Format(x)} {PdfBuilder.Format(Flip(baseline))} Td ({EscapeText(text)}) Tj ET\n");
    }

    public void TextRight(double right, double baseline, string text, double size, bool bold = false, PdfColor? color = null) =>
        Text(right - TextWidth(text, size, bold), baseline, text, size, bold, color);

    public void TextCentered(double center, double baseline, string text, double size, bool bold = false, PdfColor? color = null) =>
        Text(center - (TextWidth(text, size, bold) / 2), baseline, text, size, bold, color);

    public void Line(double x1, double y1, double x2, double y2, double width, PdfColor color)
    {
        _content.Append(CultureInfo.InvariantCulture, $"{color.Components} RG {PdfBuilder.Format(width)} w {PdfBuilder.Format(x1)} {PdfBuilder.Format(Flip(y1))} m {PdfBuilder.Format(x2)} {PdfBuilder.Format(Flip(y2))} l S\n");
    }

    public void Rectangle(double x, double top, double width, double height, PdfColor? fill, PdfColor? stroke = null, double strokeWidth = 0.6)
    {
        if (fill is null && stroke is null)
        {
            return;
        }
        var operations = new StringBuilder();
        if (fill is { } fillColor)
        {
            operations.Append(CultureInfo.InvariantCulture, $"{fillColor.Components} rg ");
        }
        if (stroke is { } strokeColor)
        {
            operations.Append(CultureInfo.InvariantCulture, $"{strokeColor.Components} RG {PdfBuilder.Format(strokeWidth)} w ");
        }
        var paintOperator = fill is not null && stroke is not null ? "B" : fill is not null ? "f" : "S";
        _content.Append(CultureInfo.InvariantCulture, $"{operations}{PdfBuilder.Format(x)} {PdfBuilder.Format(Flip(top + height))} {PdfBuilder.Format(width)} {PdfBuilder.Format(height)} re {paintOperator}\n");
    }

    /// <summary>Larghezza del testo in punti (metriche Helvetica; i caratteri accentati valgono come la lettera base).</summary>
    public static double TextWidth(string text, double size, bool bold = false)
    {
        double units = 0;
        foreach (var character in text)
        {
            units += CharacterWidth(character, bold);
        }
        return units * size / 1000.0;
    }

    /// <summary>Accorcia il testo con «…» finché sta nella larghezza data.</summary>
    public static string Fit(string text, double width, double size, bool bold = false)
    {
        if (TextWidth(text, size, bold) <= width)
        {
            return text;
        }
        const string ellipsis = "…";
        var shortened = text;
        while (shortened.Length > 1 && TextWidth(shortened + ellipsis, size, bold) > width)
        {
            shortened = shortened[..^1];
        }
        return shortened.TrimEnd() + ellipsis;
    }

    // Larghezze Helvetica per i caratteri ASCII 32-126, in millesimi di corpo.
    private static readonly int[] Regular =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584
    ];

    private static readonly int[] Bold =
    [
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611,
        975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556,
        333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
        611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584
    ];

    private static int CharacterWidth(char character, bool bold)
    {
        var table = bold ? Bold : Regular;
        if (character is >= ' ' and <= '~')
        {
            return table[character - ' '];
        }
        // Lettere accentate: come la lettera senza accento (media delle minuscole).
        return character >= 'À' ? (bold ? 580 : 540) : (bold ? 556 : 556);
    }

    /// <summary>Testo come stringa PDF in codifica WinAnsi: parentesi e barra protette, caratteri non ASCII in ottale.</summary>
    internal static string EscapeText(string text)
    {
        var builder = new StringBuilder(text.Length + 8);
        foreach (var character in text)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '(':
                    builder.Append("\\(");
                    break;
                case ')':
                    builder.Append("\\)");
                    break;
                default:
                    var code = WinAnsiCode(character);
                    if (code is >= 32 and <= 126)
                    {
                        builder.Append((char)code);
                    }
                    else
                    {
                        builder.Append('\\').Append(Convert.ToString(code, 8).PadLeft(3, '0'));
                    }
                    break;
            }
        }
        return builder.ToString();
    }

    private static int WinAnsiCode(char character) => character switch
    {
        '€' => 0x80,
        '…' => 0x85,
        '‘' => 0x91,
        '’' => 0x92,
        '“' => 0x93,
        '”' => 0x94,
        '•' => 0x95,
        '–' => 0x96,
        '—' => 0x97,
        '−' => 0x96,
        '·' => 0xB7,
        >= ' ' and <= 'ÿ' => character,
        >= ' ' and <= '~' => character,
        _ => '?'
    };
}
