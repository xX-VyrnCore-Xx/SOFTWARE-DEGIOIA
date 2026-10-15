using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public sealed class ButtonLayoutTests
{
    [Fact]
    public void IconAndTextAreCenteredTogetherWhenTheyFit()
    {
        // 200 di larghezza, testo 80, icona 16 + 7: il gruppo (103) sta al centro.
        var layout = ButtonLayout.Compute(200, 80, 16, 7, 10, hasIcon: true);

        Assert.True(layout.ShowIcon);
        Assert.Equal((200 - 103) / 2, layout.IconLeft);
        Assert.Equal(layout.IconLeft + 16 + 7, layout.TextLeft);
        Assert.Equal(80, layout.TextWidth);
        Assert.True(layout.TextLeft + layout.TextWidth <= 200 - 10);
    }

    [Fact]
    public void IconIsDroppedBeforeTheLabelIsCut()
    {
        // Il testo da solo entra (80 ≤ 100 - 20), con l'icona no (103 > 80): resta il solo testo, centrato e intero.
        var layout = ButtonLayout.Compute(100, 80, 16, 7, 10, hasIcon: true);

        Assert.False(layout.ShowIcon);
        Assert.Equal(80, layout.TextWidth);
        Assert.Equal(10, layout.TextLeft);
    }

    [Fact]
    public void WithoutIconTheTextIsCenteredAndNeverWiderThanTheInnerArea()
    {
        var centered = ButtonLayout.Compute(200, 80, 16, 7, 10, hasIcon: false);
        Assert.False(centered.ShowIcon);
        Assert.Equal(60, centered.TextLeft);

        var tight = ButtonLayout.Compute(60, 200, 16, 7, 10, hasIcon: true);
        Assert.False(tight.ShowIcon);
        Assert.Equal(40, tight.TextWidth);
        Assert.Equal(10, tight.TextLeft);
    }

    [Fact]
    public void ExactFitKeepsTheIcon()
    {
        var layout = ButtonLayout.Compute(123, 80, 16, 7, 10, hasIcon: true);

        Assert.True(layout.ShowIcon);
        Assert.Equal(10, layout.IconLeft);
    }
}
