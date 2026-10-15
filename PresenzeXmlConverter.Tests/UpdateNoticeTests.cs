using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public sealed class UpdateNoticeTests
{
    private static readonly Version V135 = new(1, 35, 0);
    private static readonly Version V136 = new(1, 36, 0);

    [Fact]
    public void ANoticeIsRepeatedOncePerDayAndImmediatelyForANewVersion()
    {
        var today = new DateTime(2026, 10, 15, 9, 0, 0);
        Assert.True(UpdateNotice.ShouldNotify(null, V136, today));
        var recorded = UpdateNotice.Record(V136, today);

        Assert.False(UpdateNotice.ShouldNotify(recorded, V136, today.AddHours(8)));
        Assert.True(UpdateNotice.ShouldNotify(recorded, V136, today.AddDays(1)));
        Assert.True(UpdateNotice.ShouldNotify(recorded, new Version(1, 37, 0), today));
        Assert.True(UpdateNotice.ShouldNotify("spazzatura", V136, today));
    }

    [Fact]
    public void TheTrayTooltipMentionsThePendingUpdateAndNeverExceedsTheLimit()
    {
        Assert.Equal("Vyrn Presence 1.35.0", UpdateNotice.TrayTooltip("Vyrn Presence 1.35.0", null));
        Assert.Equal(
            "Vyrn Presence 1.35.0 · aggiornamento 1.36.0 disponibile",
            UpdateNotice.TrayTooltip("Vyrn Presence 1.35.0", V136));

        var long_ = UpdateNotice.TrayTooltip(new string('x', 300), V136);
        Assert.Equal(UpdateNotice.TrayTextLimit, long_.Length);
        Assert.EndsWith("…", long_);
    }

    [Theory]
    [InlineData("## 1.36.0\n\n### 🧹 Tabelle e eliminazione\n- **Righe non più ridimensionabili**: dettaglio\n- altro", "Righe non più ridimensionabili: dettaglio")]
    [InlineData("Vyrn Presence 1.36.0 — PresenzeFlow 1.36.0 (v1.36.0)\n\n### Novità importanti\n* Le aziende si possono eliminare.", "Le aziende si possono eliminare")]
    [InlineData("### Novità importanti\nTesto semplice qui sotto", "Novità importanti")]
    [InlineData("", "")]
    [InlineData("   \n\n#\n- ok", "")]
    public void TheFirstRealNoteLineIsPickedWithoutMarkdownMarks(string notes, string expected) =>
        Assert.Equal(expected, UpdateNotice.FirstNoteLine(notes));

    [Fact]
    public void AVeryLongFirstLineIsShortenedWithAnEllipsis()
    {
        var line = UpdateNotice.FirstNoteLine("- " + new string('a', 200));
        Assert.True(line.Length <= 90);
        Assert.EndsWith("…", line);
    }

    [Fact]
    public void TheBalloonNamesBothVersionsAndStaysWithinTheWindowsLimit()
    {
        var text = UpdateNotice.BalloonText(V135, V136, "- Le aziende si possono eliminare ora");
        Assert.Contains("1.35.0", text);
        Assert.Contains("1.36.0", text);
        Assert.Contains("Le aziende si possono eliminare ora", text);
        Assert.EndsWith("aggiornare.", text);

        var plain = UpdateNotice.BalloonText(V135, V136, null);
        Assert.DoesNotContain("Novità:", plain);
        Assert.True(UpdateNotice.BalloonText(V135, V136, "- " + new string('b', 400)).Length <= 255);
    }
}
