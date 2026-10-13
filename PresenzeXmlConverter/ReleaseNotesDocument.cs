using System.Text.RegularExpressions;

namespace PresenzeXmlConverter;

internal enum NoteBlockKind
{
    Heading,
    Bullet,
    Paragraph
}

internal sealed record NoteBlock(NoteBlockKind Kind, string Text);

/// <summary>
/// Trasforma le note di una Release (Markdown, spesso generato da GitHub: «What's
/// Changed», «* titolo by @utente in https://…/pull/14», «Full Changelog») in
/// poche righe pulite: titoli, voci di elenco e paragrafi. Nessuna dipendenza da
/// WinForms, così le regole si provano senza una finestra.
/// </summary>
internal static class ReleaseNotesDocument
{
    private static readonly Regex Heading = new(@"^#{1,6}\s+(?<text>.*?)\s*#*$", RegexOptions.Compiled);
    private static readonly Regex Bullet = new(@"^(?:[*+-]|\d+[.)])\s+(?<text>.+)$", RegexOptions.Compiled);
    private static readonly Regex Attribution = new(@"\s+by\s+@[\w.-]+(?:\s+in\s+https?://\S+)?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TrailingLink = new(@"\s+in\s+https?://\S+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MarkdownLink = new(@"\[(?<label>[^\]]+)\]\((?<url>[^)]*)\)", RegexOptions.Compiled);

    public static IReadOnlyList<NoteBlock> Parse(string? notes)
    {
        var blocks = new List<NoteBlock>();
        if (string.IsNullOrWhiteSpace(notes))
        {
            return blocks;
        }

        var first = true;
        foreach (var raw in notes.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }
            // La riga d'apertura costruita dall'app («Vyrn Presence 1.22.2 — nome (tag)»)
            // ripete il titolo della finestra: non va mostrata una seconda volta.
            if (first && line.StartsWith("Vyrn Presence ", StringComparison.Ordinal) && line.Contains(" — ", StringComparison.Ordinal))
            {
                first = false;
                continue;
            }
            first = false;
            if (IsChangelogLink(line))
            {
                continue;
            }

            var heading = Heading.Match(line);
            if (heading.Success)
            {
                var text = HeadingText(Clean(heading.Groups["text"].Value));
                if (text.Length > 0)
                {
                    blocks.Add(new NoteBlock(NoteBlockKind.Heading, text));
                }
                continue;
            }

            var bullet = Bullet.Match(line);
            var content = Clean(bullet.Success ? bullet.Groups["text"].Value : line);
            if (content.Length > 0)
            {
                blocks.Add(new NoteBlock(bullet.Success ? NoteBlockKind.Bullet : NoteBlockKind.Paragraph, content));
            }
        }
        return blocks;
    }

    /// <summary>Testo semplice delle note, per l'accessibilità e per «Copia note».</summary>
    public static string ToPlainText(IReadOnlyList<NoteBlock> blocks) =>
        string.Join(
            Environment.NewLine,
            blocks.Select(block => block.Kind switch
            {
                NoteBlockKind.Heading => block.Text.ToUpperInvariant(),
                NoteBlockKind.Bullet => "• " + block.Text,
                _ => block.Text
            }));

    private static bool IsChangelogLink(string line) =>
        line.Replace("*", string.Empty).TrimStart().StartsWith("Full Changelog", StringComparison.OrdinalIgnoreCase);

    private static string HeadingText(string text) => text.ToLowerInvariant() switch
    {
        "what's changed" or "what’s changed" => "Novità",
        "new contributors" => "Nuovi contributori",
        "breaking changes" => "Modifiche importanti",
        "bug fixes" => "Correzioni",
        "features" => "Funzionalità",
        _ => text
    };

    private static string Clean(string text)
    {
        var cleaned = MarkdownLink.Replace(text, match => match.Groups["label"].Value);
        cleaned = cleaned.Replace("**", string.Empty).Replace("__", string.Empty).Replace("`", string.Empty);
        cleaned = Attribution.Replace(cleaned, string.Empty);
        cleaned = TrailingLink.Replace(cleaned, string.Empty);
        return cleaned.Trim();
    }
}
