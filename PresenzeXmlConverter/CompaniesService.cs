namespace PresenzeXmlConverter;

/// <summary>
/// L'anagrafica delle aziende senza alcuna finestra: elenco, salvataggio, eliminazione, regole proprie e scelta
/// dell'azienda in uso. Le stesse operazioni della sezione «Aziende» classica, con lo stesso registro attività.
/// </summary>
internal sealed class CompaniesService
{
    private readonly LocalDatabase _database;

    public CompaniesService(LocalDatabase database) => _database = database;

    public IReadOnlyList<CompanyRecord> Load() => _database.GetCompanies();

    /// <summary>Le regole proprie dell'azienda a parole, oppure «generali».</summary>
    public static string DescribeRules(CompanyRecord company) =>
        company.Rules is null ? "generali" : GenerationSettings.Describe(company.Rules.ToOptions(DateTime.Today.Year));

    /// <summary>Crea o aggiorna un'azienda (codice da 1 a 6 caratteri, come da tracciato).</summary>
    public void Save(string code, string name, string notes)
    {
        code = (code ?? string.Empty).Trim();
        if (code.Length is 0 or > 6)
        {
            throw new InvalidDataException("Il codice azienda ufficiale PAGHE deve contenere da 1 a 6 caratteri.");
        }
        _database.SaveCompany(code, name, notes);
        Audit($"Azienda {code} salvata");
    }

    public bool Delete(CompanyRecord company)
    {
        var removed = _database.DeleteCompany(company.Code);
        if (removed)
        {
            Audit($"Azienda {company.Label} eliminata");
        }
        return removed;
    }

    /// <summary>Salva le regole proprie dell'azienda (null = torna alle generali).</summary>
    public void SaveRules(CompanyRecord company, CompanyRules? rules)
    {
        _database.SaveCompanyRules(company.Code, rules);
        Audit(
            $"Regole dell'azienda {company.Code} " + (rules is null ? "tolte: usa le generali" : "salvate"),
            rules is null ? string.Empty : GenerationSettings.Describe(rules.ToOptions(DateTime.Today.Year)));
    }

    /// <summary>Salva le soglie dei controlli dell'azienda (di serie = nessuna soglia propria).</summary>
    public void SaveChecks(CompanyRecord company, ValidationRules rules)
    {
        _database.SaveCompanyChecks(company.Code, rules);
        Audit(
            $"Controlli dell'azienda {company.Code} " + (rules.IsDefault ? "riportati a quelli di serie" : "salvati"),
            rules.IsDefault ? string.Empty : rules.Describe());
    }

    /// <summary>Salva le spettanze annuali di ferie e permessi dell'azienda (vuote = nessun saldo).</summary>
    public void SaveLeave(CompanyRecord company, IReadOnlyList<LeaveRule> rules)
    {
        _database.SaveCompanyLeave(company.Code, rules);
        Audit(
            $"Spettanze dell'azienda {company.Code} " + (rules.Count == 0 ? "tolte" : "salvate"),
            string.Join(" · ", rules.Select(rule => rule.Describe())));
    }

    /// <summary>L'azienda diventa quella della conversione (il codice si ricorda per la prossima volta).</summary>
    public void UseForConversion(CompanyRecord company)
    {
        _database.SaveSetting("company-code", company.Code);
        Audit($"Azienda in uso: {company.Label}");
    }

    private void Audit(string title, string detail = "")
    {
        try
        {
            _database.AddActivity(ActivityKind.Company, title, detail);
        }
        catch (Exception exception) when (
            exception is Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Il registro non è l'operazione.
        }
    }
}
