using System.Globalization;
using System.Text;

namespace PresenzeXmlConverter;

/// <summary>Le icone dell'interfaccia. Il disegno di ognuna sta in <see cref="IconGeometry"/>.</summary>
internal enum Glyph
{
    Search,
    Gear,
    Refresh,
    Upload,
    File,
    Check,
    Clock,
    Database,
    Shield,
    Arrow,
    Folder,
    Alert,
    Table,
    Code,
    Chip,
    Users,
    Sum,
    Download,
    Plus,
    Trash,
    Transfer,
    Save,
    Dashboard,
    Sheet,
    Building,
    List,
    Info
}

/// <summary>Un segmento di tracciato: M = sposta, L = linea, C = curva di Bézier cubica (tre punti), Z = chiudi.</summary>
internal readonly record struct IconSeg(char Op, float[] Values);

/// <summary>Un tracciato dell'icona: a tratto (di norma) oppure pieno (i puntini).</summary>
internal sealed record IconPath(IReadOnlyList<IconSeg> Segs, bool Filled);

/// <summary>
/// Disegno delle icone come dati: tracciati su una griglia di 24 × 24 unità, a tratto con estremità tonde (stile
/// Feather/Lucide), tutti dentro un margine di 2 unità perché il tratto non venga mai tagliato. Chi disegna
/// (GDI+ nell'app) e chi controlla (i test, l'anteprima SVG) usano gli stessi numeri: un'icona sbagliata si vede
/// prima di arrivare sullo schermo.
/// </summary>
internal static class IconGeometry
{
    public const float Grid = 24f;

    private const float Kappa = 0.5522847498f;

    private static readonly Dictionary<Glyph, IReadOnlyList<IconPath>> Cache = [];

