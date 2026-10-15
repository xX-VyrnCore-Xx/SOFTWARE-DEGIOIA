using System.Text;
using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public sealed class TokenStorageTests
{
    // «Cifratura» finta (inversione dei byte): basta a provare formato, migrazione e gestione degli errori.
    private static byte[] Flip(byte[] data) => data.Reverse().ToArray();

    [Fact]
    public void ATokenIsStoredWithThePrefixAndNotInPlainText()
    {
        var content = TokenStorage.Encode("ghp_segretissimo", Flip);

        Assert.StartsWith(TokenStorage.Prefix, content);
        Assert.DoesNotContain("ghp_segretissimo", content);
        Assert.Equal("ghp_segretissimo", TokenStorage.Decode(content, Flip, out var upgrade));
        Assert.False(upgrade);
    }

    [Fact]
    public void APlainTextTokenFromAnOlderVersionIsReadAndMarkedForUpgrade()
    {
        Assert.Equal("ghp_vecchio", TokenStorage.Decode("  ghp_vecchio\r\n", Flip, out var upgrade));
        Assert.True(upgrade);
    }

    [Fact]
    public void AnUnreadableEncryptedTokenIsTreatedAsNoToken()
    {
        // Altro utente o file copiato su un altro PC: la decifratura fallisce e non si inventa nulla.
        static byte[] Fail(byte[] _) => throw new System.Security.Cryptography.CryptographicException("altro utente");
        var content = TokenStorage.Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes("xx"));

        Assert.Equal(string.Empty, TokenStorage.Decode(content, Fail, out var upgrade));
        Assert.False(upgrade);
        Assert.Equal(string.Empty, TokenStorage.Decode(TokenStorage.Prefix + "non-base64!!", Flip, out _));
        Assert.Equal(string.Empty, TokenStorage.Decode(null, Flip, out _));
        Assert.Equal(string.Empty, TokenStorage.Decode("   ", Flip, out _));
    }

    [Fact]
    public void WithoutDataProtectionTheTokenStaysUsable()
    {
        static byte[] Unsupported(byte[] _) => throw new PlatformNotSupportedException();

        Assert.Equal("ghp_x", TokenStorage.Encode("ghp_x", Unsupported));
    }

    [Fact]
    public void WritingAndReadingAFileRoundTripsAndUpgradesAPlainFile()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vp-tok-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "github.token");
            File.WriteAllText(path, "ghp_in_chiaro");

            Assert.Equal("ghp_in_chiaro", TokenStorage.Read(path));
            // Dove c'è DPAPI (Windows) il file è stato riscritto cifrato; altrove resta com'è: in entrambi i casi si rilegge uguale.
            Assert.Equal("ghp_in_chiaro", TokenStorage.Read(path));
            if (OperatingSystem.IsWindows())
            {
                Assert.StartsWith(TokenStorage.Prefix, File.ReadAllText(path));
            }
            Assert.Equal(string.Empty, TokenStorage.Read(Path.Combine(folder, "manca.token")));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void TheAuthenticodeCheckNeverThrowsAndRefusesAnUnsignedFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "vp-sign-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllText(path, "non sono un eseguibile firmato");
        try
        {
            var result = AuthenticodeVerifier.Verify(path);

            Assert.NotEqual(0, result);
            Assert.False(UpdateSecurity.IsSignatureIntact(result));
            Assert.NotEqual(0, AuthenticodeVerifier.Verify(Path.Combine(Path.GetTempPath(), "non-esiste-" + Guid.NewGuid().ToString("N"))));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
