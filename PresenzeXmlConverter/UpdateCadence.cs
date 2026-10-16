namespace PresenzeXmlConverter;

/// <summary>
/// Quando cercare un aggiornamento, solo calcolo. Il controllo gira spesso (ogni dieci minuti) e anche negli
/// istanti in cui la rete torna disponibile o il PC esce dalla sospensione; un intervallo minimo evita richieste
/// ripetute quando più di un evento arriva insieme.
/// </summary>
internal static class UpdateCadence
{
    /// <summary>Intervallo del controllo periodico: GitHub concede 60 richieste all'ora senza token, qui se ne usano 6.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(10);

    /// <summary>Dopo un controllo fallito per la rete si riprova presto.</summary>
    public static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(2);

    /// <summary>Tra un controllo automatico e il successivo non passa mai meno di così.</summary>
    public static readonly TimeSpan MinimumGap = TimeSpan.FromMinutes(2);

    /// <summary>Riaprendo la finestra dopo più di così si ricontrolla subito.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    /// <summary>Attesa dopo il ritorno della rete o la fine della sospensione: la connessione ha bisogno di un istante.</summary>
    public static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(12);

    /// <summary>Si può cercare adesso? Mai un controllo ravvicinato al precedente, a meno che <paramref name="staleOnly"/> chieda più margine.</summary>
    public static bool ShouldCheck(DateTime? lastCheckUtc, DateTime nowUtc, bool staleOnly)
    {
        if (lastCheckUtc is null)
        {
            return true;
        }
        var elapsed = nowUtc - lastCheckUtc.Value;
        if (elapsed < TimeSpan.Zero)
        {
            return true; // L'orologio è tornato indietro: meglio controllare.
        }
        return elapsed >= (staleOnly ? StaleAfter : MinimumGap);
    }
}
