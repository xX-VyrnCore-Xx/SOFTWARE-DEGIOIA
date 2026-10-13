namespace PresenzeXmlConverter;

/// <summary>
/// Conti della barra di scorrimento disegnata dall'applicazione. WinForms può
/// mostrare solo la barra nativa di Windows, che resta grigia e fuori tema anche
/// quando il resto della finestra è dipinto con la palette del programma: per i
/// testi lunghi (le novità di una Release) la barra viene quindi disegnata qui.
///
/// Tutta la geometria sta in questa struttura, senza WinForms: posizione e
/// altezza del pollice, scorrimento con la rotellina, trascinamento e clic sulla
/// pista sono calcolabili e verificabili con un test.
/// </summary>
internal readonly struct ScrollBarLayout
{
    /// <summary>Larghezza della fascia riservata alla barra, a destra del testo.</summary>
    public const int TrackWidth = 14;

    /// <summary>Larghezza del pollice (più stretto della fascia, per non toccare il testo).</summary>
    public const int ThumbWidth = 6;

    /// <summary>Il pollice non scende mai sotto questa altezza: altrimenti è impossibile da afferrare.</summary>
    public const int MinimumThumbHeight = 28;

    /// <summary>Scorrimento di una tacca della rotellina.</summary>
    public const int WheelStep = 48;

    private ScrollBarLayout(
        bool needsScroll,
        int contentWidth,
        int offset,
        int maximumOffset,
        int trackHeight,
        int thumbTop,
        int thumbHeight)
    {
        NeedsScroll = needsScroll;
        ContentWidth = contentWidth;
        Offset = offset;
        MaximumOffset = maximumOffset;
        TrackHeight = trackHeight;
        ThumbTop = thumbTop;
        ThumbHeight = thumbHeight;
    }

    /// <summary>La barra serve? (false quando il contenuto entra tutto: nessuna barra disegnata)</summary>
    public bool NeedsScroll { get; }

    /// <summary>Larghezza che resta al contenuto: la barra, se c'è, toglie la sua fascia.</summary>
    public int ContentWidth { get; }

    /// <summary>Scorrimento applicato, sempre fra 0 e <see cref="MaximumOffset"/>.</summary>
    public int Offset { get; }

    /// <summary>Scorrimento massimo: quanto il contenuto supera lo spazio visibile.</summary>
    public int MaximumOffset { get; }

    /// <summary>Altezza della pista (coincide con l'area visibile).</summary>
    public int TrackHeight { get; }

    /// <summary>Posizione del pollice nella pista.</summary>
    public int ThumbTop { get; }

    /// <summary>Altezza del pollice: proporzionale alla parte di contenuto visibile.</summary>
    public int ThumbHeight { get; }

    /// <summary>Posizione del pollice nel punto medio della fascia, per il disegno.</summary>
    public int ThumbLeft => TrackWidth - ThumbWidth - 3;

    /// <summary>
    /// Geometria per l'altezza visibile e quella richiesta dal contenuto. Lo
    /// scorrimento richiesto viene riportato dentro i limiti: chi chiama non deve
    /// conoscere l'altezza del contenuto per non uscire dal fondo.
    /// </summary>
    public static ScrollBarLayout Compute(
        int viewportWidth,
        int viewportHeight,
        int contentHeight,
        int requestedOffset)
    {
        var height = Math.Max(0, viewportHeight);
        var content = Math.Max(0, contentHeight);
        var needsScroll = content > height && height > 0;
        var width = Math.Max(0, viewportWidth - (needsScroll ? TrackWidth : 0));
        if (!needsScroll)
        {
            return new ScrollBarLayout(false, width, 0, 0, height, 0, height);
        }

        var maximumOffset = content - height;
        var offset = Math.Clamp(requestedOffset, 0, maximumOffset);
        var thumbHeight = (int)Math.Round(height * (double)height / content, MidpointRounding.AwayFromZero);
        thumbHeight = Math.Clamp(thumbHeight, Math.Min(MinimumThumbHeight, height), height);
        var travel = height - thumbHeight;
        var thumbTop = maximumOffset == 0
            ? 0
            : (int)Math.Round(travel * (offset / (double)maximumOffset), MidpointRounding.AwayFromZero);
        return new ScrollBarLayout(true, width, offset, maximumOffset, height, thumbTop, thumbHeight);
    }

    /// <summary>Scorrimento corrispondente alla posizione in cui viene rilasciato il pollice.</summary>
    public int OffsetForThumbTop(int thumbTop)
    {
        var travel = TrackHeight - ThumbHeight;
        if (!NeedsScroll || MaximumOffset == 0 || travel <= 0)
        {
            return 0;
        }
        var clamped = Math.Clamp(thumbTop, 0, travel);
        return (int)Math.Round(MaximumOffset * (clamped / (double)travel), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Scorrimento dopo un clic sulla pista: una schermata verso il punto toccato
    /// (sopra il pollice si va indietro, sotto si va avanti).
    /// </summary>
    public int OffsetAfterTrackClick(int y) =>
        !NeedsScroll ? 0 : y < ThumbTop ? Offset - TrackHeight : Offset + TrackHeight;

    /// <summary>
    /// Scorrimento dopo una tacca di rotellina: <paramref name="delta"/> è quello
    /// di <c>MouseEventArgs</c> (positivo verso l'alto), quindi il testo sale.
    /// </summary>
    public int OffsetAfterWheel(int delta)
    {
        if (!NeedsScroll || delta == 0)
        {
            return Offset;
        }
        var notches = delta / 120;
        if (notches == 0)
        {
            notches = Math.Sign(delta);
        }
        return Offset - (notches * WheelStep);
    }

    /// <summary>La coordinata X cade nella fascia della barra?</summary>
    public bool HitsTrack(int x, int viewportWidth) => NeedsScroll && x >= viewportWidth - TrackWidth;

    /// <summary>La coordinata Y cade sul pollice?</summary>
    public bool HitsThumb(int y) => NeedsScroll && y >= ThumbTop && y <= ThumbTop + ThumbHeight;
}
