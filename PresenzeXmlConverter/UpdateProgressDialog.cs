namespace PresenzeXmlConverter;

/// <summary>
/// Avanzamento del download di un aggiornamento: con i testi propri
/// dell'aggiornamento.
/// </summary>
internal sealed class UpdateProgressDialog : ProgressDialog
{
    public UpdateProgressDialog(string version)
        : base(
            "Aggiornamento di PresenzeFlow",
            $"Scaricamento di PresenzeFlow {version}",
            "Preparazione del download…")
    {
    }
}
