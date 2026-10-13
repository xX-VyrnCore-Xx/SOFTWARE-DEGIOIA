namespace PresenzeXmlConverter;

/// <summary>
/// Avanzamento del download di un aggiornamento: con i testi propri
/// dell'aggiornamento.
/// </summary>
internal sealed class UpdateProgressDialog : ProgressDialog
{
    public UpdateProgressDialog(string version)
        : base(
            "Aggiornamento di Vyrn Presence",
            $"Scaricamento di Vyrn Presence {version}",
            "Preparazione del download…")
    {
    }
}
