using System.Text;

namespace PresenzeXmlConverter;

/// <summary>
/// Evidenziazione dell'XML come documento RTF unico. Colorare il testo con una selezione
/// per ogni tag, come si faceva prima, costa un messaggio a Windows per ciascuno (decine
/// di migliaia su un file medio: secondi di interfaccia bloccata). Qui il colore è scritto
/// nel testo RTF in un solo passaggio (millisecondi) e il controllo lo carica una volta.
/// </summary>
internal static class XmlRtf
{
    /// <summary>Colori in ordine: testo, tag, stringhe fra virgolette, valori.</summary>
    public readonly record struct Palette(
        (int R, int G, int B) Ink,
        (int R, int G, int B) Tag,
        (int R, int G, int B) String,
        (int R, int G, int B) Value);

    public static string Build(string xml, Palette palette, string fontName, float fontPoints)
    {
        var builder = new StringBuilder(xml.Length * 2 + 256);
        builder.Append(@"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fmodern\fcharset0 ")
            .Append(fontName)
            .Append(@";}}{\colortbl ;");
        foreach (var color in new[] { palette.Ink, palette.Tag, palette.String, palette.Value })
        {
            builder.Append($"\\red{color.R}\\green{color.G}\\blue{color.B};");
        }
        builder.Append(@"}\viewkind4\uc1\pard\f0\fs")
            .Append(Math.Max(2, (int)Math.Round(fontPoints * 2)))
            .Append(@"\cf1 ");

        var index = 0;
        while (index < xml.Length)
        {
            if (xml[index] == '<')
            {
                index = AppendTag(builder, xml, index);
            }
            else
            {
                var end = xml.IndexOf('<', index);
                if (end < 0)
                {
                    end = xml.Length;
                }
                AppendRun(builder, xml, index, end, 4);
                index = end;
            }
        }
        builder.Append('}');
        return builder.ToString();
    }

    /// <summary>Scrive un tag (nome in colore tag, attributi fra virgolette in colore stringa).</summary>
    private static int AppendTag(StringBuilder builder, string xml, int start)
    {
        var end = xml.IndexOf('>', start);
        end = end < 0 ? xml.Length : end + 1;
        var segment = start;
        var inQuote = '\0';
        for (var position = start; position < end; position++)
        {
            var character = xml[position];
            if (inQuote == '\0' && (character == '"' || character == '\''))
            {
                AppendRun(builder, xml, segment, position, 2);
                inQuote = character;
                segment = position;
            }
            else if (inQuote != '\0' && character == inQuote)
            {
                AppendRun(builder, xml, segment, position + 1, 3);
                inQuote = '\0';
                segment = position + 1;
            }
        }
        AppendRun(builder, xml, segment, end, 2);
        return end;
    }

    private static void AppendRun(StringBuilder builder, string xml, int start, int end, int color)
    {
        if (end <= start)
        {
            return;
        }
        builder.Append("\\cf").Append(color).Append(' ');
        for (var position = start; position < end; position++)
        {
            var character = xml[position];
            switch (character)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '{':
                    builder.Append(@"\{");
                    break;
                case '}':
                    builder.Append(@"\}");
                    break;
                case '\r':
                    break;
                case '\n':
                    builder.Append("\\par\r\n");
                    break;
                case '\t':
                    builder.Append(@"\tab ");
                    break;
                default:
                    if (character < 0x20)
                    {
                        break;
                    }
                    if (character < 0x80)
                    {
                        builder.Append(character);
                    }
                    else
                    {
                        // Caratteri non ASCII: numero a 16 bit con segno, come vuole l'RTF.
                        builder.Append("\\u").Append((short)character).Append('?');
                    }
                    break;
            }
        }
    }
}
