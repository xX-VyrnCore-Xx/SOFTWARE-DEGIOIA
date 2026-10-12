namespace PresenzeXmlConverter;

/// <summary>
/// Conti sull'altezza della card di sinistra: le righe ad altezza fissa occupano
/// sempre lo stesso spazio e la guida ai passi è l'unica elastica. Tenere qui le
/// altezze e lo spazio disponibile permette di verificare con un test che il
/// contenuto entri nella finestra: una card più alta dello spazio che ha a
/// disposizione mostrerebbe la barra di scorrimento di sistema, che Windows
/// disegna in grigio fuori tema.
/// </summary>
internal static class SetupCardLayout
{
    /* Riga per riga, nell'ordine in cui vengono impilate. */
    public const int TitleRow = 40;
    public const int SectionRow = 22;
    public const int CompanyRow = 44;
    public const int ImportRow = 46;
    public const int SourceFileRow = 26;
    public const int SheetRow = 30;
    public const int MetricsRow = 84;
    public const int ActionsRow = 42;
    public const int GenerateRow = 46;

    /// <summary>Altezza della guida ai passi quando lo spazio lo consente.</summary>
    public const int StepsMaximumHeight = 82;

    /// <summary>Sotto questa altezza il testo dei passi non entra: la guida sparisce.</summary>
    public const int StepsMinimumHeight = 74;

    /// <summary>Altezza minima di una riga della guida (il testo del passo vuole ~30 px).</summary>
    public const int StepsRowMinimumHeight = 31;

    /// <summary>
    /// Spazio che la finestra non dà all'impilamento: margini della finestra (24),
    /// intestazione (84), stato in basso (34) e spaziatura interna della card (36).
    /// </summary>
    public const int ChromeHeight = 178;

    /// <summary>Bordo e barra del titolo della finestra a 96 dpi.</summary>
    public const int WindowFrameHeight = 39;

    /// <summary>
    /// Altezza minima consentita per la finestra: tolti cornice e intestazione,
    /// la card riceve abbastanza spazio per tutte le righe fisse, quindi non può
    /// essere rimpicciolita al punto da dover scorrere.
    /// </summary>
    public const int MinimumWindowHeight = 650;

    /// <summary>Larghezza minima consentita per la finestra.</summary>
    public const int MinimumWindowWidth = 1024;

    /// <summary>Somma delle righe ad altezza fissa (la guida ai passi non è compresa).</summary>
    public static int FixedRowsHeight =>
        TitleRow + (SectionRow * 2) + CompanyRow + ImportRow + SourceFileRow + SheetRow +
        MetricsRow + ActionsRow + GenerateRow;

    /// <summary>Spazio verticale che la card mette a disposizione dell'impilamento.</summary>
    public static int ContentHeight(int windowClientHeight) => windowClientHeight - ChromeHeight;

    /// <summary>Altezza della guida ai passi: zero quando non c'è spazio per il suo testo.</summary>
    public static int StepsHeight(int available)
    {
        var remaining = available - FixedRowsHeight;
        return remaining >= StepsMinimumHeight ? Math.Min(StepsMaximumHeight, remaining) : 0;
    }

    /// <summary>
    /// Altezza totale dell'impilamento. Non deve superare lo spazio della card:
    /// oltre quel limite comparirebbe la barra di scorrimento di sistema, cosa che
    /// può succedere solo con finestre più basse di <see cref="MinimumWindowHeight"/>.
    /// </summary>
    public static int StackHeight(int available) => FixedRowsHeight + StepsHeight(available);
}
