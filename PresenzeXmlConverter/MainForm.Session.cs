using Microsoft.Data.Sqlite;

namespace PresenzeXmlConverter;

/// <summary>
/// Salvataggio e ripristino del lavoro in corso: il file importato, l'anteprima generata (con le correzioni
/// fatte a mano) e se l'XML è già stato esportato. Si salva da solo poco dopo ogni cambiamento e alla chiusura,
/// e all'avvio il programma riporta tutto com'era.
/// </summary>
internal sealed partial class MainForm
{
    private readonly System.Windows.Forms.Timer _sessionSaveTimer = new() { Interval = 2000 };
    private bool _sessionDirty;
    private bool _restoringSession;
    private bool _sessionTimerWired;

    // Finché il ripristino non è stato tentato non si salva nulla: l'avvio azzera l'anteprima e un salvataggio
    // in quel momento cancellerebbe proprio il lavoro da riportare.
    private bool _sessionReady;

    /// <summary>Segnala che il lavoro è cambiato: si salverà fra un paio di secondi, dopo l'ultima modifica.</summary>
    private void MarkSessionDirty()
    {
        if (_restoringSession)
        {
            return;
        }
        _sessionDirty = true;
        if (!_sessionTimerWired)
        {
            _sessionTimerWired = true;
            _sessionSaveTimer.Tick += (_, _) =>
            {
                _sessionSaveTimer.Stop();
                try
                {
                    SaveSession();
                }
                catch (Exception exception) when (exception is SqliteException or IOException)
                {
                    // Salvataggio di sicurezza: riproverà alla prossima modifica.
                }
            };
        }
        _sessionSaveTimer.Stop();
        _sessionSaveTimer.Start();
    }

    private void SaveSession()
    {
        if (!_sessionDirty || _restoringSession || !_sessionReady)
        {
            return;
        }
        _sessionDirty = false;
        if (_sourcePath is null && _movements.Count == 0)
        {
            _database.SaveSetting(SessionState.SettingKey, string.Empty);
            return;
        }
        var snapshot = SessionState.Create(
            _sourcePath,
            _inspection?.SheetName,
            _companyCode.Value.Trim(),
            _currentPreviewExported && _movements.Count > 0,
            _movements,
            _generationWarnings);
        _database.SaveSetting(SessionState.SettingKey, SessionState.Serialize(snapshot));
    }

    /// <summary>Dimentica il lavoro salvato: alla prossima apertura il programma parte pulito.</summary>
    private void ForgetSavedSession()
    {
        _sessionDirty = false;
        _sessionSaveTimer.Stop();
        _database.SaveSetting(SessionState.SettingKey, string.Empty);
        SetStatus("●   Lavoro salvato dimenticato: alla prossima apertura il programma riparte da zero.", false);
    }

    /// <summary>
    /// Riporta il lavoro lasciato aperto: rilegge il file sorgente e, se non è cambiato (o non c'è più),
    /// ripristina l'anteprima con le correzioni fatte. Se il file è cambiato l'anteprima salvata non vale
    /// più e si riparte dal file.
    /// </summary>
    private async Task RestoreSessionAsync()
    {
        if (IsDisposed || _sourcePath is not null || _movements.Count > 0)
        {
            _sessionReady = true;
            return;
        }
        string? saved;
        try
        {
            saved = _database.GetSetting(SessionState.SettingKey);
        }
        catch (SqliteException)
        {
            _sessionReady = true;
            return;
        }
        if (!SessionState.TryParse(saved, out var snapshot))
        {
            _sessionReady = true;
            return;
        }

        _restoringSession = true;
        try
        {
            var status = SessionState.CheckSource(snapshot);
            var fileLoaded = false;
            if (status is SessionState.SourceStatus.Same or SessionState.SourceStatus.Changed)
            {
                fileLoaded = await LoadWorkbookAsync(snapshot.SourcePath!, snapshot.SheetName, restoring: true);
            }
            if (IsDisposed)
            {
                return;
            }

            var restorePreview = snapshot.Movements.Count > 0 &&
                                 status is SessionState.SourceStatus.Same or SessionState.SourceStatus.Missing or SessionState.SourceStatus.None;
            if (restorePreview)
            {
                restorePreview = RestorePreview(snapshot);
            }

            if (!fileLoaded && !restorePreview)
            {
                return;
            }
            ShowSection(SectionConversion);
            var fileName = snapshot.SourcePath is null ? "lavoro precedente" : Path.GetFileName(snapshot.SourcePath);
            if (restorePreview)
            {
                Notify(
                    ToastKind.Success,
                    "Lavoro ripristinato",
                    $"{fileName} · {_movements.Count:N0} movimenti con le tue correzioni" +
                    (snapshot.Exported ? " · già esportato" : string.Empty) +
                    (status == SessionState.SourceStatus.Missing ? " · il file sorgente non si trova più" : string.Empty) + ".");
                SetStatus($"●   Ripristinato il lavoro di {snapshot.SavedAt:dd/MM HH:mm}.", false);
            }
            else
            {
                Notify(
                    ToastKind.Info,
                    "File riaperto",
                    status == SessionState.SourceStatus.Changed
                        ? $"{fileName} è stato modificato dopo l'ultimo salvataggio: crea di nuovo l'anteprima."
                        : $"{fileName} è pronto: crea l'anteprima.");
            }
        }
        finally
        {
            _restoringSession = false;
            _sessionReady = true;
            // Quanto ripristinato è già salvato: niente da riscrivere finché non cambia qualcosa.
            _sessionDirty = false;
            _sessionSaveTimer.Stop();
        }
    }

    private bool RestorePreview(SessionSnapshot snapshot)
    {
        try
        {
            var movements = SessionState.ToMovements(snapshot);
            var companyCode = snapshot.CompanyCode.Length > 0 ? snapshot.CompanyCode : _companyCode.Value.Trim();
            if (companyCode.Length is 0 or > 6)
            {
                return false;
            }
            var xml = TrriPwXmlWriter.Create(companyCode, movements);
            _movements = movements;
            _xml = xml;
            _currentPreviewExported = snapshot.Exported;
            PopulatePreview(movements);
            ShowXmlPreview(xml);
            UpdatePreviewSummary(movements);
            SetGenerationWarnings(snapshot.Warnings);
            RunDeterministicChecks(movements);
            UpdateButtons();
            RefreshDashboard();
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}
