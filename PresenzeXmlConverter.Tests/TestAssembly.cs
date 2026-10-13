using Xunit;

// Più classi di test modificano la stessa variabile d'ambiente del token e lo stesso
// stato statico di UpdateService: se girassero in parallelo si leggerebbero i valori a
// vicenda (errore reale visto in CI: "token-ambiente" al posto di "token-comune").
// I test sono veloci, quindi si eseguono in sequenza.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
