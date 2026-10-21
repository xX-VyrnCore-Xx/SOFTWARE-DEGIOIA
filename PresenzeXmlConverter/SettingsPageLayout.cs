namespace PresenzeXmlConverter;

/// <summary>
/// Misure della finestra Impostazioni (a 96 dpi). Le schede non hanno più altezze di riga dichiarate qui:
/// ogni riga è alta quanto il suo contenuto (vedi <c>SettingsDialog.CreatePage</c>), quindi non c'è nessun
/// conto che il carattere più grande o un testo più lungo possano far saltare. Restano i limiti della
/// finestra, e un test grafico verifica che ogni scheda entri senza barre di scorrimento.
/// </summary>
internal static class SettingsPageLayout
{
    /// <summary>Altezza dei pulsanti «Salva impostazioni» e «Annulla».</summary>
    public const int ActionButtonHeight = 36;

    /// <summary>Bordo e barra del titolo della finestra a 96 dpi (come la finestra principale).</summary>
    public const int WindowFrameHeight = SetupCardLayout.WindowFrameHeight;

    public const int MinimumWindowWidth = 680;

    /// <summary>Altezza minima: tutte le schede entrano intere, senza barre di scorrimento di sistema.</summary>
    public const int MinimumWindowHeight = 660;

    public const int DefaultClientWidth = 780;

    public const int DefaultClientHeight = 660;
}
