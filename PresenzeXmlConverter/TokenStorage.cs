using System.Security.Cryptography;
using System.Text;

namespace PresenzeXmlConverter;

/// <summary>
/// Il token GitHub salvato sul disco non sta più in chiaro: si cifra con DPAPI di Windows, legato all'account
/// dell'utente (un altro utente, o chi copia il file su un altro PC, non lo legge). Un token salvato in chiaro
/// dalle versioni precedenti si legge ancora e viene riscritto cifrato alla prima lettura.
/// </summary>
internal static class TokenStorage
{
    internal const string Prefix = "dpapi1:";

    // Entropia fissa dell'applicazione: un'altra app dello stesso utente non legge il token a colpo d'occhio.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Vyrn Presence update token v1");

    /// <summary>Il contenuto da scrivere nel file: il token cifrato con il prefisso (o in chiaro se la cifratura non c'è).</summary>
    internal static string Encode(string token, Func<byte[], byte[]> protect)
    {
        try
        {
            return Prefix + Convert.ToBase64String(protect(Encoding.UTF8.GetBytes(token)));
        }
        catch (Exception exception) when (exception is CryptographicException or PlatformNotSupportedException)
        {
            // Sistema senza DPAPI (non Windows): meglio il token in chiaro che nessun aggiornamento.
            return token;
        }
    }

    /// <summary>
    /// Il token dal contenuto del file. <paramref name="needsUpgrade"/> è vero se era in chiaro (da riscrivere cifrato).
    /// Un contenuto cifrato illeggibile (altro utente, file copiato) vale «nessun token».
    /// </summary>
    internal static string Decode(string? content, Func<byte[], byte[]> unprotect, out bool needsUpgrade)
    {
        needsUpgrade = false;
        var text = (content ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }
        if (!text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            needsUpgrade = true;
            return text;
        }
        try
        {
            return Encoding.UTF8.GetString(unprotect(Convert.FromBase64String(text[Prefix.Length..]))).Trim();
        }
        catch (Exception exception) when (
            exception is CryptographicException or FormatException or PlatformNotSupportedException)
        {
            return string.Empty;
        }
    }

    /// <summary>Legge il token dal file (e, se era in chiaro, lo riscrive cifrato). Stringa vuota se manca o è illeggibile.</summary>
    public static string Read(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return string.Empty;
            }
            var token = Decode(File.ReadAllText(path), UnprotectForCurrentUser, out var needsUpgrade);
            if (needsUpgrade && token.Length > 0)
            {
                Write(path, token);
            }
            return token;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>Scrive il token cifrato.</summary>
    public static void Write(string path, string token) =>
        File.WriteAllText(path, Encode(token, ProtectForCurrentUser), Encoding.UTF8);

    private static byte[] ProtectForCurrentUser(byte[] data) =>
        ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);

    private static byte[] UnprotectForCurrentUser(byte[] data) =>
        ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
}