    public static IReadOnlyList<IconPath> For(Glyph glyph)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(glyph, out var paths))
            {
                paths = Build(glyph);
                Cache[glyph] = paths;
            }
            return paths;
        }
    }

    /// <summary>L'icona come SVG (24 × 24): per l'anteprima e per i test.</summary>
    public static string ToSvg(Glyph glyph, string color = "#1d1d1f", float stroke = 2f)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{stroke}\" stroke-linecap=\"round\" stroke-linejoin=\"round\">");
        foreach (var path in For(glyph))
        {
            text.Append(CultureInfo.InvariantCulture, $"<path d=\"{SvgData(path)}\"");
            if (path.Filled)
            {
                text.Append(CultureInfo.InvariantCulture, $" fill=\"{color}\" stroke=\"none\"");
            }
            text.Append("/>");
        }
        text.Append("</svg>");
        return text.ToString();
    }

    private static string SvgData(IconPath path)
    {
        var data = new StringBuilder();
        foreach (var seg in path.Segs)
        {
            data.Append(seg.Op);
            data.Append(string.Join(' ', seg.Values.Select(value => value.ToString("0.###", CultureInfo.InvariantCulture))));
        }
        return data.ToString();
    }

    /* ----------------------------- le icone ------------------------------ */

    private static IReadOnlyList<IconPath> Build(Glyph glyph) => glyph switch
    {
        Glyph.Search => [Circle(10.5f, 10.5f, 7f), Line(15.8f, 15.8f, 21f, 21f)],
        Glyph.Gear => [GearOutline(), Circle(12f, 12f, 3.2f)],
        Glyph.Refresh => RefreshIcon(),
        Glyph.Upload => [Line(12f, 16f, 12f, 4f), Poly(false, 7f, 9f, 12f, 4f, 17f, 9f), Tray()],
        Glyph.Download => [Line(12f, 4f, 12f, 16f), Poly(false, 7f, 11f, 12f, 16f, 17f, 11f), Tray()],
        Glyph.File => FileIcon(),
        Glyph.Check => [Poly(false, 4.5f, 12.5f, 9.5f, 17.5f, 19.5f, 6.5f)],
        Glyph.Clock => [Circle(12f, 12f, 9f), Poly(false, 12f, 7f, 12f, 12f, 15.5f, 14f)],
        Glyph.Database =>
        [
            Ellipse(12f, 6f, 8f, 3f),
            Line(4f, 6f, 4f, 18f),
            Line(20f, 6f, 20f, 18f),
            EllipseArc(12f, 12f, 8f, 3f, 0f, 180f),
            EllipseArc(12f, 18f, 8f, 3f, 0f, 180f)
        ],
        Glyph.Shield =>
        [
            Path(false, [M(12f, 3f), L(20f, 6f), L(20f, 12f), C(20f, 16.5f, 16.5f, 20f, 12f, 21.5f), C(7.5f, 20f, 4f, 16.5f, 4f, 12f), L(4f, 6f), Z()]),
            Poly(false, 8.5f, 12f, 11f, 14.5f, 15.5f, 9.5f)
        ],
        Glyph.Arrow => [Line(4f, 12f, 20f, 12f), Poly(false, 14f, 6f, 20f, 12f, 14f, 18f)],
        Glyph.Folder => [Poly(true, 3f, 6f, 9f, 6f, 11.5f, 9f, 21f, 9f, 21f, 19f, 3f, 19f)],
        Glyph.Alert => [Poly(true, 12f, 3.5f, 21.5f, 20f, 2.5f, 20f), Line(12f, 10f, 12f, 14.5f), Dot(12f, 17.3f, 1.15f)],
        Glyph.Table => [RoundRect(3f, 4f, 18f, 16f, 2f), Line(3f, 10f, 21f, 10f), Line(9.5f, 10f, 9.5f, 20f)],
        Glyph.Code => [Poly(false, 8f, 7f, 3f, 12f, 8f, 17f), Poly(false, 16f, 7f, 21f, 12f, 16f, 17f), Line(13.5f, 5f, 10.5f, 19f)],
        Glyph.Info => [Circle(12f, 12f, 9f), Line(12f, 11f, 12f, 16.5f), Dot(12f, 7.8f, 1.15f)],
        Glyph.Chip => ChipIcon(),
        Glyph.Users =>
        [
            Circle(9f, 8f, 3.5f),
            Path(false, [M(2.5f, 20f), C(2.5f, 16f, 5.5f, 14f, 9f, 14f), C(12.5f, 14f, 15.5f, 16f, 15.5f, 20f)]),
            Path(false, [M(15.5f, 4.6f), C(17.8f, 4.9f, 19.2f, 6.6f, 18.8f, 8.6f), C(18.5f, 10.2f, 17.4f, 11.2f, 16f, 11.5f)]),
            Path(false, [M(17.5f, 14.4f), C(19.8f, 14.9f, 21.5f, 16.6f, 21.5f, 19.5f)])
        ],
        Glyph.Sum => [Poly(false, 18f, 5f, 6f, 5f, 12f, 12f, 6f, 19f, 18f, 19f)],
        Glyph.Plus => [Line(12f, 5f, 12f, 19f), Line(5f, 12f, 19f, 12f)],
        Glyph.Trash =>
        [
            Line(4f, 7f, 20f, 7f),
            Poly(false, 9f, 7f, 9f, 4f, 15f, 4f, 15f, 7f),
            Poly(false, 6f, 7f, 7f, 20f, 17f, 20f, 18f, 7f),
            Line(10f, 11f, 10f, 16f),
            Line(14f, 11f, 14f, 16f)
        ],
        Glyph.Transfer =>
        [
            Line(4f, 8f, 20f, 8f),
            Poly(false, 16f, 4f, 20f, 8f, 16f, 12f),
            Line(20f, 16f, 4f, 16f),
            Poly(false, 8f, 12f, 4f, 16f, 8f, 20f)
        ],
        Glyph.Save =>
        [
            Poly(true, 4f, 4f, 16f, 4f, 20f, 8f, 20f, 20f, 4f, 20f),
            Poly(false, 8f, 4f, 8f, 9f, 15f, 9f, 15f, 4f),
            Poly(false, 7f, 20f, 7f, 13f, 17f, 13f, 17f, 20f)
        ],
        Glyph.Dashboard =>
        [
            RoundRect(3f, 3f, 7.5f, 7.5f, 1.6f),
            RoundRect(13.5f, 3f, 7.5f, 7.5f, 1.6f),
            RoundRect(3f, 13.5f, 7.5f, 7.5f, 1.6f),
            RoundRect(13.5f, 13.5f, 7.5f, 7.5f, 1.6f)
        ],
        Glyph.Sheet => [.. FileOutline(), Line(8.5f, 14f, 15.5f, 14f), Poly(false, 13f, 11.5f, 15.5f, 14f, 13f, 16.5f)],
        Glyph.Building =>
        [
            RoundRect(5f, 3f, 14f, 18f, 1.6f),
            Poly(false, 10f, 21f, 10f, 16.5f, 14f, 16.5f, 14f, 21f),
            Dot(9f, 7.5f, 1.1f),
            Dot(15f, 7.5f, 1.1f),
            Dot(9f, 11.8f, 1.1f),
            Dot(15f, 11.8f, 1.1f)
        ],
        Glyph.List =>
        [
            Dot(5f, 6f, 1.5f), Dot(5f, 12f, 1.5f), Dot(5f, 18f, 1.5f),
            Line(10f, 6f, 20f, 6f), Line(10f, 12f, 20f, 12f), Line(10f, 18f, 20f, 18f)
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(glyph))
    };

    private static IconPath Tray() => Poly(false, 4f, 15f, 4f, 20f, 20f, 20f, 20f, 15f);

    private static IReadOnlyList<IconPath> FileOutline() =>
    [
        Poly(true, 5f, 3f, 14f, 3f, 19f, 8f, 19f, 21f, 5f, 21f),
        Poly(false, 14f, 3f, 14f, 8f, 19f, 8f)
    ];

    private static IReadOnlyList<IconPath> FileIcon() =>
    [
        .. FileOutline(),
        Line(8.5f, 13f, 15.5f, 13f),
        Line(8.5f, 17f, 15.5f, 17f)
    ];

    private static IReadOnlyList<IconPath> ChipIcon() =>
    [
        RoundRect(6f, 6f, 12f, 12f, 1.6f),
        RoundRect(9.6f, 9.6f, 4.8f, 4.8f, 0.6f),
        Line(9f, 3f, 9f, 6f), Line(15f, 3f, 15f, 6f),
        Line(9f, 18f, 9f, 21f), Line(15f, 18f, 15f, 21f),
        Line(3f, 9f, 6f, 9f), Line(3f, 15f, 6f, 15f),
        Line(18f, 9f, 21f, 9f), Line(18f, 15f, 21f, 15f)
    ];

    /// <summary>Freccia circolare in senso orario con la punta in alto a destra.</summary>
    private static IReadOnlyList<IconPath> RefreshIcon() =>
    [
        EllipseArc(12f, 12f, 8f, 8f, 25f, 290f),
        Poly(false, 14.4f, 6.9f, 18.5f, 7.3f, 18.2f, 3.2f)
    ];

    /// <summary>Ingranaggio a otto denti: contorno unico con i fianchi leggermente inclinati.</summary>
    private static IconPath GearOutline()
    {
        const int teeth = 8;
        const float outer = 10f;
        const float inner = 7.4f;
        var points = new List<float>();
        for (var tooth = 0; tooth < teeth; tooth++)
        {
            var center = tooth * 360f / teeth;
            foreach (var (offset, radius) in new[] { (-14f, inner), (-8.5f, outer), (8.5f, outer), (14f, inner) })
            {
                var angle = (center + offset) * MathF.PI / 180f;
                points.Add(12f + (radius * MathF.Cos(angle)));
                points.Add(12f + (radius * MathF.Sin(angle)));
            }
        }
        return Poly(true, [.. points]);
    }

    /* ------------------------------ mattoni ------------------------------ */

    private static IconSeg M(float x, float y) => new('M', [x, y]);

    private static IconSeg L(float x, float y) => new('L', [x, y]);

    private static IconSeg C(float x1, float y1, float x2, float y2, float x, float y) => new('C', [x1, y1, x2, y2, x, y]);

    private static IconSeg Z() => new('Z', []);

    private static IconPath Path(bool filled, IconSeg[] segs) => new(segs, filled);

    private static IconPath Line(float x1, float y1, float x2, float y2) => Path(false, [M(x1, y1), L(x2, y2)]);

    /// <summary>Spezzata: con <paramref name="closed"/> torna al primo punto.</summary>
    private static IconPath Poly(bool closed, params float[] points)
    {
        var segs = new List<IconSeg> { M(points[0], points[1]) };
        for (var index = 2; index < points.Length; index += 2)
        {
            segs.Add(L(points[index], points[index + 1]));
        }
        if (closed)
        {
            segs.Add(Z());
        }
        return Path(false, [.. segs]);
    }

    private static IconPath Circle(float cx, float cy, float r) => Ellipse(cx, cy, r, r);

    private static IconPath Ellipse(float cx, float cy, float rx, float ry) => EllipseArc(cx, cy, rx, ry, 0f, 360f);

    /// <summary>Puntino pieno.</summary>
    private static IconPath Dot(float cx, float cy, float r)
    {
        var circle = Circle(cx, cy, r);
        return circle with { Filled = true };
    }

    /// <summary>Arco di ellisse (angoli in gradi, in senso orario con l'asse y verso il basso), a pezzi da al più 90°.</summary>
    private static IconPath EllipseArc(float cx, float cy, float rx, float ry, float startDeg, float sweepDeg)
    {
        var segs = new List<IconSeg>();
        var pieces = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(sweepDeg) / 90f));
        var step = sweepDeg / pieces;
        var k = 4f / 3f * MathF.Tan(step * MathF.PI / 180f / 4f);
        for (var piece = 0; piece < pieces; piece++)
        {
            var a0 = (startDeg + (piece * step)) * MathF.PI / 180f;
            var a1 = (startDeg + ((piece + 1) * step)) * MathF.PI / 180f;
            float X(float a) => cx + (rx * MathF.Cos(a));
            float Y(float a) => cy + (ry * MathF.Sin(a));
            if (piece == 0)
            {
                segs.Add(M(X(a0), Y(a0)));
            }
            segs.Add(C(
                X(a0) - (k * rx * MathF.Sin(a0)),
                Y(a0) + (k * ry * MathF.Cos(a0)),
                X(a1) + (k * rx * MathF.Sin(a1)),
                Y(a1) - (k * ry * MathF.Cos(a1)),
                X(a1),
                Y(a1)));
        }
        if (MathF.Abs(sweepDeg) >= 360f)
        {
            segs.Add(Z());
        }
        return Path(false, [.. segs]);
    }

    /// <summary>Rettangolo con gli angoli arrotondati.</summary>
    private static IconPath RoundRect(float x, float y, float w, float h, float r)
    {
        var k = r * (1f - Kappa);
        return Path(false,
        [
            M(x + r, y), L(x + w - r, y), C(x + w - k, y, x + w, y + k, x + w, y + r),
            L(x + w, y + h - r), C(x + w, y + h - k, x + w - k, y + h, x + w - r, y + h),
            L(x + r, y + h), C(x + k, y + h, x, y + h - k, x, y + h - r),
            L(x, y + r), C(x, y + k, x + k, y, x + r, y), Z()
        ]);
    }
}
