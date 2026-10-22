using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>Le scelte della pagina Impostazioni, come testo e interruttori (la pagina le legge e le restituisce senza interpretarle).</summary>
internal sealed record SettingsModel(
    string HoursText,
    bool IncludeSaturdays,
    bool AutoDetect,
    bool FillPresence,
    string HolidaysText,
    bool AutoUpdate,
    bool StartWithWindows,
    bool InAppNotifications,
    bool SystemNotifications,
    string CloseBehavior,
    ThemeMode Theme,
    string XmlRoot,
    string XmlNamespace,
    bool XmlPerNode);

/// <summary>Esito del salvataggio: un errore da mostrare (e il campo da evidenziare) oppure un messaggio di conferma.</summary>
internal sealed record SettingsSaveResult(string? Error, string Message, bool ThemeChanged = false)
{
    public bool Succeeded => Error is null;
}

/// <summary>
/// Impostazioni dell'applicazione senza finestre: lettura, controllo e salvataggio degli stessi valori della finestra
/// classica (regole di conversione, aggiornamenti, notifiche, comportamento della X, tema, forma dell'XML) e le azioni
/// sul token e sul backup. La pagina WPF e i test usano questa classe; la finestra classica resta com'era.
/// </summary>
internal sealed class SettingsService
{
    internal const string AutoUpdateSetting = "auto-update-check";
    internal const string NativeNotificationsSetting = "notifications";
    internal const string InAppNotificationsSetting = "in-app-notifications";
    internal const string CloseBehaviorSetting = "close-behavior";

    private readonly ConversionService _conversion;
    private readonly LocalDatabase _database;

    public SettingsService(ConversionService conversion)
    {
        _conversion = conversion;
        _database = conversion.Database;
    }

    /// <summary>L'anno a cui si riferiscono le chiusure aziendali: quello del foglio importato, altrimenti l'anno in corso.</summary>
    public int ReferenceYear => _conversion.Inspection?.PeriodStart?.Year ?? DateTime.Today.Year;

    /// <summary>Se true, è un token valido salvato in questa sessione (la finestra controlla subito gli aggiornamenti).</summary>
    public bool TokenSaved { get; private set; }

    public SettingsModel Load()
    {
        var options = _conversion.GlobalOptions;
        var format = XmlFormat.Active;
        var close = _database.GetSetting(CloseBehaviorSetting) ?? string.Empty;
        return new SettingsModel(
            options.HoursPerDay.ToString("0.##", CultureInfo.CurrentCulture),
            options.IncludeSaturdays,
            options.AutoDetect,
            options.FillPresence,
            GenerationSettings.FormatHolidays(options.ExtraHolidays, ReferenceYear).Replace(", ", Environment.NewLine),
            IsEnabled(AutoUpdateSetting),
            StartupRegistration.IsEnabled(),
            IsEnabled(InAppNotificationsSetting),
            IsEnabled(NativeNotificationsSetting),
            close is "tray" or "exit" ? close : string.Empty,
            ThemePreference.Load(),
            format.RootElement,
            format.Namespace,
            format.MovementPerNode);
    }

