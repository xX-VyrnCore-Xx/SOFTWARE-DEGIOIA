namespace PresenzeXmlConverter.Tests;

public class ReleaseNotesTextTests
{
    private const string GitHubNotes =
        "Vyrn Presence 1.22.2 — PresenzeFlow 1.22.2 (v1.22.2)\n\n" +
        "## What's Changed\n" +
        "* 1.22.2: «Salva azienda» sempre visibile by @xX-VyrnCore-Xx in https://github.com/xX-VyrnCore-Xx/SOFTWARE-DEGIOIA/pull/14\n\n\n" +
        "**Full Changelog**: https://github.com/xX-VyrnCore-Xx/SOFTWARE-DEGIOIA/compare/v1.22.1...v1.22.2";

    [Fact]
    public void GitHubGeneratedNotesBecomeAHeadingAndCleanBullets()
    {
        var blocks = ReleaseNotesDocument.Parse(GitHubNotes);

        Assert.Collection(
            blocks,
            heading =>
            {
                Assert.Equal(NoteBlockKind.Heading, heading.Kind);
                Assert.Equal("Novità", heading.Text);
            },
            bullet =>
            {
                Assert.Equal(NoteBlockKind.Bullet, bullet.Kind);
                Assert.Equal("1.22.2: «Salva azienda» sempre visibile", bullet.Text);
            });
    }

    [Fact]
    public void HandWrittenMarkdownLosesItsMarkers()
    {
        var blocks = ReleaseNotesDocument.Parse("## Grafica Windows\n- **Titolo** scuro\n### Controlli\nTesto libero\n#hashtag");

        Assert.Equal(
            new[]
            {
                (NoteBlockKind.Heading, "Grafica Windows"),
                (NoteBlockKind.Bullet, "Titolo scuro"),
                (NoteBlockKind.Heading, "Controlli"),
                (NoteBlockKind.Paragraph, "Testo libero"),
                (NoteBlockKind.Paragraph, "#hashtag")
            },
            blocks.Select(block => (block.Kind, block.Text)).ToArray());
    }

    [Fact]
    public void EmptyNotesProduceNoBlocksAndPlainTextKeepsBullets()
    {
        Assert.Empty(ReleaseNotesDocument.Parse("  \n "));
        var plain = ReleaseNotesDocument.ToPlainText(ReleaseNotesDocument.Parse("## Novità\n* Voce"));
        Assert.Equal("NOVITÀ" + Environment.NewLine + "• Voce", plain);
    }
}
