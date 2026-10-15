namespace PresenzeXmlConverter;

/// <summary>Dove cadono icona e testo dentro un pulsante.</summary>
internal readonly record struct ButtonContentLayout(bool ShowIcon, int IconLeft, int TextLeft, int TextWidth);

/// <summary>
/// Disposizione di icona e testo di un pulsante: calcolo puro, verificabile con un test. L'icona e il testo
/// stanno insieme al centro; se la larghezza non basta per entrambi l'icona si toglie e resta il testo,
/// perché un'etichetta tagliata è peggio di un pulsante senza icona.
/// </summary>
internal static class ButtonLayout
{
    public static ButtonContentLayout Compute(int width, int textWidth, int iconSize, int gap, int padding, bool hasIcon)
    {
        var inner = Math.Max(0, width - (2 * padding));
        if (hasIcon)
        {
            var group = iconSize + gap + textWidth;
            if (group <= inner)
            {
                var left = (width - group) / 2;
                return new ButtonContentLayout(true, left, left + iconSize + gap, textWidth);
            }
        }
        var shown = Math.Min(textWidth, inner);
        return new ButtonContentLayout(false, 0, Math.Max(padding, (width - shown) / 2), shown);
    }
}
