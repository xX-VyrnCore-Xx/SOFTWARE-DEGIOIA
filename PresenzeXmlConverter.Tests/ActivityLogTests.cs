using System.Text;
using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public sealed class ActivityLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vp-act-" + Guid.NewGuid().ToString("N"));

    public ActivityLogTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
        }
    }

    private LocalDatabase NewDatabase() => new(Path.Combine(_folder, "a.db"));

    private static ActivityEntry Entry(long id, ActivityKind kind, string title, string detail = "", int minutesAgo = 0) =>
        new(id, new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc).AddMinutes(-minutesAgo), kind, title, detail);

    [Fact]
    public void ActivitiesAreStoredNewestFirstAndSurviveReopening()
    {
        var database = NewDatabase();
        var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        database.AddActivity(ActivityKind.Import, "Importato «ottobre.xlsx»", "120 richieste", now.AddMinutes(-30));
        database.AddActivity(ActivityKind.Export, "XML esportato", "AZ001 · 12 dipendenti", now.AddMinutes(-5));

        var entries = new LocalDatabase(Path.Combine(_folder, "a.db")).GetActivities();

        Assert.Equal(2, entries.Count);
        Assert.Equal(ActivityKind.Export, entries[0].Kind);
        Assert.Equal("XML esportato", entries[0].Title);
        Assert.Equal("AZ001 · 12 dipendenti", entries[0].Detail);
        Assert.Equal(ActivityKind.Import, entries[1].Kind);
    }

    [Fact]
    public void EmptyTitlesAreIgnoredAndTextsAreSingleLine()
    {
        var database = NewDatabase();
        database.AddActivity(ActivityKind.Check, "   ");
        database.AddActivity(ActivityKind.Check, "Riga uno\r\nriga due\tcoda", "dettaglio\nsu due righe");

        var only = Assert.Single(database.GetActivities());
        Assert.Equal("Riga uno riga due coda", only.Title);
        Assert.Equal("dettaglio su due righe", only.Detail);
    }

    [Fact]
    public void OnlyTheLatestEntriesAreKept()
    {
        var database = NewDatabase();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // Una sola transazione: una per voce (oltre cinquemila) su un disco lento di CI impiegava minuti.
        database.AddActivities(Enumerable.Range(0, ActivityLog.MaximumEntries + 25)
            .Select(index => (ActivityKind.Check, $"voce {index}", string.Empty, start.AddSeconds(index)))
            .ToList());

        var entries = database.GetActivities();
        Assert.Equal(ActivityLog.MaximumEntries, entries.Count);
        Assert.Equal($"voce {ActivityLog.MaximumEntries + 24}", entries[0].Title);
        Assert.Equal("voce 25", entries[^1].Title);
    }

    [Fact]
    public void ClearingEmptiesTheLog()
    {
        var database = NewDatabase();
        database.AddActivity(ActivityKind.Backup, "Backup creato");
        database.ClearActivities();
        Assert.Empty(database.GetActivities());
    }

    [Fact]
    public void LimitIsRespected()
    {
        var database = NewDatabase();
        for (var index = 0; index < 5; index++)
        {
            database.AddActivity(ActivityKind.Check, $"voce {index}");
        }
        Assert.Equal(2, database.GetActivities(2).Count);
    }

    [Fact]
    public void UnknownKindsFromNewerVersionsDoNotBreakReading()
    {
        Assert.Equal(ActivityKind.Check, ActivityLog.ParseKind("Qualcosa"));
        Assert.Equal(ActivityKind.Check, ActivityLog.ParseKind(null));
        Assert.Equal(ActivityKind.Export, ActivityLog.ParseKind("export"));
        Assert.Equal(ActivityKind.Check, ActivityLog.ParseKind("99"));
    }

    [Fact]
    public void FilterFindsWordsInAnyOrderAndByKind()
    {
        var entries = new[]
        {
            Entry(1, ActivityKind.Export, "XML esportato", "AZ001 · Rossi Srl"),
            Entry(2, ActivityKind.Codes, "Eliminato dipendente «Rossi Mario»", "", 5),
            Entry(3, ActivityKind.Backup, "Backup creato", "dati.db", 10)
        };

        Assert.Equal(2, ActivityLog.Filter(entries, "rossi").Count);
        Assert.Single(ActivityLog.Filter(entries, "mario ELIMINATO"));
        Assert.Single(ActivityLog.Filter(entries, "esportazione"));
        Assert.Single(ActivityLog.Filter(entries, "rossi", ActivityKind.Codes));
        Assert.Equal(3, ActivityLog.Filter(entries, "  ").Count);
        Assert.Empty(ActivityLog.Filter(entries, "inesistente"));
    }

    [Fact]
    public void SummaryAndWhenAreReadable()
    {
        var now = new DateTime(2026, 10, 16, 15, 0, 0);
        Assert.Equal("Nessuna attività registrata.", ActivityLog.Summarize([], now));
        var entries = new[] { Entry(1, ActivityKind.Import, "a"), Entry(2, ActivityKind.Import, "b", "", 60) };
        Assert.StartsWith("2 operazioni · l'ultima", ActivityLog.Summarize(entries, now));
        Assert.StartsWith("1 operazione ·", ActivityLog.Summarize([entries[0]], now));
        Assert.Equal("oggi 10:24", ActivityLog.DescribeWhen(new DateTime(2026, 10, 16, 10, 24, 0), now));
        Assert.Equal("ieri 18:02", ActivityLog.DescribeWhen(new DateTime(2026, 10, 15, 18, 2, 0), now));
        Assert.Equal("01/09/2026 09:15", ActivityLog.DescribeWhen(new DateTime(2026, 9, 1, 9, 15, 0), now));
    }

    [Fact]
    public void CsvHasBomHeaderAndEscapesSeparatorsQuotesAndFormulas()
    {
        var entries = new[]
        {
            Entry(1, ActivityKind.Export, "XML \"esportato\"; ok", "=SOMMA(A1:A2)", 10),
            Entry(2, ActivityKind.Codes, "Normale", "")
        };

        var csv = ActivityLog.ToCsv(entries);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal('﻿', csv[0]);
        Assert.Equal("Quando;Tipo;Azione;Dettaglio", lines[0].TrimStart('﻿'));
        Assert.Contains("\"XML \"\"esportato\"\"; ok\"", lines[1]);
        Assert.Contains(";'=SOMMA(A1:A2)", lines[1]);
        Assert.EndsWith("Normale;", lines[2]);
        // In ordine cronologico: la voce più vecchia per prima.
        Assert.Contains("Esportazione", lines[1]);
    }

    [Theory]
    [InlineData("+39 333", "'+39 333")]
    [InlineData("-5", "'-5")]
    [InlineData("@utente", "'@utente")]
    [InlineData("semplice", "semplice")]
    [InlineData(null, "")]
    public void CellsAreNeutralisedAgainstFormulas(string? input, string expected) =>
        Assert.Equal(expected, ActivityLog.Cell(input));

    [Fact]
    public void LongTextsAreClippedWithAnEllipsis()
    {
        var clipped = ActivityLog.CleanTitle(new string('a', 500));
        Assert.True(clipped.Length <= 160);
        Assert.EndsWith("…", clipped);
        Assert.Equal("breve", ActivityLog.CleanTitle(" breve "));
    }

    [Fact]
    public void EveryKindHasALabel()
    {
        foreach (var kind in Enum.GetValues<ActivityKind>())
        {
            Assert.False(string.IsNullOrWhiteSpace(ActivityLog.Label(kind)));
        }
        Assert.Equal(Enum.GetValues<ActivityKind>().Length, Enum.GetValues<ActivityKind>().Select(ActivityLog.Label).Distinct().Count());
    }
}
