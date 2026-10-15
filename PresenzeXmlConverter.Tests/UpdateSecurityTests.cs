using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public sealed class UpdateSecurityTests
{
    private const string Owner = "xX-VyrnCore-Xx";
    private const string Repo = "SOFTWARE-DEGIOIA";

    [Theory]
    [InlineData("https://api.github.com/repos/xX-VyrnCore-Xx/SOFTWARE-DEGIOIA/releases/assets/123", true)]
    [InlineData("https://API.GITHUB.COM/repos/xx-vyrncore-xx/software-degioia/releases/assets/1", true)]
    [InlineData("http://api.github.com/repos/xX-VyrnCore-Xx/SOFTWARE-DEGIOIA/releases/assets/123", false)]
    [InlineData("https://evil.example.com/repos/xX-VyrnCore-Xx/SOFTWARE-DEGIOIA/releases/assets/123", false)]
    [InlineData("https://api.github.com.evil.com/repos/xX-VyrnCore-Xx/SOFTWARE-DEGIOIA/releases/assets/1", false)]
    [InlineData("https://api.github.com/repos/altro/SOFTWARE-DEGIOIA/releases/assets/123", false)]
    [InlineData("https://api.github.com/repos/xX-VyrnCore-Xx/altro/releases/assets/123", false)]
    [InlineData("https://api.github.com/repos/xX-VyrnCore-Xx/SOFTWARE-DEGIOIA/zipball/main", false)]
    [InlineData("https://user:pass@api.github.com/repos/xX-VyrnCore-Xx/SOFTWARE-DEGIOIA/releases/assets/1", false)]
    [InlineData("https://api.github.com:8443/repos/xX-VyrnCore-Xx/SOFTWARE-DEGIOIA/releases/assets/1", false)]
    [InlineData("", false)]
    [InlineData("non un indirizzo", false)]
    public void OnlyAssetsOfTheExpectedRepositoryOnGitHubOverHttpsAreTrusted(string url, bool expected) =>
        Assert.Equal(expected, UpdateSecurity.IsTrustedAssetUrl(url, Owner, Repo));

    [Fact]
    public void ASignedInstallerWithAnIntactSignatureAndTheSameSignerIsAccepted()
    {
        Assert.Equal(InstallerVerdict.Accept, UpdateSecurity.EvaluateInstaller(0, "AA11", "aa11").Verdict);
        // Certificato autofirmato non (ancora) attendibile su questo PC, ma file integro: si accetta.
        Assert.Equal(InstallerVerdict.Accept, UpdateSecurity.EvaluateInstaller(UpdateSecurity.UntrustedRoot, "AA11", "AA11").Verdict);
        Assert.Equal(InstallerVerdict.Accept, UpdateSecurity.EvaluateInstaller(UpdateSecurity.ChainingProblem, "AA11", "AA11").Verdict);
    }

    [Fact]
    public void ATamperedInstallerIsRefusedWhateverTheSigner()
    {
        var decision = UpdateSecurity.EvaluateInstaller(UpdateSecurity.BadDigest, "AA11", "AA11");
        Assert.Equal(InstallerVerdict.RejectTampered, decision.Verdict);
        Assert.Contains("modificato", decision.Message);

        // Qualsiasi altro codice (revoca, firma non valida…) è un rifiuto.
        Assert.Equal(InstallerVerdict.RejectTampered, UpdateSecurity.EvaluateInstaller(unchecked((int)0x800B0004), "AA11", "AA11").Verdict);
        Assert.Equal(InstallerVerdict.RejectTampered, UpdateSecurity.EvaluateInstaller(unchecked((int)0x80004005), "AA11", "BB22").Verdict);
    }

    [Fact]
    public void AnUnsignedInstallerReplacingASignedAppIsRefused()
    {
        Assert.Equal(InstallerVerdict.RejectUnsigned, UpdateSecurity.EvaluateInstaller(UpdateSecurity.NoSignature, "AA11", null).Verdict);
        Assert.Equal(InstallerVerdict.RejectUnsigned, UpdateSecurity.EvaluateInstaller(UpdateSecurity.NoSignature, "AA11", "").Verdict);
        // Build di sviluppo (né l'app né l'installer firmati): nessun rifiuto.
        Assert.Equal(InstallerVerdict.Accept, UpdateSecurity.EvaluateInstaller(UpdateSecurity.NoSignature, null, null).Verdict);
    }

    [Fact]
    public void ADifferentSignerAsksTheUserInsteadOfDecidingAlone()
    {
        var decision = UpdateSecurity.EvaluateInstaller(0, "AA11", "BB22");
        Assert.Equal(InstallerVerdict.AskUser, decision.Verdict);
        Assert.Contains("rinnovato", decision.Message);
        // Il rinnovo di un app non firmata (prima installazione firmata) non chiede nulla.
        Assert.Equal(InstallerVerdict.Accept, UpdateSecurity.EvaluateInstaller(0, null, "BB22").Verdict);
    }

    [Fact]
    public void InstallerNamesAreUnpredictableAndDirectoryIsPrivate()
    {
        var first = UpdateSecurity.InstallerFileName(new Version(1, 35, 0), Guid.NewGuid());
        var second = UpdateSecurity.InstallerFileName(new Version(1, 35, 0), Guid.NewGuid());
        Assert.NotEqual(first, second);
        Assert.StartsWith("PresenzeFlow-Setup-1.35.0-", first);
        Assert.EndsWith(".exe", first);

        var directory = UpdateSecurity.PrivateUpdateDirectory();
        Assert.DoesNotContain(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), directory + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("PresenzeXmlConverter", "updates"), directory);
    }

    [Fact]
    public void OnlyOldInstallersAreReportedAsStale()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vp-upd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var old = Path.Combine(folder, "PresenzeFlow-Setup-1.0.0-old.exe");
            var recent = Path.Combine(folder, "PresenzeFlow-Setup-1.0.1-new.exe");
            var other = Path.Combine(folder, "altro.exe");
            File.WriteAllText(old, "x");
            File.WriteAllText(recent, "x");
            File.WriteAllText(other, "x");
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-3));
            File.SetLastWriteTimeUtc(other, DateTime.UtcNow.AddDays(-9));

            var stale = UpdateSecurity.StaleInstallers(folder, DateTime.UtcNow, TimeSpan.FromDays(1));

            Assert.Equal([old], stale);
            Assert.Empty(UpdateSecurity.StaleInstallers(Path.Combine(folder, "manca"), DateTime.UtcNow, TimeSpan.FromDays(1)));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ACertificateNearExpiryIsReportedAndAFarOneIsNot()
    {
        var now = new DateTime(2026, 10, 15);
        Assert.Null(ExecutableSignature.ExpiryNotice(null, now));
        Assert.Null(ExecutableSignature.ExpiryNotice(now.AddDays(400), now));
        Assert.Contains("scade fra 30 giorni", ExecutableSignature.ExpiryNotice(now.AddDays(30).AddHours(3), now));
        Assert.Contains("scaduto", ExecutableSignature.ExpiryNotice(now.AddDays(-1), now));
    }
}
