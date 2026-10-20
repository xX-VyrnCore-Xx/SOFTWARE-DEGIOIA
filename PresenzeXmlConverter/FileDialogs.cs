namespace PresenzeXmlConverter;

/// <summary>
/// Finestre «Apri» e «Salva con nome». Windows ricorda dimensione e posizione di ogni finestra di questo tipo per
/// applicazione: una volta ingrandita a tutto schermo si riapre sempre così, senza poterla spostare. Con un
/// identificativo proprio la finestra riparte con la dimensione normale, si sposta e si ridimensiona a piacere.
/// </summary>
internal static class FileDialogs
{
    public static readonly Guid ClientId = new("6f1c2b7e-4c0a-4e3b-9b52-7d1a8e5f0c91");
}
