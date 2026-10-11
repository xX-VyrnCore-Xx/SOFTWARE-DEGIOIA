using System.Text.Json;

namespace PresenzeXmlConverter.Tests;

/// <summary>
/// L'assistente AI propone, il programma dispone: queste prove verificano che
/// le modifiche strutturate prodotte dall'AI siano applicate in modo
/// deterministico e che i codici non configurati vengano sempre scartati.
/// </summary>
public class DocumentEditorTests
{
    private static DailyMovement Movement(
        string employee = "Mario Rossi",
        string code = "1234567",
        string policy = "Ferie",
        string reason = "01",
        int day = 17,
        int hours = 8,
        int minutes = 0) =>
        new(employee, code, policy, reason, new DateTime(2026, 8, day), hours, minutes);

    [Fact]
    public void SetTimeUpdatesOnlyTheMovementsMatchingTheFilter()
    {
        var movements = new[]
        {
            Movement(day: 17, hours: 8),
            Movement(day: 18, hours: 8),
            Movement(employee: "Luigi Bianchi", code: "7654321", day: 17, hours: 8)
        };
        var edit = new DocumentEdit(
            DocumentEditKind.SetTime,
            new MovementFilter(Employee: "Rossi", Date: new DateTime(2026, 8, 18)),
            Hours: 4,
            Minutes: 30);

        var result = DocumentEditor.Apply(movements, [edit]);

        Assert.Single(result.Applied);
        Assert.Empty(result.Skipped);
        Assert.Equal(3, result.Movements.Count);
        var changed = result.Movements.Single(movement => movement.Date.Day == 18);
        Assert.Equal(4, changed.Hours);
        Assert.Equal(30, changed.Minutes);
        Assert.Equal(8, result.Movements.Single(movement => movement.Date.Day == 17 && movement.EmployeeName == "Mario Rossi").Hours);
    }

    [Fact]
    public void ScaleHoursHalvesSelectedMovementsWithoutTouchingOthers()
    {
        var movements = new[] { Movement(day: 17), Movement(employee: "Luigi Bianchi", code: "7654321", day: 17) };
        var edit = new DocumentEdit(DocumentEditKind.ScaleHours, new MovementFilter(Employee: "Rossi"), Factor: 0.5m);

        var result = DocumentEditor.Apply(movements, [edit]);

        Assert.Equal(4, result.Movements.Single(movement => movement.EmployeeName == "Mario Rossi").Hours);
        Assert.Equal(8, result.Movements.Single(movement => movement.EmployeeName == "Luigi Bianchi").Hours);
    }

    [Fact]
    public void ShiftDaysMovesDatesAndRemoveDeletesSelectedRows()
    {
        var movements = new[] { Movement(day: 17), Movement(day: 18) };

        var shifted = DocumentEditor.Apply(
            movements,
            [new DocumentEdit(DocumentEditKind.ShiftDays, new MovementFilter(Date: new DateTime(2026, 8, 17)), DayDelta: 2)]);
        Assert.Equal(19, shifted.Movements.Single(movement => movement.SourceRow == 0 && movement.Date.Day == 19).Date.Day);

        var removed = DocumentEditor.Apply(
            movements,
            [new DocumentEdit(DocumentEditKind.Remove, new MovementFilter(Date: new DateTime(2026, 8, 17)))]);
        Assert.Single(removed.Movements);
        Assert.Equal(18, removed.Movements[0].Date.Day);
    }

    [Fact]
    public void SetTimeOutsideTheTrackLimitsIsSkipped()
    {
        var result = DocumentEditor.Apply(
            [Movement()],
            [new DocumentEdit(DocumentEditKind.SetTime, new MovementFilter(), Hours: 100)]);

        Assert.Empty(result.Applied);
        Assert.Single(result.Skipped);
        Assert.Equal(8, result.Movements[0].Hours);
    }

    [Fact]
    public void FilterWithoutMatchesReportsTheEditAsSkipped()
    {
        var result = DocumentEditor.Apply(
            [Movement()],
            [new DocumentEdit(DocumentEditKind.Remove, new MovementFilter(Employee: "Nessuno"))]);

        Assert.Empty(result.Applied);
        Assert.Single(result.Skipped);
        Assert.Single(result.Movements);
    }

    [Fact]
    public void ParserReadsReplyAndTypedEdits()
    {
        const string json = """
            {
              "reply": "Fatto.",
              "edits": [
                { "kind": "setHours", "employee": "Rossi", "date": "2026-08-18", "hours": 4, "minutes": 0,
                  "summary": "Ferie Rossi a 4 ore" },
                { "kind": "shiftDays", "policy": "Ferie", "days": -1 }
              ]
            }
            """;

        var edits = DocumentEditParser.Parse(json);

        Assert.Equal("Fatto.", DocumentEditParser.ParseReply(json));
        Assert.Equal(2, edits.Count);
        Assert.Equal(DocumentEditKind.SetTime, edits[0].Kind);
        Assert.Equal(4, edits[0].Hours);
        Assert.Equal("Rossi", edits[0].Filter.Employee);
        Assert.Equal(new DateTime(2026, 8, 18), edits[0].Filter.Date);
        Assert.Equal(DocumentEditKind.ShiftDays, edits[1].Kind);
        Assert.Equal(-1, edits[1].DayDelta);
    }

    [Fact]
    public void ParserDropsEditsThatUseUnconfiguredReasonCodes()
    {
        const string json = """
            {
              "reply": "ok",
              "edits": [
                { "kind": "setReasonCode", "policy": "Ferie", "reasonCode": "09" },
                { "kind": "setReasonCode", "policy": "Malattia", "reasonCode": "01" }
              ]
            }
            """;

        var edits = DocumentEditParser.Parse(json, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "01" });

        var edit = Assert.Single(edits);
        Assert.Equal(DocumentEditKind.SetReasonCode, edit.Kind);
        Assert.Equal("01", edit.ReasonCode);
    }

    [Fact]
    public void ParserIgnoresUnknownKindsAndMalformedJson()
    {
        Assert.Empty(DocumentEditParser.Parse("""{ "edits": [ { "kind": "esplodi" } ] }"""));
        Assert.Empty(DocumentEditParser.Parse("non è json"));
        Assert.Empty(DocumentEditParser.Parse(""));
    }

    [Fact]
    public void SummarizeMentionsTotalsAndPolicies()
    {
        var summary = DocumentEditor.Summarize([Movement(day: 17), Movement(day: 18, hours: 4)]);

        Assert.Contains("2", summary, StringComparison.Ordinal);
        Assert.Contains("Ferie", summary, StringComparison.Ordinal);
        Assert.Contains("2026-08-17", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ParserHandlesNumericStrings()
    {
        var edit = Assert.Single(DocumentEditParser.Parse(
            """{ "edits": [ { "kind": "scaleHours", "factor": "0,5" } ] }"""));

        Assert.Equal(0.5m, edit.Factor);
    }
}
