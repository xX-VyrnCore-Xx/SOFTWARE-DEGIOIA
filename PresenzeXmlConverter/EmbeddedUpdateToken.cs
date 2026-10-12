using System.Security.Cryptography;
using System.Text;

namespace PresenzeXmlConverter;

/// <summary>
/// Token di sola lettura per le Release, iniettato nella build dalla CI.
///
/// Nei sorgenti i due valori sono vuoti: il workflow di rilascio li riscrive
/// (tools/Inject-UpdateToken.ps1) a partire dal secret PRESENZEFLOW_GITHUB_TOKEN
/// subito prima della compilazione, quindi ogni installer esce con gli
/// aggiornamenti già collegati e nessun token finisce nel repository.
///
/// Il token è soltanto <b>offuscato</b> (XOR con una chiave casuale diversa a
/// ogni build), non cifrato: serve a non lasciarlo in chiaro nell'eseguibile e a
/// evitare che gli scanner di segreti lo individuino, ma chi ha il programma può
/// comunque estrarlo. Per questo deve avere il solo permesso
/// <i>Contents: Read-only</i> sul solo repository delle Release, mai altro.
/// </summary>
internal static class EmbeddedUpdateToken
{
    // Riscritti dalla CI: non modificare a mano.
    private const string Payload = "";
    private const string Key = "";

    private static readonly Lazy<string?> Cached = new(() => Decode(Payload, Key));

    /// <summary>Il token incorporato (null se la build non ne contiene uno).</summary>
    public static string? Resolve() => Cached.Value;

    /// <summary>Questa build porta con sé un token per gli aggiornamenti?</summary>
    public static bool IsPresent => Resolve() is not null;

    /// <summary>Ricava il token da payload e chiave (entrambi Base64); null se mancano o sono illeggibili.</summary>
    internal static string? Decode(string? payload, string? key)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }
        try
        {
            var data = Convert.FromBase64String(payload);
            var mask = Convert.FromBase64String(key);
            if (data.Length == 0 || mask.Length == 0)
            {
                return null;
            }
            for (var index = 0; index < data.Length; index++)
            {
                data[index] ^= mask[index % mask.Length];
            }
            var token = UpdateService.SanitizeToken(Encoding.UTF8.GetString(data));
            return token.Length == 0 ? null : token;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Operazione inversa di <see cref="Decode"/>: la CI la esegue in PowerShell
    /// con lo stesso schema (XOR byte per byte con chiave ciclica, poi Base64).
    /// </summary>
    internal static (string Payload, string Key) Encode(string token, byte[]? key = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var mask = key is { Length: > 0 } ? key : RandomNumberGenerator.GetBytes(32);
        var data = Encoding.UTF8.GetBytes(token.Trim());
        for (var index = 0; index < data.Length; index++)
        {
            data[index] ^= mask[index % mask.Length];
        }
        return (Convert.ToBase64String(data), Convert.ToBase64String(mask));
    }
}
