namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Geometria della barra di scorrimento disegnata dall'app (quella nativa di
/// Windows resta grigia e fuori tema). Sono conti puri su numeri interi: pollice
/// dentro la pista, scorrimento sempre dentro i limiti, rotellina, trascinamento
/// e clic sulla pista. Girano anche senza schermo.
/// </summary>
public class ThemedScrollTests
{
    [Fact]
    public void NothingScrollsWhileTheContentFits()
    {
        var layout = ScrollBarLayout.Compute(300, 200, 180, 0);
        Assert.False(layout.NeedsScroll);
        Assert.Equal(0, layout.Offset);
        Assert.Equal(0, layout.MaximumOffset);
        Assert.Equal(200, layout.ThumbHeight);
        // Nessuna barra: al contenuto resta tutta la larghezza.
        Assert.Equal(300, layout.ContentWidth);
    }

    [Fact]
    public void WhenTheBarAppearsTheContentLosesItsStrip()
    {
        var layout = ScrollBarLayout.Compute(300, 200, 500, 0);
        Assert.True(layout.NeedsScroll);
        Assert.Equal(300 - ScrollBarLayout.TrackWidth, layout.ContentWidth);
        Assert.Equal(300, layout.MaximumOffset);
        Assert.True(layout.HitsTrack(300 - ScrollBarLayout.TrackWidth, 300));
        Assert.False(layout.HitsTrack(300 - ScrollBarLayout.TrackWidth - 1, 300));
        // Senza barra, nessuna fascia cliccabile.
        Assert.False(ScrollBarLayout.Compute(300, 200, 180, 0).HitsTrack(299, 300));
    }

    [Fact]
    public void TheOffsetIsAlwaysInsideTheContent()
    {
        Assert.Equal(0, ScrollBarLayout.Compute(300, 200, 500, -80).Offset);
        var bottom = ScrollBarLayout.Compute(300, 200, 500, 9999);
        Assert.Equal(bottom.MaximumOffset, bottom.Offset);
        Assert.Equal(bottom.TrackHeight - bottom.ThumbHeight, bottom.ThumbTop);
    }

    [Fact]
    public void TheThumbNeverGetsShorterThanItsMinimumNorTallerThanTheTrack()
    {
        var huge = ScrollBarLayout.Compute(300, 100, 100_000, 0);
        Assert.Equal(ScrollBarLayout.MinimumThumbHeight, huge.ThumbHeight);
        Assert.InRange(huge.ThumbTop, 0, huge.TrackHeight - huge.ThumbHeight);

        var tiny = ScrollBarLayout.Compute(300, 100, 101, 0);
        Assert.True(tiny.ThumbHeight <= tiny.TrackHeight);
        Assert.InRange(tiny.ThumbTop, 0, tiny.TrackHeight - tiny.ThumbHeight);
    }

    [Fact]
    public void TheThumbStaysInsideTheTrackForEveryOffset()
    {
        for (var content = 201; content <= 3000; content += 37)
        {
            for (var requested = 0; requested <= content; requested += 13)
            {
                var layout = ScrollBarLayout.Compute(320, 200, content, requested);
                Assert.True(layout.ThumbTop >= 0, $"Pollice sopra la pista ({content}/{requested}).");
                Assert.True(
                    layout.ThumbTop + layout.ThumbHeight <= layout.TrackHeight,
                    $"Pollice oltre il fondo ({content}/{requested}).");
                Assert.InRange(layout.Offset, 0, layout.MaximumOffset);
            }
        }
    }

    [Fact]
    public void DraggingTheThumbKeepsItsPositionAndReachesTheEnds()
    {
        var layout = ScrollBarLayout.Compute(320, 200, 700, 200);
        // Afferrato il pollice dove si trova, lo scorrimento non cambia.
        Assert.InRange(layout.OffsetForThumbTop(layout.ThumbTop), 199, 201);
        Assert.Equal(0, layout.OffsetForThumbTop(-50));
        Assert.Equal(layout.MaximumOffset, layout.OffsetForThumbTop(10_000));
    }

    [Fact]
    public void ClickingTheTrackScollsOneScreenTowardsTheClick()
    {
        var layout = ScrollBarLayout.Compute(320, 200, 900, 350);
        Assert.Equal(350 - 200, layout.OffsetAfterTrackClick(layout.ThumbTop - 5));
        Assert.Equal(350 + 200, layout.OffsetAfterTrackClick(layout.ThumbTop + layout.ThumbHeight + 5));
    }

    [Fact]
    public void TheWheelScrollsAndStopsAtTheEnds()
    {
        var layout = ScrollBarLayout.Compute(320, 200, 900, 350);
        Assert.Equal(350 - ScrollBarLayout.WheelStep, layout.OffsetAfterWheel(120));
        Assert.Equal(350 + ScrollBarLayout.WheelStep, layout.OffsetAfterWheel(-120));
        // Una tacca sola, anche quando il sistema invia un delta diverso.
        Assert.Equal(350 - ScrollBarLayout.WheelStep, layout.OffsetAfterWheel(30));
        // I limiti li applica Compute: la rotellina non può uscire dal testo.
        var atEnd = layout.OffsetAfterWheel(-120 * 100);
        Assert.Equal(layout.MaximumOffset, ScrollBarLayout.Compute(320, 200, 900, atEnd).Offset);
        Assert.Equal(0, ScrollBarLayout.Compute(320, 200, 900, layout.OffsetAfterWheel(120 * 100)).Offset);
    }

    [Fact]
    public void TheThumbGrowsWithTheVisiblePartOfTheContent()
    {
        var moreVisible = ScrollBarLayout.Compute(320, 200, 400, 0);
        var lessVisible = ScrollBarLayout.Compute(320, 200, 4000, 0);
        Assert.True(moreVisible.ThumbHeight > lessVisible.ThumbHeight);
    }

    [Fact]
    public void AnEmptyOrZeroSpaceViewportNeverBreaks()
    {
        var layout = ScrollBarLayout.Compute(0, 0, 0, 0);
        Assert.False(layout.NeedsScroll);
        Assert.Equal(0, layout.Offset);
        Assert.Equal(0, ScrollBarLayout.Compute(0, 200, 0, 5).Offset);
    }
}
