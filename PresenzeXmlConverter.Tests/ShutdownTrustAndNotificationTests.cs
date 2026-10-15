using System.Text.RegularExpressions;

namespace PresenzeXmlConverter.Tests;

/// <summary>
/// Uscita dal programma, notifiche e attendibilità del certificato: regole che si
/// verificano leggendo i sorgenti, perché il tray, Windows e certutil non sono
/// eseguibili nei test.
/// </summary>
public class ShutdownTrustAndNotificationTests
{
    [Fact]
    public void TheMainWindowTreatsBothDialogButtonsAsChoicesAndExitsThroughOneRoutine()
    {
        var source = ReadSource("MainForm.cs");
        if (source is null)
        {
            return;
        }

        // Il confronto "!= DialogResult.OK" annullava la chiusura anche per «Esci»:
        // nel punto in cui si apre il dialogo di chiusura non deve più esserci.
        var opened = source.IndexOf("new CloseBehaviorDialog()", StringComparison.Ordinal);
        Assert.True(opened > 0, "Il dialogo di chiusura non è più usato dalla finestra principale.");
        var around = source.Substring(opened, 300);
        Assert.DoesNotContain("!= DialogResult.OK", around, StringComparison.Ordinal);
        Assert.Contains("CloseBehaviorDialog.IsChoice(", around, StringComparison.Ordinal);
        // Il comando del tray e quello del dialogo passano da un'unica uscita, che
        // ferma comunque il ciclo dei messaggi se la chiusura normale non basta.
        Assert.Contains("private void ExitApplication()", source, StringComparison.Ordinal);
        Assert.Contains("Application.Exit();", source, StringComparison.Ordinal);
        Assert.Contains("(_, _) => ExitApplication()", source, StringComparison.Ordinal);
        // La manutenzione alla chiusura non può impedire di chiudere.
        var closing = source[source.IndexOf("protected override void OnFormClosing", StringComparison.Ordinal)..];
        Assert.Matches(new Regex(@"try\s*\{\s*SaveWindowBounds\(\);"), closing);
    }

    [Fact]
    public void NotificationsAreShownWhenTheWindowIsNotInUseNotOnlyWhenItIsHidden()
    {
        var source = ReadSource("MainForm.cs");
        if (source is null)
        {
            return;
        }

        // Prima: "if (Visible) return;" → una finestra ridotta a icona o coperta da
        // altre non riceveva mai la notifica di fine conversione.
        Assert.Contains("Visible && WindowState != FormWindowState.Minimized && ReferenceEquals(Form.ActiveForm, this)", source, StringComparison.Ordinal);
        Assert.Contains("if (IsWindowInUse)", source, StringComparison.Ordinal);
        // L'icona appena registrata non è pronta: la prima notifica si ritarda.
        Assert.Contains("Interval = 600", source, StringComparison.Ordinal);
        // Un clic sulla notifica riporta la finestra davanti (e ne esegue l'azione, se ne ha una).
        Assert.Matches(new Regex(@"BalloonTipClicked\s*\+=\s*\(_, _\)\s*=>\s*\{[^}]*RestoreFromTray\(\);[^}]*\}"), source);
        Assert.Contains("action?.Invoke();", source, StringComparison.Ordinal);
        // Nessuna chiamata diretta a ShowBalloonTip fuori dall'unica routine.
        foreach (var name in new[] { "MainForm.cs", "MainForm.Update.cs", "MainForm.Data.cs", "MainForm.Shell.cs" })
        {
            var text = ReadSource(name)!;
            var calls = Regex.Matches(text, @"\.ShowBalloonTip\(").Count;
            Assert.True(
                calls == (name == "MainForm.cs" ? 1 : 0),
                $"{name}: le notifiche devono passare da ShowTrayNotification ({calls} chiamate dirette).");
        }
    }

