using System.Diagnostics;

namespace PresenzeXmlConverter;

/// <summary>Come presentare un aggiornamento trovato: niente, un avviso discreto, oppure la finestra con le novità.</summary>
internal enum UpdateAnnouncement
{
    /// <summary>Solo lo stato (versione già rimandata dall'utente).</summary>
    Silent,

    /// <summary>Cartellino, fumetto e lampeggio: niente finestre a sorpresa.</summary>
    Notify,

    /// <summary>La finestra con le novità e il pulsante per aggiornare.</summary>
    ShowDialog
}

/// <summary>Cosa raccontare dopo un aggiornamento: niente, le note già salvate, oppure quelle lette dalla Release.</summary>
internal enum WhatsNewKind
{
    None,
    StoredNotes,
    FetchedNotes
}

internal sealed record WhatsNewPlan(WhatsNewKind Kind, string? Notes, bool ClearPending);

/// <summary>
/// Le decisioni degli aggiornamenti, senza finestre né rete (la finestra classica e la nuova usano le stesse): quando mostrare
/// la finestra e quando basta un avviso, quando raccontare le novità, quando riprovare il controllo.
/// </summary>
internal static class UpdateWorkflow
{
    public const string AutoUpdateSetting = "auto-update-check";
    public const string SkippedVersionSetting = "skipped-update-version";
    public const string PendingUpdateVersionSetting = "pending-update-version";
    public const string PendingUpdateNotesSetting = "pending-update-notes";
    public const string LastRunVersionSetting = "last-run-version";
    public const string UpdateEndpointSetting = "update-endpoint";
    public const string UpdateNoticeSetting = "update-notice";

