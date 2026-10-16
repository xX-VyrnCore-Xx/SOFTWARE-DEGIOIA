namespace PresenzeXmlConverter;

/// <summary>Un'azione che la palette comandi può eseguire.</summary>
internal sealed record AppCommand(string Id, string Title, string Hint, string Shortcut, string Keywords)
{
    /// <summary>Riga di dettaglio mostrata sotto il titolo: suggerimento e scorciatoia.</summary>
    public string Detail => Shortcut.Length == 0 ? $"Comando · {Hint}" : $"Comando · {Hint} · {Shortcut}";
}

/// <summary>
/// Elenco dei comandi della palette (Ctrl+K) e ricerca fra essi. Solo calcolo: il collegamento fra identificativo e
/// azione sta nella finestra, qui si decide cosa si offre e in che ordine.
/// </summary>
internal static class CommandCatalog
{
    public const string Prefix = ">";
    public const int MaximumShown = 8;
    public const int MaximumAmongResults = 3;

    public static IReadOnlyList<AppCommand> All { get; } =
    [
        new("import", "Apri un file Excel", "importa un foglio di presenze", "Ctrl+O", "importa carica foglio xlsx"),
        new("preview", "Genera anteprima e XML", "converte le richieste in movimenti", "Ctrl+G", "converti conversione genera movimenti"),
        new("export", "Salva l'XML su file", "esporta il file per Zucchetti", "Ctrl+S", "esporta consegna paghe salva trripw"),
        new("remove-file", "Rimuovi il file caricato", "toglie il foglio Excel dall'app", "Canc", "elimina cancella foglio"),
        new("go-dashboard", "Vai alla Dashboard", "riepilogo e stato del sistema", "Ctrl+0", "sezione home riepilogo"),
        new("go-convert", "Vai a Nuova conversione", "movimenti, anteprima XML e controlli", "Ctrl+1", "sezione conversione movimenti"),
        new("go-companies", "Vai ad Aziende", "anagrafica e regole per azienda", "Ctrl+4", "sezione aziende anagrafica"),
        new("go-codes", "Vai a Gestione codici", "dipendenti e giustificativi", "Ctrl+5", "sezione codici abbinamenti dipendenti giustificativi"),
        new("go-history", "Vai allo Storico esportazioni", "i file XML già consegnati", "Ctrl+6", "sezione storico esportazioni consegne"),
        new("go-activity", "Vai al Registro attività", "cosa è stato fatto", "Ctrl+7", "sezione registro attività log cronologia"),
        new("settings", "Apri le Impostazioni", "conversione, XML, notifiche, tema", "Ctrl+,", "preferenze opzioni configurazione"),
        new("theme-dark", "Tema scuro", "si applica al prossimo avvio", "", "aspetto notte colori"),
        new("theme-light", "Tema chiaro", "si applica al prossimo avvio", "", "aspetto giorno colori"),
        new("theme-system", "Tema come Windows", "segue il sistema, al prossimo avvio", "", "aspetto automatico colori"),
        new("backup", "Crea un backup dei dati", "copia di sicurezza immediata", "", "salva copia database sicurezza"),
        new("open-data", "Apri la cartella dei dati", "dove l'app conserva i dati", "", "cartella database esplora"),
        new("open-codes-backup", "Apri la cartella delle copie dei codici", "backup automatici di codici e aziende", "", "cartella copie codici aziende"),
        new("updates", "Cerca aggiornamenti", "controlla se c'è una versione nuova", "", "versione aggiorna release scarica"),
        new("shortcuts", "Mostra le scorciatoie da tastiera", "elenco di tutte le scorciatoie", "", "tasti aiuto guida")
    ];

    /// <summary>La query è una richiesta di soli comandi (comincia con «&gt;»)?</summary>
    public static bool IsCommandQuery(string? query) =>
        query is not null && query.TrimStart().StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Testo da cercare senza il prefisso «&gt;».</summary>
    public static string StripPrefix(string? query) =>
        IsCommandQuery(query) ? query!.TrimStart()[Prefix.Length..].Trim() : (query ?? string.Empty).Trim();

    /// <summary>
    /// Comandi che contengono tutte le parole cercate (nel titolo, nelle parole chiave o nel suggerimento): prima quelli
    /// il cui titolo inizia con la prima parola, poi quelli che la contengono nel titolo, poi gli altri; a parità vale
    /// l'ordine dell'elenco. Query vuota = i comandi suggeriti, cioè i primi disponibili.
    /// </summary>
    public static IReadOnlyList<AppCommand> Find(string? query, Func<AppCommand, bool>? isAvailable = null, int limit = MaximumShown)
    {
        var terms = MovementSearch.SplitTerms(StripPrefix(query));
        var available = All.Where(command => isAvailable?.Invoke(command) ?? true);
        if (terms.Count == 0)
        {
            return available.Take(limit).ToList();
        }
        return available
            .Select((command, order) => (command, order))
            .Where(item => MovementSearch.Matches($"{item.command.Title} {item.command.Keywords} {item.command.Hint}", terms))
            .OrderBy(item => Rank(item.command, terms[0]))
            .ThenBy(item => item.order)
            .Take(limit)
            .Select(item => item.command)
            .ToList();
    }

    private static int Rank(AppCommand command, string firstTerm)
    {
        var title = MovementSearch.Normalize(command.Title);
        if (title.StartsWith(firstTerm, StringComparison.Ordinal))
        {
            return 0;
        }
        return title.Contains(firstTerm, StringComparison.Ordinal) ? 1 : 2;
    }

    public static SearchHit ToHit(AppCommand command) => new(SearchKind.Command, command.Title, command.Detail, command.Id);

    /// <summary>
    /// Mescola i risultati dei dati con i comandi in un elenco di al più <see cref="MaximumShown"/> voci: i comandi non
    /// superano <see cref="MaximumAmongResults"/> (così non coprono i risultati), ma se i dati sono pochi ne entrano di più.
    /// Con la query che comincia per «&gt;» ci sono solo comandi.
    /// </summary>
    public static IReadOnlyList<SearchHit> Compose(IReadOnlyList<SearchHit> data, IReadOnlyList<AppCommand> commands, bool commandsOnly)
    {
        if (commandsOnly)
        {
            return commands.Take(MaximumShown).Select(ToHit).ToList();
        }
        var room = MaximumShown - data.Count;
        var allowedCommands = Math.Min(commands.Count, Math.Max(MaximumAmongResults, room));
        var dataKept = data.Take(MaximumShown - Math.Min(allowedCommands, MaximumAmongResults)).ToList();
        allowedCommands = Math.Min(allowedCommands, MaximumShown - dataKept.Count);
        return dataKept.Concat(commands.Take(allowedCommands).Select(ToHit)).ToList();
    }
}