    [Fact]
    public void NotificationsHaveAnInWindowChannelAndTheSettingsCanTestThem()
    {
        var notifications = ReadSource("MainForm.Notifications.cs");
        var dialog = ReadSource("SettingsDialog.cs");
        if (notifications is null || dialog is null)
        {
            return;
        }

        // Chi sta lavorando nella finestra non riceveva mai nulla: il cartellino interno compare comunque,
        // il fumetto di Windows e il lampeggio nella barra solo a finestra non in uso.
        Assert.Contains("private void Notify(", notifications, StringComparison.Ordinal);
        Assert.Contains("TaskbarFlash.Flash(this)", notifications, StringComparison.Ordinal);
        Assert.Contains("if (!systemNotification || IsWindowInUse)", notifications, StringComparison.Ordinal);
        // La prova mostra i canali anche con le notifiche spente e le Impostazioni la espongono.
        Assert.Contains("force: true", notifications, StringComparison.Ordinal);
        Assert.Contains("NotificationTestRequested", dialog, StringComparison.Ordinal);
        Assert.Contains("CreatePage(SettingsPageLayout.NotificationRows)", dialog, StringComparison.Ordinal);
        Assert.Contains("ms-settings:notifications", dialog, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStartMenuShortcutCarriesTheSameAppUserModelIdTheAppDeclares()
    {
        var script = ReadInstaller();
        if (script is null)
        {
            return;
        }

        // Senza un collegamento con lo stesso AppUserModelID le notifiche del tray
        // non sono attribuite al programma e Windows può sopprimerle.
        Assert.Contains($"#define AppUserModelId \"{AppIdentity.AppUserModelId}\"", script, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(script, @"AppUserModelID:\s*""\{#AppUserModelId\}""").Count);
    }

    [Fact]
    public void TheInstallerCanTrustThePublisherCertificateForTheCurrentUserOnly()
    {
        var script = ReadInstaller();
        if (script is null)
        {
            return;
        }

        Assert.Contains("Name: \"trustcert\"", script, StringComparison.Ordinal);
        Assert.Contains("-user -addstore -f TrustedPublisher", script, StringComparison.Ordinal);
        Assert.Contains("-user -addstore -f Root", script, StringComparison.Ordinal);
        // Mai a livello di PC (servirebbe l'amministratore) e mai durante un aggiornamento silenzioso.
        Assert.DoesNotContain("-addstore -f TrustedPublisher \"\" ", script, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"if WizardSilent or not WizardIsTaskSelected\('trustcert'\) then\s+Exit;"), script);
        // Si toglie alla disinstallazione, e i simboli sono obbligatori insieme.
        Assert.Contains("-user -delstore TrustedPublisher {#CertThumbprint}", script, StringComparison.Ordinal);
        Assert.Contains("#error \"TrustCertificate richiede anche -dCertThumbprint", script, StringComparison.Ordinal);
        // Il ramo si attiva solo con i simboli: l'installer si compila anche senza certificato.
        Assert.Contains("#ifdef TrustCertificate", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseAndCiBothCompileTheCertificateBranchOfTheInstaller()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            return;
        }

        var release = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        var ci = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        Assert.Contains("-dTrustCertificate=1", release, StringComparison.Ordinal);
        Assert.Contains("-dCertThumbprint=$env:SIGN_CERT_THUMBPRINT", release, StringComparison.Ordinal);
        // La CI lo prova con un certificato usa e getta, mai quello vero.
        Assert.Contains("-dTrustCertificate=1", ci, StringComparison.Ordinal);
        Assert.Contains("New-SelfSignedCertificate", ci, StringComparison.Ordinal);
        Assert.DoesNotContain("CODE_SIGNING_PFX", ci, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExecutableCarriesPublisherMetadata()
    {
        var project = ReadSource("PresenzeXmlConverter.csproj");
        if (project is null)
        {
            return;
        }

        Assert.Contains("<Company>DeGioia</Company>", project, StringComparison.Ordinal);
        Assert.Contains("<Copyright>", project, StringComparison.Ordinal);
    }

    private static string? ReadSource(string fileName)
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            return null;
        }
        var path = Path.Combine(root, "PresenzeXmlConverter", fileName);
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : null;
    }

    private static string? ReadInstaller()
    {
        var root = FindRepositoryRoot();
        return root is null ? null : File.ReadAllText(Path.Combine(root, "Installer.iss")).Replace("\r\n", "\n");
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Installer.iss")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return null;
    }
}