    private bool IsEnabled(string key)
    {
        var value = _database.GetSetting(key);
        return value is null || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Controlla i valori senza salvare: il testo dell'errore, oppure null se sono tutti utilizzabili.</summary>
    public string? Validate(SettingsModel model)
    {
        if (!GenerationSettings.TryParseHoursPerDay(model.HoursText, out _))
        {
            return $"Le ore di una giornata devono essere un numero fra {GenerationSettings.MinimumHoursPerDay:0.##} e " +
                   $"{GenerationSettings.MaximumHoursPerDay:0.##} (es. 8 oppure 7,5).";
        }
        GenerationSettings.ParseHolidaysDetailed(model.HolidaysText, ReferenceYear, out var ignored);
        if (ignored.Count > 0)
        {
            return $"Date non riconosciute e ignorate: {string.Join(", ", ignored.Take(4))}. Usa il formato gg/mm oppure gg/mm/aaaa.";
        }
        if (!XmlFormat.IsValidElementName(model.XmlRoot))
        {
            return "Il nome dell'elemento radice non è valido: usa lettere, cifre, trattino o underscore, senza spazi.";
        }
        if (!XmlFormat.IsValidNamespace(model.XmlNamespace))
        {
            return "Lo spazio dei nomi deve essere un indirizzo completo (es. http://…) oppure vuoto.";
        }
        return null;
    }

    /// <summary>Controlla e salva tutto, come «Salva impostazioni» della finestra classica.</summary>
    public SettingsSaveResult Save(SettingsModel model)
    {
        if (Validate(model) is { } error)
        {
            return new SettingsSaveResult(error, string.Empty);
        }
        GenerationSettings.TryParseHoursPerDay(model.HoursText, out var hours);
        var holidays = GenerationSettings.ParseHolidaysDetailed(model.HolidaysText, ReferenceYear, out _);

        if (model.StartWithWindows != StartupRegistration.IsEnabled() && !StartupRegistration.Set(model.StartWithWindows))
        {
            return new SettingsSaveResult("Windows non ha permesso di cambiare l'avvio automatico: riprova o lascia la spunta com'era.", string.Empty);
        }

        var previous = _conversion.GlobalOptions;
        var options = previous with
        {
            HoursPerDay = hours,
            IncludeSaturdays = model.IncludeSaturdays,
            ExtraHolidays = holidays ?? [],
            AutoDetect = model.AutoDetect,
            FillPresence = model.FillPresence
        };
        GenerationSettings.Save(_database, options, ReferenceYear);
        _conversion.ApplyGlobalOptions(options);

        _database.SaveSetting(AutoUpdateSetting, model.AutoUpdate ? "true" : "false");
        _database.SaveSetting(InAppNotificationsSetting, model.InAppNotifications ? "true" : "false");
        _database.SaveSetting(NativeNotificationsSetting, model.SystemNotifications ? "true" : "false");
        _database.SaveSetting(CloseBehaviorSetting, model.CloseBehavior is "tray" or "exit" ? model.CloseBehavior : string.Empty);

        var themeChanged = model.Theme != ThemePreference.Load();
        var themeFailed = themeChanged && !ThemePreference.Save(model.Theme);
        if (themeFailed)
        {
            themeChanged = false;
        }

        var format = new XmlFormat(model.XmlRoot.Trim(), model.XmlNamespace.Trim(), model.XmlPerNode);
        var formatChanged = format != XmlFormat.Active;
        XmlFormat.Active = format;
        format.Save(_database);
        if (formatChanged)
        {
            _conversion.InvalidateGenerated();
        }

        var equivalent = GenerationSettings.AreEquivalent(previous, options);
        if (equivalent)
        {
            Audit("Impostazioni salvate", GenerationSettings.Describe(options));
        }
        var message = themeFailed
            ? "Impostazioni salvate, ma la scelta del tema non si è potuta salvare: resta quello di prima."
            : themeChanged
                ? "Impostazioni salvate. Il tema cambia al prossimo avvio: chiudi e riapri il programma."
                : equivalent
                    ? "Impostazioni salvate."
                    : "Impostazioni salvate: rigenera l'anteprima per applicare le regole di conversione.";
        return new SettingsSaveResult(null, message, themeChanged);
    }

    /// <summary>Copia la forma di un XML accettato da Zucchetti (radice, spazio dei nomi, disposizione) senza salvarla.</summary>
    public static bool TryDetectFormat(string path, out XmlFormat format, out string message) =>
        XmlFormat.TryDetect(path, out format, out message);

    /// <summary>Verifica il token su GitHub e, se valido, lo salva e lo attiva subito.</summary>
    public async Task<(bool Ok, string Message)> VerifyTokenAsync(string token)
    {
        var candidate = UpdateService.SanitizeToken(token);
        if (candidate.Length == 0)
        {
            return (false, "Inserisci prima il token nel campo qui sopra.");
        }
        try
        {
            var result = await UpdateService.ValidateTokenAsync(candidate);
            if (!result.Succeeded)
            {
                return (false, result.Message);
            }
            if (!UpdateService.TrySaveToken(candidate, out var error))
            {
                return (false, error);
            }
            TokenSaved = true;
            return (true, $"Token verificato, salvato e attivo da subito. {result.Message}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return (false, $"Verifica non riuscita: {exception.Message}");
        }
    }

    public (bool Ok, string Message) ClearToken()
    {
        if (!UpdateService.TryClearToken(out var error))
        {
            return (false, error);
        }
        TokenSaved = true;
        return (true, EmbeddedUpdateToken.IsPresent
            ? "Token personale rimosso: gli aggiornamenti continuano con il token incluso nell'app."
            : "Token rimosso: gli aggiornamenti automatici resteranno disattivati finché non ne inserisci uno nuovo.");
    }

    public string TokenStatus() =>
        $"Stato attuale: {UpdateService.DescribeTokenSource()}. Il file del token è {UpdateService.TokenFilePath}";

    public string CurrentToken() => UpdateService.ResolveUserToken() ?? string.Empty;

    /// <summary>Crea un backup dei dati e dice come si chiama.</summary>
    public string BackupNow() => $"Backup creato: {Path.GetFileName(_database.CreateBackup("manuale"))}";

    /// <summary>Percorsi e riassunti per la scheda «Generale».</summary>
    public GeneralInfo BuildGeneralInfo()
    {
        var folder = Path.GetDirectoryName(_database.DatabasePath) ?? string.Empty;
        string database;
        var codes = string.Empty;
        try
        {
            database = _database.GetHealth().Describe(DateTime.UtcNow);
            var backup = CodesBackup.Read(_database, DateTime.UtcNow);
            var copies = CodesAutoBackup.List(CodesAutoBackup.FolderFor(_database));
            codes = backup.Count == 0
                ? "Codici: nessuno ancora"
                : $"Codici: {backup.Describe()}" + (copies.Count > 0 ? $" · {copies.Count} copie automatiche" : string.Empty);
        }
        catch (Exception exception) when (exception is Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            database = "stato dei dati non leggibile";
        }
        return new GeneralInfo(folder, database, codes, CodesAutoBackup.FolderFor(_database));
    }

    private void Audit(string title, string detail)
    {
        try
        {
            _database.AddActivity(ActivityKind.Settings, title, detail);
        }
        catch (Exception exception) when (
            exception is Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Il registro non è l'operazione.
        }
    }
}
