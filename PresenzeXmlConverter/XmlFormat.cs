using System.Xml;
using System.Xml.Linq;

namespace PresenzeXmlConverter;

/// <summary>
/// Forma del file XML consegnato a Zucchetti: nome dell'elemento radice, spazio dei
/// nomi e modo in cui sono scritti i movimenti. Il tracciato PDF non dice come si chiama
/// la radice né se c'è uno spazio dei nomi, mentre il validatore di Zucchetti li pretende
/// («cvc-elt.1: impossibile trovare la dichiarazione dell'elemento…»): quindi la forma si
/// può cambiare dalle Impostazioni, o si può leggere da un file XML che Zucchetti
/// accetta, senza aspettare una nuova versione dell'app.
/// </summary>
internal sealed record XmlFormat(string RootElement, string Namespace, bool MovementPerNode)
{
    public const string RootSetting = "xml-root";
    public const string NamespaceSetting = "xml-namespace";
    public const string MovementPerNodeSetting = "xml-movement-per-node";

    // Default va dichiarata PRIMA di Active: gli inizializzatori statici girano in ordine
    // di scrittura, e Active = Default letto prima di Default valeva null.
    public static XmlFormat Default { get; } = new(TrriPwXmlWriter.RootElement, string.Empty, false);

    /// <summary>Forma in uso: scelta nelle Impostazioni, altrimenti quella predefinita.</summary>
    public static XmlFormat Active { get; set; } = Default;

    /// <summary>Spazio dei nomi da scrivere, o null quando non c'è.</summary>
    public string? NamespaceOrNull => Namespace.Length == 0 ? null : Namespace;

    public static bool IsValidElementName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }
        try
        {
            XmlConvert.VerifyNCName(name.Trim());
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    public static bool IsValidNamespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme.Length > 0);

    public static XmlFormat Load(LocalDatabase database)
    {
        var root = database.GetSetting(RootSetting);
        var ns = database.GetSetting(NamespaceSetting);
        var perNode = database.GetSetting(MovementPerNodeSetting);
        return new XmlFormat(
            IsValidElementName(root) ? root!.Trim() : Default.RootElement,
            IsValidNamespace(ns) ? (ns ?? string.Empty).Trim() : string.Empty,
            string.Equals(perNode, "true", StringComparison.OrdinalIgnoreCase));
    }

    public void Save(LocalDatabase database)
    {
        database.SaveSetting(RootSetting, RootElement);
        database.SaveSetting(NamespaceSetting, Namespace);
        database.SaveSetting(MovementPerNodeSetting, MovementPerNode ? "true" : "false");
    }

    /// <summary>
    /// Legge la forma da un file XML accettato da Zucchetti (di esempio, o prodotto da
    /// un altro programma di presenze): radice, spazio dei nomi e se ogni movimento è
    /// un nodo «Movimenti» a sé o sta dentro un nodo «Movimenti» come «Movimento».
    /// </summary>
    public static bool TryDetect(string path, out XmlFormat format, out string message)
    {
        format = Default;
        XDocument document;
        try
        {
            document = XmlSafety.Load(path);
        }
        catch (Exception exception) when (exception is XmlException or IOException or UnauthorizedAccessException)
        {
            message = $"Il file non si può leggere come XML: {exception.Message}";
            return false;
        }

        var root = document.Root;
        if (root is null)
        {
            message = "Il file XML è vuoto.";
            return false;
        }

        var perNode = false;
        var movimenti = root.Descendants().FirstOrDefault(element => element.Name.LocalName == "Movimenti");
        if (movimenti is not null)
        {
            perNode = !movimenti.Elements().Any(child => child.Name.LocalName == "Movimento");
        }

        format = new XmlFormat(root.Name.LocalName, root.Name.NamespaceName, perNode);
        message = movimenti is null
            ? $"Radice «{format.RootElement}» letta; nel file non ci sono nodi «Movimenti», quindi la disposizione dei movimenti resta quella attuale."
            : $"Radice «{format.RootElement}»" +
              (format.Namespace.Length > 0 ? $", spazio dei nomi {format.Namespace}" : ", senza spazio dei nomi") +
              (perNode ? "; ogni movimento è un nodo «Movimenti»." : "; i movimenti stanno dentro un nodo «Movimenti».");
        if (movimenti is null)
        {
            format = format with { MovementPerNode = Active.MovementPerNode };
        }
        return true;
    }
}
