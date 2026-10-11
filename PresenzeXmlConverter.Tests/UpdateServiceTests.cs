using System.Security.Cryptography;

namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Verifica la logica degli aggiornamenti che non dipende dalla rete:
/// interpretazione del tag, confronto versioni e controllo del checksum.
/// </summary>
public class UpdateServiceTests
{
    [Theory]
    [InlineData("v1.8.0", "1.8.0.0")]
    [InlineData("1.7", "1.7.0.0")]
    [InlineData("V2.0.1.5", "2.0.1.5")]
    public void VersionTagsAreParsedAndNormalized(string tag, string expected)
    {
        Assert.Equal(Version.Parse(expected), UpdateService.ParseVersion(tag));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ultima")]
    [InlineData("release-1.8.0")]
    public void UnrecognizedTagsProduceNoVersion(string tag)
    {
        Assert.Null(UpdateService.ParseVersion(tag));
    }

    [Theory]
    [InlineData("1.8.0", "1.7.0", true)]
    [InlineData("1.8", "1.8.0.0", false)]
    [InlineData("1.7.0", "1.7.0", false)]
    [InlineData("1.6.9", "1.7.0", false)]
    [InlineData("2.0.0", "1.9.9", true)]
    public void VersionComparisonIgnoresMissingComponents(string candidate, string current, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsNewer(Version.Parse(candidate), Version.Parse(current)));
    }

    [Fact]
    public void ChecksumVerificationAcceptsTheRightFileAndRejectsAnother()
    {
        var path = Path.Combine(Path.GetTempPath(), $"presenzeflow-sha-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllText(path, "contenuto di prova");
            using (var stream = File.OpenRead(path))
            {
                var expected = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                Assert.True(UpdateService.VerifySha256(path, expected));
                Assert.True(UpdateService.VerifySha256(path, expected.ToUpperInvariant()));
            }
            Assert.False(UpdateService.VerifySha256(path, new string('0', 64)));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
