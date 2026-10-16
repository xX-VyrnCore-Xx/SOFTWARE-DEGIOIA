namespace PresenzeXmlConverter;

internal enum SearchKind
{
    Movements,
    Company,
    Employee,
    Policy,
    Export,
    Command
}

/// <summary>Un risultato della ricerca globale: cosa è, come si chiama, un dettaglio e la chiave per ritrovarlo.</summary>
internal sealed record SearchHit(SearchKind Kind, string Title, string Detail, string Key);

/// <summary>
/// Ricerca globale della testata: oltre ai movimenti trova aziende, dipendenti, giustificativi ed esportazioni.
/// Stesse regole della ricerca nei movimenti (parole in qualsiasi ordine, senza accenti né maiuscole). Solo calcolo.
/// </summary>
internal static class GlobalSearch
{
    public const int MinimumLength = 2;
    public const int MaximumHits = 8;
    private const int PerKind = 3;

    public static IReadOnlyList<SearchHit> Find(
        string? query,
        int movementCount,
        IEnumerable<CompanyRecord> companies,
        IReadOnlyDictionary<string, string> employees,
        IReadOnlyDictionary<string, string> policies,
        IEnumerable<ExportRecord> exports)
    {
        var terms = MovementSearch.SplitTerms(query);
        if (terms.Count == 0 || (query ?? string.Empty).Trim().Length < MinimumLength)
        {
            return [];
        }

        var hits = new List<SearchHit>();
        if (movementCount > 0)
        {
            hits.Add(new SearchHit(
                SearchKind.Movements,
                $"Cerca «{query!.Trim()}» nei movimenti",
                $"{movementCount:N0} movimenti nell'anteprima",
                query.Trim()));
        }

        hits.AddRange(Rank(
            companies.Where(c => MovementSearch.Matches($"{c.Code} {c.Name} {c.Notes}", terms)),
            c => c.Label,
            c => new SearchHit(SearchKind.Company, c.Label, "Azienda", c.Code),
            terms));
        hits.AddRange(Rank(
            employees.Where(pair => MovementSearch.Matches($"{pair.Key} {pair.Value}", terms)),
            pair => pair.Key,
            pair => new SearchHit(
                SearchKind.Employee,
                pair.Key,
                pair.Value.Length == 0 ? "Dipendente · codice da inserire" : $"Dipendente · codice {pair.Value}",
                pair.Key),
            terms));
        hits.AddRange(Rank(
            policies.Where(pair => MovementSearch.Matches($"{pair.Key} {pair.Value}", terms)),
            pair => pair.Key,
            pair => new SearchHit(
                SearchKind.Policy,
                pair.Key,
                pair.Value.Length == 0 ? "Giustificativo · codice da inserire" : $"Giustificativo · codice {pair.Value}",
                pair.Key),
            terms));
        hits.AddRange(Rank(
            exports.Where(e => MovementSearch.Matches($"{e.FileName} {e.CompanyCode} {e.SourceName}", terms)),
            e => e.FileName,
            e => new SearchHit(
                SearchKind.Export,
                e.FileName,
                $"Esportazione del {e.LocalTime:dd/MM/yyyy HH:mm} · {e.Employees:N0} dipendenti",
                e.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            terms));

        return hits.Count <= MaximumHits ? hits : hits.GetRange(0, MaximumHits);
    }

    /// <summary>Prima chi inizia con il primo termine, poi l'ordine alfabetico; al più <see cref="PerKind"/> per tipo.</summary>
    private static IEnumerable<SearchHit> Rank<T>(
        IEnumerable<T> items,
        Func<T, string> title,
        Func<T, SearchHit> make,
        IReadOnlyList<string> terms) =>
        items
            .OrderBy(item => MovementSearch.Normalize(title(item)).StartsWith(terms[0], StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(title, StringComparer.CurrentCultureIgnoreCase)
            .Take(PerKind)
            .Select(make);
}
