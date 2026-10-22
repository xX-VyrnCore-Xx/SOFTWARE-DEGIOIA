namespace PresenzeXmlConverter;

/// <summary>
/// I passaggi di schema del database, in ordine e con un numero di versione. Lo schema di base (versione 5) è
/// scritto in modo che si possa rieseguire senza danni; dalla 6 ogni modifica è un passaggio a sé, applicato in una
/// transazione insieme al nuovo numero di versione: o riesce tutto o resta com'era. Solo dati, così un test controlla
/// che i numeri siano consecutivi e che nessun passaggio sia stato dimenticato.
/// </summary>
internal static class DatabaseMigrations
{
    /// <summary>Ultima versione costruita dal blocco iniziale di <c>LocalDatabase</c>.</summary>
    public const int BaselineVersion = 5;

    public sealed record Step(int Version, string Description, string Sql);

    public static IReadOnlyList<Step> Steps { get; } =
    [
        new Step(
            6,
            "Indici per le liste in ordine di data e per le ricerche per azienda",
            """
            CREATE INDEX IF NOT EXISTS Exports_Recent ON Exports (ExportedAtUtc DESC, Id DESC);
            CREATE INDEX IF NOT EXISTS Exports_Company ON Exports (CompanyCode, ExportedAtUtc DESC);
            CREATE INDEX IF NOT EXISTS Activity_Recent ON Activity (AtUtc DESC, Id DESC);
            CREATE INDEX IF NOT EXISTS Activity_Kind ON Activity (Kind, AtUtc DESC);
            """),
        new Step(
            7,
            "Ore di ogni dipendente mese per mese, conservate a ogni esportazione (andamento e confronti)",
            """
            CREATE TABLE IF NOT EXISTS MonthlyTotals (
                CompanyCode TEXT NOT NULL,
                Month TEXT NOT NULL,
                EmployeeCode TEXT NOT NULL,
                EmployeeName TEXT NOT NULL,
                PresenceMinutes INTEGER NOT NULL,
                AbsenceMinutes INTEGER NOT NULL,
                SavedUtc TEXT NOT NULL,
                PRIMARY KEY (CompanyCode, Month, EmployeeCode)
            );
            CREATE INDEX IF NOT EXISTS MonthlyTotals_Company ON MonthlyTotals (CompanyCode, Month);
            """),
        new Step(
            8,
            "Ore di ogni giustificativo usate da ogni dipendente mese per mese (saldo di ferie e permessi)",
            """
            CREATE TABLE IF NOT EXISTS AbsenceTotals (
                CompanyCode TEXT NOT NULL,
                Month TEXT NOT NULL,
                EmployeeCode TEXT NOT NULL,
                EmployeeName TEXT NOT NULL,
                Policy TEXT NOT NULL,
                ReasonCode TEXT NOT NULL,
                Minutes INTEGER NOT NULL,
                SavedUtc TEXT NOT NULL,
                PRIMARY KEY (CompanyCode, Month, EmployeeCode, ReasonCode)
            );
            CREATE INDEX IF NOT EXISTS AbsenceTotals_Company ON AbsenceTotals (CompanyCode, Month);
            """)
    ];

    /// <summary>La versione che questa build scrive nel database (costante: serve a <c>LocalDatabase.CurrentSchemaVersion</c>).</summary>
    public const int Latest = 8;

    /// <summary>I passaggi ancora da fare partendo da <paramref name="currentVersion"/>, in ordine.</summary>
    public static IEnumerable<Step> Pending(int currentVersion) =>
        Steps.Where(step => step.Version > currentVersion).OrderBy(step => step.Version);

    /// <summary>Null se l'elenco è coerente; altrimenti che cosa non va (versioni non consecutive, passaggio vuoto).</summary>
    public static string? Validate()
    {
        var expected = BaselineVersion + 1;
        foreach (var step in Steps)
        {
            if (step.Version != expected)
            {
                return $"Passaggio {step.Version}: ci si aspettava la versione {expected}.";
            }
            if (string.IsNullOrWhiteSpace(step.Sql) || string.IsNullOrWhiteSpace(step.Description))
            {
                return $"Passaggio {step.Version}: testo o descrizione mancanti.";
            }
            expected++;
        }
        return expected - 1 == Latest ? null : $"L'ultimo passaggio è {expected - 1} ma Latest dice {Latest}.";
    }
}
