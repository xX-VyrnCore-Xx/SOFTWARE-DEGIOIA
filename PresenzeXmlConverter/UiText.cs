namespace PresenzeXmlConverter;

/// <summary>
/// Disegno del testo nei controlli fatti a mano. Il testo non viene mai tagliato dal bordo del suo riquadro: con
/// caratteri più grandi di quanto previsto (o con lo scaling di Windows al 125–150 %) una riga che sborda di un
/// pixel resta leggibile invece di perdere la metà inferiore delle lettere. Le ellissi «…» restano calcolate sulla larghezza.
/// </summary>
internal static class UiText
{
    public static void Draw(IDeviceContext device, string? text, Font font, Rectangle bounds, Color color, TextFormatFlags flags) =>
        TextRenderer.DrawText(device, text, font, bounds, color, flags | TextFormatFlags.NoClipping);
}
