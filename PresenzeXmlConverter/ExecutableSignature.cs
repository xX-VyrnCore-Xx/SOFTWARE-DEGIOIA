using System.Security.Cryptography.X509Certificates;

namespace PresenzeXmlConverter;

/// <summary>
/// Firma digitale dell'eseguibile in esecuzione: l'app la mostra nelle
/// Impostazioni, così si vede subito se il file installato è quello firmato
/// (o se qualcuno ha sostituito l'eseguibile con uno non firmato).
/// </summary>
internal static class ExecutableSignature
{
    internal const string NotSignedText = "Eseguibile non firmato digitalmente";

    /// <summary>
    /// Versione dell'eseguibile in esecuzione a tre cifre (es. "1.12.0"), letta
    /// dall'assembly del programma e non da una costante: non può restare
    /// indietro rispetto a quanto è stato pubblicato. La usano il titolo della
    /// finestra e le Impostazioni.
    /// </summary>
    public static string DisplayVersion { get; } =
        typeof(ExecutableSignature).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>
    /// Riga da mostrare: editore e scadenza se il file è firmato, altrimenti una
    /// dicitura esplicita. Non solleva mai eccezioni: la lettura della firma non
    /// deve impedire l'apertura delle Impostazioni.
    /// </summary>
    public static string DescribeCurrentExecutable()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return NotSignedText;
        }

        try
        {
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
            return FormatSigner(certificate.Subject, certificate.NotAfter);
        }
        catch (Exception)
        {
            // Nessuna firma, file illeggibile o sistema che non sa leggerla.
            return NotSignedText;
        }
    }

    /// <summary>Impronta del certificato con cui è firmato il file, o null se non è firmato.</summary>
    public static string? SignerThumbprint(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }
        try
        {
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
            return certificate.Thumbprint;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Un aggiornamento NON firmato che sostituisce un'app firmata è un passo indietro
    /// (o una sostituzione del file): va rifiutato. Un certificato diverso invece è
    /// ammesso, altrimenti il rinnovo del certificato bloccherebbe gli aggiornamenti
    /// di chi ha già installato la versione precedente.
    /// </summary>
    internal static bool IsSignatureDowngrade(string? runningSigner, string? installerSigner) =>
        !string.IsNullOrEmpty(runningSigner) && string.IsNullOrEmpty(installerSigner);

    /// <summary>Testo mostrato all'utente a partire dai dati del certificato.</summary>
    internal static string FormatSigner(string? subject, DateTime? notAfter)
    {
        var publisher = CommonNameOf(subject);
        if (string.IsNullOrWhiteSpace(publisher))
        {
            return NotSignedText;
        }
        return notAfter is null
            ? $"Eseguibile firmato digitalmente da {publisher}"
            : $"Eseguibile firmato digitalmente da {publisher} (valido fino al {notAfter.Value:dd/MM/yyyy})";
    }

    /// <summary>
    /// Nome dell'editore dal subject X.500 ("CN=DeGioia, O=DeGioia, OU=PresenzeFlow"
    /// → "DeGioia"): è l'unica parte che interessa mostrare.
    /// </summary>
    internal static string? CommonNameOf(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }
        foreach (var part in subject.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed[3..].Trim();
                return value.Length == 0 ? null : value;
            }
        }
        return null;
    }
}
