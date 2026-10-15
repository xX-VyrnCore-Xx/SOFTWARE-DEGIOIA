using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public sealed class IconGeometryTests
{
    public static IEnumerable<object[]> AllGlyphs() =>
        Enum.GetNames<Glyph>().Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(AllGlyphs))]
    public void EveryGlyphHasADrawingThatStaysInsideTheGridMargin(string name)
    {
        var glyph = Enum.Parse<Glyph>(name);
        var paths = IconGeometry.For(glyph);
        Assert.NotEmpty(paths);
        foreach (var path in paths)
        {
            Assert.NotEmpty(path.Segs);
            Assert.Equal('M', path.Segs[0].Op);
            foreach (var seg in path.Segs)
            {
                Assert.Equal(seg.Op == 'C' ? 6 : seg.Op == 'Z' ? 0 : 2, seg.Values.Length);
                foreach (var value in seg.Values)
                {
                    // 1 unità di margine: con il tratto da 2 unità l'icona non viene mai tagliata dal riquadro.
                    Assert.InRange(value, 1f, 23f);
                }
            }
        }
    }

    [Fact]
    public void EveryGlyphIsDistinct()
    {
        var drawings = Enum.GetValues<Glyph>().Select(glyph => IconGeometry.ToSvg(glyph)).ToArray();
        Assert.Equal(drawings.Length, drawings.Distinct().Count());
    }

    [Fact]
    public void OnlyDotsAreFilled()
    {
        foreach (var glyph in Enum.GetValues<Glyph>())
        {
            foreach (var path in IconGeometry.For(glyph).Where(path => path.Filled))
            {
                // Un puntino è un cerchio chiuso (quattro curve), mai una linea aperta.
                Assert.Equal('Z', path.Segs[^1].Op);
                Assert.Equal(4, path.Segs.Count(seg => seg.Op == 'C'));
            }
        }
    }

    [Fact]
    public void SvgPreviewIsWellFormed()
    {
        var svg = IconGeometry.ToSvg(Glyph.Check);
        Assert.StartsWith("<svg", svg);
        Assert.EndsWith("</svg>", svg);
        Assert.Contains("M4.5 12.5L9.5 17.5L19.5 6.5", svg);
    }

    /// <summary>Con VP_ICON_SHEET=percorso scrive una pagina con tutte le icone, da guardare in un browser.</summary>
    [Fact]
    public void WritesTheContactSheetWhenAskedTo()
    {
        var target = Environment.GetEnvironmentVariable("VP_ICON_SHEET");
        if (string.IsNullOrEmpty(target))
        {
            return;
        }
        var cells = string.Concat(Enum.GetValues<Glyph>().Select(glyph =>
            $"<div class=c><div class=b>{IconGeometry.ToSvg(glyph, stroke: 2f).Replace("<svg", "<svg width=96 height=96")}</div>" +
            $"<div class=s>{IconGeometry.ToSvg(glyph, stroke: 1.7f * 24 / 18).Replace("<svg", "<svg width=18 height=18")}" +
            $"{IconGeometry.ToSvg(glyph, stroke: 1.6f * 24 / 16).Replace("<svg", "<svg width=16 height=16")}</div><p>{glyph}</p></div>"));
        File.WriteAllText(
            target,
            "<!doctype html><meta charset=utf-8><style>body{font:12px sans-serif;background:#fff;margin:16px}" +
            ".g{display:flex;flex-wrap:wrap;gap:10px}.c{width:120px;text-align:center;border:1px solid #ddd;border-radius:8px;padding:8px}" +
            ".b{background:#f5f5f7}.s{display:flex;gap:10px;justify-content:center;margin:6px 0}p{margin:2px}</style>" +
            $"<div class=g>{cells}</div>");
    }
}
