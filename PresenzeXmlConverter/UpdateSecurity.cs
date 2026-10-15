using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>Che cosa fare dell'installer scaricato dopo averne controllato la firma.</summary>
internal enum InstallerVerdict
{
    /// <summary>Firma integra e dello stesso editore dell'app: si può installare.</summary>
    Accept,

    /// <summary>Firma integra ma di un certificato diverso da quello dell'app (rinnovo?): serve la conferma dell'utente.</summary>
    AskUser,

    /// <summary>L'installer non è firmato, mentre l'app lo è.</summary>
    RejectUnsigned,

    /// <summary>Il file è stato modificato dopo la firma, oppure la firma non è valida.</summary>
    RejectTampered
}

/// <summary>Esito della valutazione: cosa fare e il testo da mostrare all'utente.</summary>
internal sealed record InstallerDecision(InstallerVerdict Verdict, string Message);

/// <summary>
/// Regole di sicurezza dell'aggiornamento, come calcolo puro (verificabile con un test): quali indirizzi sono
/// attendibili, dove si scarica l'installer e se la sua firma digitale permette di eseguirlo.
/// </summary>
internal static class UpdateSecurity
{
    /* Codici di WinVerifyTrust (HRESULT). Quelli «integri» provano che il file non è stato toccato dopo la firma
       anche se questo PC non si fida (ancora) del certificato autofirmato: è il caso di un PC dove il certificato
       non è stato installato. Tutto il resto è un rifiuto. */
    internal const int TrustSucceeded = 0;

    internal static readonly int NoSignature = unchecked((int)0x800B0100);

    internal static readonly int BadDigest = unchecked((int)0x80096010);

    internal static readonly int UntrustedRoot = unchecked((int)0x800B0109);

    internal static readonly int ChainingProblem = unchecked((int)0x800B010A);

    internal static readonly int UntrustedTestRoot = unchecked((int)0x800B010D);

    /// <summary>L'indirizzo è un asset della Release del repository atteso, su api.github.com e in HTTPS?</summary>
    public static bool IsTrustedAssetUrl(string? url, string owner, string repository)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }
        return uri.Scheme == Uri.UriSchemeHttps &&
               string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase) &&
               uri.IsDefaultPort &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               uri.AbsolutePath.StartsWith(
                   $"/repos/{owner}/{repository}/releases/assets/",
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>La firma è integra (anche se il certificato non è attendibile su questo PC)?</summary>
    internal static bool IsSignatureIntact(int winVerifyTrustResult) =>
        winVerifyTrustResult == TrustSucceeded ||
        winVerifyTrustResult == UntrustedRoot ||
        winVerifyTrustResult == ChainingProblem ||
        winVerifyTrustResult == UntrustedTestRoot;

    /// <summary>
    /// Decide se l'installer scaricato può essere eseguito. <paramref name="winVerifyTrustResult"/> è l'esito di
    /// WinVerifyTrust sul file, le impronte sono quelle dei certificati con cui sono firmati l'app e l'installer.
    /// </summary>
    public static InstallerDecision EvaluateInstaller(int winVerifyTrustResult, string? runningSigner, string? installerSigner)
    {
        var installerSigned = !string.IsNullOrEmpty(installerSigner);
        if (!installerSigned)
        {
            return string.IsNullOrEmpty(runningSigner)
                ? new InstallerDecision(InstallerVerdict.Accept, "Né l'app né l'installer sono firmati (build di sviluppo).")
                : new InstallerDecision(
                    InstallerVerdict.RejectUnsigned,
                    "L'installer scaricato non è firmato, mentre questa applicazione lo è: aggiornamento annullato per sicurezza.");
        }
        if (!IsSignatureIntact(winVerifyTrustResult))
        {
            return new InstallerDecision(
                InstallerVerdict.RejectTampered,
                winVerifyTrustResult == BadDigest
                    ? "L'installer scaricato è stato modificato dopo la firma: aggiornamento annullato per sicurezza."
                    : $"La firma digitale dell'installer non è valida (codice 0x{winVerifyTrustResult:X8}): aggiornamento annullato per sicurezza.");
        }
        if (!string.IsNullOrEmpty(runningSigner) &&
            !string.Equals(runningSigner, installerSigner, StringComparison.OrdinalIgnoreCase))
        {
            return new InstallerDecision(
                InstallerVerdict.AskUser,
                "L'installer è firmato da un certificato diverso da quello di questa applicazione. " +
                "Succede quando il certificato viene rinnovato, ma potrebbe anche essere un file contraffatto.");
        }
        return new InstallerDecision(InstallerVerdict.Accept, "Firma digitale integra.");
    }

    /// <summary>
    /// Nome (con un identificativo casuale) del file dell'installer scaricato: un nome prevedibile in una cartella
    /// condivisa permetterebbe a un altro programma di piazzarci prima un file suo.
    /// </summary>
    public static string InstallerFileName(Version version, Guid unique) =>
        string.Create(CultureInfo.InvariantCulture, $"PresenzeFlow-Setup-{version.ToString(3)}-{unique:N}.exe");

    /// <summary>Cartella privata dell'utente (non %TEMP%) dove si scarica l'installer.</summary>
    public static string PrivateUpdateDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeGioia",
        "PresenzeXmlConverter",
        "updates");

    /// <summary>I file di installer più vecchi di <paramref name="maxAge"/> nella cartella privata (da eliminare).</summary>
    public static IReadOnlyList<string> StaleInstallers(string directory, DateTime nowUtc, TimeSpan maxAge)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }
        return Directory.GetFiles(directory, "PresenzeFlow-Setup-*.exe")
            .Where(file => nowUtc - File.GetLastWriteTimeUtc(file) > maxAge)
            .ToArray();
    }
}
