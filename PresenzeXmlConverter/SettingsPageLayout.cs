namespace PresenzeXmlConverter;

/// <summary>
/// Conti sulle altezze del dialogo Impostazioni. Ogni scheda dichiara qui le
/// proprie righe e l'altezza della pagina è la loro somma: i numeri stanno in un
/// unico posto, quindi il conto si può verificare con un test (come per la card
/// della finestra principale).
///
/// Prima le altezze erano scritte a mano accanto alle righe e una scheda più
/// bassa delle righe che conteneva schiacciava i campi, tagliando l'ultima riga
/// di testo (una scheda era alta 150 px per righe che ne chiedevano
/// 142 solo per i campi utilizzati, spazi vuoti compresi).
/// </summary>
internal static class SettingsPageLayout
{
    /* ------------------------------ Righe ------------------------------- */

    /// <summary>Sottotitolo della scheda: una o due righe di testo.</summary>
    public const int HintRow = 34;

    /// <summary>Testo guida di un campo: due righe, se la frase è lunga.</summary>
    public const int FieldLabelRow = 34;

    /// <summary>Campo di testo (36 px) con i suoi margini.</summary>
    public const int FieldRow = 46;

    /// <summary>Casella di spunta con il suo margine.</summary>
    public const int CheckRow = 34;

    /// <summary>Pulsanti affiancati (34 px) con i loro margini.</summary>
    public const int ActionRow = 44;

    /// <summary>Esito di una verifica: fino a due righe.</summary>
    public const int StatusRow = 46;

    /// <summary>Nota in fondo alla scheda.</summary>
    public const int NoteRow = 34;

    /// <summary>
    /// Riga informativa in fondo alla scheda (firma digitale dell'eseguibile,
    /// modello installato): una o due righe, mai tagliata.
    /// </summary>
    public const int SignatureRow = 34;

    /// <summary>Elenco delle chiusure aziendali: quattro righe di testo.</summary>
    public const int HolidaysRow = 70;

    /* ----------------------------- Schede ------------------------------ */

    /// <summary>Scheda "Conversione": giornata standard, sabato, presenze, chiusure.</summary>
    public static readonly int[] ConversionRows =
    {
        HintRow, FieldLabelRow, FieldRow, CheckRow, CheckRow, CheckRow, FieldLabelRow, HolidaysRow, NoteRow
    };

    /// <summary>Scheda "Aggiornamenti": ricerca automatica, token e firma.</summary>
    public static readonly int[] UpdateRows =
    {
        HintRow, CheckRow, FieldLabelRow, FieldRow, ActionRow, StatusRow, NoteRow, SignatureRow
    };

    /// <summary>Scheda "Formato XML": radice, spazio dei nomi, disposizione dei movimenti.</summary>
    public static readonly int[] XmlRows =
    {
        HintRow, FieldLabelRow, FieldRow, FieldLabelRow, FieldRow, CheckRow, ActionRow, NoteRow
    };

    /// <summary>Tutte le schede, nell'ordine in cui compaiono.</summary>
    public static IReadOnlyList<int[]> Pages => new[] { ConversionRows, UpdateRows, XmlRows };

    /// <summary>Spaziatura interna della pagina (sopra e sotto).</summary>
    public const int PagePadding = 16;

    /// <summary>Altezza che la pagina chiede: le sue righe più la spaziatura interna.</summary>
    public static int PageHeight(IReadOnlyList<int> rows) => rows.Sum() + PagePadding;

    /// <summary>Altezza della scheda più alta: è quella che decide la finestra minima.</summary>
    public static int TallestPageHeight => Pages.Max(PageHeight);

    /* --------------------------- La finestra --------------------------- */

    /// <summary>Margini della finestra (sopra e sotto).</summary>
    public const int WindowPadding = 32;

    /// <summary>Riga dell'intestazione: titolo del dialogo e sottotitolo.</summary>
    public const int HeaderRow = 62;

    /// <summary>Altezza dei pulsanti "Salva impostazioni" e "Annulla".</summary>
    public const int ActionButtonHeight = 34;

    /// <summary>Riga del messaggio di errore, sopra i pulsanti del piè di pagina.</summary>
    public const int FooterMessageRow = 20;

    /// <summary>
    /// Riga del piè di pagina: messaggio di errore più i pulsanti, che con una
    /// riga più bassa finivano tagliati sul bordo inferiore.
    /// </summary>
    public const int FooterRow = FooterMessageRow + ActionButtonHeight;

    /// <summary>Spaziatura interna della card che contiene le schede.</summary>
    public const int ShellPadding = 24;

    /// <summary>Barra delle schede del controllo temizzato (40 px più la riga di separazione).</summary>
    public const int TabStripHeight = 41;

    /// <summary>
    /// Spazio che la finestra non lascia alla pagina: i suoi margini, l'intestazione,
    /// il piè di pagina, la spaziatura interna della card e la barra delle schede.
    /// </summary>
    public const int ChromeHeight = WindowPadding + HeaderRow + FooterRow + ShellPadding + TabStripHeight;

    /// <summary>Bordo e barra del titolo della finestra a 96 dpi (come la finestra principale).</summary>
    public const int WindowFrameHeight = SetupCardLayout.WindowFrameHeight;

    public const int MinimumWindowWidth = 660;

    /// <summary>
    /// Altezza minima della finestra: tolti cornice e contorno, ogni scheda entra
    /// intera, quindi non serve nessuna barra di scorrimento (quelle di Windows
    /// sono grigie e fuori tema).
    /// </summary>
    public const int MinimumWindowHeight = 640;

    public const int DefaultClientWidth = 760;

    public const int DefaultClientHeight = 640;

    /// <summary>Altezza che resta alla pagina nell'area client della finestra.</summary>
    public static int AvailablePageHeight(int clientHeight) => clientHeight - ChromeHeight;

    /// <summary>La scheda entra intera, senza scorrere?</summary>
    public static bool Fits(int clientHeight, IReadOnlyList<int> rows) =>
        PageHeight(rows) <= AvailablePageHeight(clientHeight);
}
