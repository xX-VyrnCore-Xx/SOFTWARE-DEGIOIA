namespace PresenzeXmlConverter.Tests;

public class ReleaseNotesTextTests
{
    [Fact]
    public void MarkdownHeadingsAndBoldMarkersAreRemovedFromTheDisplayedNotes()
    {
        var shown = ReleaseNotesDialog.ReadableNotes("## Grafica Windows\n- **Titolo** scuro\n### Controlli\n#hashtag");
        var lines = shown.Split(Environment.NewLine);

        Assert.Equal("Grafica Windows", lines[0]);
        Assert.Equal("- Titolo scuro", lines[1]);
        Assert.Equal("Controlli", lines[2]);
        Assert.Equal("#hashtag", lines[3]);
    }
}