    /// <summary>
    /// Un aggiornamento rimandato dall'utente non si ripropone a ogni avvio (ma resta nel menu «Cerca aggiornamenti»);
    /// con la finestra nascosta o in secondo piano, o a programma già in uso dopo il primo controllo, niente dialoghi a sorpresa.
    /// </summary>
    public static UpdateAnnouncement Announce(
        UpdateInfo update,
        bool userInitiated,
        string? skippedVersion,
        bool windowInUse,
        bool startupCheckDone)
    {
        if (!userInitiated && string.Equals(skippedVersion, update.Version.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return UpdateAnnouncement.Silent;
        }
        if (!userInitiated && (!windowInUse || startupCheckDone))
        {
            return UpdateAnnouncement.Notify;
        }
        return UpdateAnnouncement.ShowDialog;
    }

    /// <summary>
    /// Le novità dopo un aggiornamento. Con un aggiornamento fatto dall'app si mostrano le note salvate prima del riavvio (nessuna rete);
    /// con uno fatto fuori dall'app si prova a leggerle dalla Release; al primo avvio non c'è nulla da raccontare.
    /// </summary>
    public static WhatsNewPlan PlanWhatsNew(Version current, string? lastRunVersion, string? pendingVersion, string? pendingNotes)
    {
        var pending = UpdateService.ParseVersion(pendingVersion);
        // Si confronta con la normalizzazione delle versioni (1.65.0 e 1.65.0.0 sono la stessa): il confronto diretto le distingue.
        if (pending is not null && !UpdateService.IsNewer(pending, current))
        {
            return string.IsNullOrWhiteSpace(pendingNotes)
                ? Fallback(current, lastRunVersion, clearPending: true)
                : new WhatsNewPlan(WhatsNewKind.StoredNotes, pendingNotes, ClearPending: true);
        }
        return Fallback(current, lastRunVersion, clearPending: false);
    }

    private static WhatsNewPlan Fallback(Version current, string? lastRunVersion, bool clearPending)
    {
        if (lastRunVersion is null)
        {
            return new WhatsNewPlan(WhatsNewKind.None, null, clearPending);
        }
        var previous = UpdateService.ParseVersion(lastRunVersion);
        return previous is null || !UpdateService.IsNewer(current, previous)
            ? new WhatsNewPlan(WhatsNewKind.None, null, clearPending)
            : new WhatsNewPlan(WhatsNewKind.FetchedNotes, null, clearPending);
    }

    /// <summary>Il testo di riserva se le note della Release non si possono leggere.</summary>
    public static string GenericNotes(Version current) =>
        $"Vyrn Presence è stato aggiornato alla versione {current.ToString(3)}. " +
        "Le novità complete sono descritte nella Release su GitHub.";

    /// <summary>Errore in una riga sola, adatto alla barra di stato.</summary>
    public static string SummarizeError(string error)
    {
        var firstLine = error.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? error;
        firstLine = firstLine.Trim();
        return firstLine.Length <= 80 ? firstLine : firstLine[..77] + "…";
    }

    /// <summary>True quando il controllo è fallito per un problema di token, non di rete.</summary>
    public static bool NeedsTokenAttention(string error) =>
        error.Contains("privato", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("token", StringComparison.OrdinalIgnoreCase);

    public static Version RunningVersion =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
}

/// <summary>Le scelte che il download di un aggiornamento chiede a chi lo mostra (finestra di avanzamento e conferma di un altro firmatario).</summary>
internal interface IUpdateInstallUi
{
    CancellationToken Token { get; }

    void SetStep(UpdateStep step);

    void SetDetail(string text);

    void SetTransfer(TransferMeter meter);

    /// <summary>L'installer è firmato da un certificato diverso dal programma in uso: installare lo stesso?</summary>
    bool ConfirmOtherSigner(string message, string currentSigner, string installerSigner);
}

/// <summary>Gli ingressi esterni del download, sostituibili nei test (rete, firma, avvio dell'installer).</summary>
internal sealed class UpdateInstallerEnvironment
{
    public Func<UpdateInfo, string, IProgress<(long Received, long Total)>, CancellationToken, Task> Download { get; init; } =
        (update, destination, transfer, token) => UpdateService.DownloadAsync(update, destination, null, token, transfer);

    public Func<UpdateInfo, Task<string?>> GetChecksum { get; init; } = update => UpdateService.TryGetChecksumAsync(update);

    public Func<string, string, bool> VerifySha256 { get; init; } = UpdateService.VerifySha256;

    public Func<string, int> VerifySignature { get; init; } = AuthenticodeVerifier.Verify;

    public Func<string?> RunningSigner { get; init; } = () => ExecutableSignature.SignerThumbprint(Environment.ProcessPath);

    public Func<string, string?> FileSigner { get; init; } = ExecutableSignature.SignerThumbprint;

    public Func<string> DescribeCurrent { get; init; } = ExecutableSignature.DescribeCurrentExecutable;

    public Func<string, string> DescribeFile { get; init; } = ExecutableSignature.DescribeFile;

    public Action<string> StartInstaller { get; init; } = UpdateInstaller.StartInstallerProcess;

    public Func<string> Directory { get; init; } = UpdateSecurity.PrivateUpdateDirectory;
}

/// <summary>
/// Scarica, controlla e avvia l'installer di un aggiornamento: cartella privata e nome casuale, checksum SHA-256 della Release
/// (obbligatorio se dichiarato), firma Authenticode, file tenuto aperto fra la verifica e l'avvio. Stesso percorso della finestra
/// classica, senza finestre: chi lo chiama mostra l'avanzamento tramite <see cref="IUpdateInstallUi"/>.
/// </summary>
internal static class UpdateInstaller
{
    public static async Task InstallAsync(UpdateInfo update, LocalDatabase database, IUpdateInstallUi ui, UpdateInstallerEnvironment? environment = null)
    {
        environment ??= new UpdateInstallerEnvironment();
        // Cartella privata dell'utente e nome casuale: in %TEMP% con un nome prevedibile un altro programma potrebbe
        // piazzare (o sostituire) il file prima che venga eseguito.
        var updateDirectory = environment.Directory();
        System.IO.Directory.CreateDirectory(updateDirectory);
        foreach (var stale in UpdateSecurity.StaleInstallers(updateDirectory, DateTime.UtcNow, TimeSpan.FromDays(1)))
        {
            TryDelete(stale);
        }
        var destination = Path.Combine(updateDirectory, UpdateSecurity.InstallerFileName(update.Version, Guid.NewGuid()));
        var clock = Stopwatch.StartNew();
        try
        {
            ui.SetStep(UpdateStep.Download);
            var meter = new TransferMeter();
            await environment.Download(
                update,
                destination,
                new Progress<(long Received, long Total)>(transfer =>
                {
                    if (transfer.Total <= 0 || transfer.Received <= 0)
                    {
                        // Segnale di nuovo tentativo dopo una rete interrotta: si riparte con misure pulite.
                        meter = new TransferMeter();
                        clock.Restart();
                        ui.SetDetail("Connessione interrotta: nuovo tentativo…");
                        return;
                    }
                    meter.Add(transfer.Received, transfer.Total, clock.Elapsed);
                    ui.SetTransfer(meter);
                }),
                ui.Token);

            ui.SetStep(UpdateStep.Checksum);
            var expected = await environment.GetChecksum(update);
            if (expected is null && update.ChecksumApiUrl is not null)
            {
                // La Release dichiara un checksum: non poterlo leggere non è un motivo per installare un file non verificato.
                TryDelete(destination);
                throw new InvalidDataException(
                    "Impossibile leggere il checksum della Release: aggiornamento annullato per sicurezza. Riprova fra poco.");
            }
            if (expected is not null && !environment.VerifySha256(destination, expected))
            {
                TryDelete(destination);
                throw new InvalidDataException("Il file scaricato non corrisponde al checksum della Release: aggiornamento annullato.");
            }

            // Da qui il file è tenuto aperto in lettura senza condivisione in scrittura: nessuno può sostituirlo
            // fra la verifica e l'avvio dell'installer.
            using var locked = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
            ui.SetStep(UpdateStep.Signature);
            var decision = UpdateSecurity.EvaluateInstaller(
                environment.VerifySignature(destination),
                environment.RunningSigner(),
                environment.FileSigner(destination));
            if (decision.Verdict is InstallerVerdict.RejectUnsigned or InstallerVerdict.RejectTampered)
            {
                locked.Dispose();
                TryDelete(destination);
                throw new InvalidDataException(decision.Message);
            }
            if (decision.Verdict == InstallerVerdict.AskUser &&
                !ui.ConfirmOtherSigner(decision.Message, environment.DescribeCurrent(), environment.DescribeFile(destination)))
            {
                locked.Dispose();
                TryDelete(destination);
                throw new InvalidDataException("Aggiornamento annullato: l'installer è firmato da un certificato diverso.");
            }

            // Le note vengono salvate prima del riavvio: dopo l'aggiornamento la finestra «Novità» le mostra senza rete.
            database.SaveSetting(UpdateWorkflow.PendingUpdateVersionSetting, update.Version.ToString());
            database.SaveSetting(UpdateWorkflow.PendingUpdateNotesSetting, update.Notes);

            ui.SetStep(UpdateStep.Install);
            try
            {
                database.AddActivity(ActivityKind.Update, $"Aggiornamento alla {update.Version.ToString(3)} avviato", "Firma e checksum verificati");
            }
            catch (Exception exception) when (exception is Microsoft.Data.Sqlite.SqliteException or IOException)
            {
                // Il registro non è l'operazione.
            }
            environment.StartInstaller(destination);
        }
        catch
        {
            // Annullamento o errore: il file scaricato non serve più (l'installer avviato non arriva mai qui).
            TryDelete(destination);
            throw;
        }
    }

    public static void StartInstallerProcess(string installerPath)
    {
        var start = new ProcessStartInfo(installerPath)
        {
            UseShellExecute = true,
            Arguments = "/SILENT /NORESTART /CLOSEAPPLICATIONS"
        };
        if (Process.Start(start) is null)
        {
            throw new InvalidOperationException("Windows non ha avviato l'installer di aggiornamento.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
