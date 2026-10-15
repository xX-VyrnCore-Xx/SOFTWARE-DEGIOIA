using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

/// <summary>Il lavoro in corso si salva e si ripristina: niente da rifare se si chiude per sbaglio.</summary>
public class SessionStateTests
{
    private static List<DailyMovement> Sample() =>
    [
        new("Rossi Mario", "0000001", "Ferie", "FE", new DateTime(2026, 9, 15), 8, 0, 12, null),
        new("Rossi Mario", "0000001", AttendanceMovementGenerator.PresencePolicy, "01", new DateTime(2026, 9, 16), 6, 30, 0, "01")
    ];

    [Fact]
    public void ASnapshotSurvivesSerializationWithEveryMovementFieldIncludingManualEdits()
    {
        var snapshot = SessionState.Create(null, "Foglio1", "AZ001", exported: true, Sample(), ["Riga 4: avviso"]);

        Assert.True(SessionState.TryParse(SessionState.Serialize(snapshot), out var restored));

        Assert.Equal("AZ001", restored.CompanyCode);
        Assert.True(restored.Exported);
        Assert.Equal("Foglio1", restored.SheetName);
        Assert.Equal(["Riga 4: avviso"], restored.Warnings);
        Assert.Equal(Sample(), SessionState.ToMovements(restored));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ non è json")]
    [InlineData("{\"Version\":99,\"Movements\":[],\"Warnings\":[]}")]
    [InlineData("null")]
    public void AnUnreadableOrForeignSnapshotIsIgnoredWithoutThrowing(string? text)
    {
        Assert.False(SessionState.TryParse(text, out _));
    }

    [Fact]
    public void TheSourceFileIsRecognizedAsSameChangedOrMissing()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vyrn-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var file = Path.Combine(folder, "presenze.xlsx");
            File.WriteAllText(file, "uno");

            var snapshot = SessionState.Create(file, null, "AZ001", false, Sample(), []);
            Assert.Equal(SessionState.SourceStatus.Same, SessionState.CheckSource(snapshot));

            File.WriteAllText(file, "uno e altro");
            Assert.Equal(SessionState.SourceStatus.Changed, SessionState.CheckSource(snapshot));

            File.Delete(file);
            Assert.Equal(SessionState.SourceStatus.Missing, SessionState.CheckSource(snapshot));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void WithoutASourceFileThereIsNothingToCheck()
    {
        var snapshot = SessionState.Create(null, null, "AZ001", false, Sample(), []);

        Assert.Equal(SessionState.SourceStatus.None, SessionState.CheckSource(snapshot));
    }
}
