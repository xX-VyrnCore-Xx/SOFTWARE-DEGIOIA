using PresenzeXmlConverter;

namespace PresenzeXmlConverter.Tests;

public class StatusBarModelTests
{
    [Theory]
    [InlineData("●   Backup creato: dati.db", "Backup creato: dati.db")]
    [InlineData("●  Database collegato", "Database collegato")]
    [InlineData("Impossibile aprire la cartella", "Impossibile aprire la cartella")]
    [InlineData("  ●    Più    spazi   qui ", "Più spazi qui")]
    [InlineData("", "Pronto")]
    [InlineData(null, "Pronto")]
    [InlineData("●   ", "Pronto")]
    public void MessagesLoseTheBulletAndExtraSpaces(string? input, string expected) =>
        Assert.Equal(expected, StatusBarModel.CleanMessage(input));

    [Theory]
    [InlineData("Aggiornamenti · aggiornato alla 1.42.0 · 12:13", "aggiornato alla 1.42.0 · 12:13")]
    [InlineData("Aggiornamenti · nuova versione 1.43.0 disponibile", "nuova versione 1.43.0 disponibile")]
    [InlineData("aggiornato alla 1.42.0", "aggiornato alla 1.42.0")]
    [InlineData("Aggiornamenti", "verifica in corso")]
    [InlineData("Aggiornamenti · verifica in corso", "verifica in corso")]
    [InlineData("", "verifica in corso")]
    public void UpdatesTextDropsTheLabel(string input, string expected) =>
        Assert.Equal(expected, StatusBarModel.CleanUpdates(input));

    [Fact]
    public void AllSegmentsFitWhenThereIsRoom()
    {
        Assert.Equal(3, StatusBarModel.SegmentsThatFit(1000, 200, [200, 60, 50], 12));
    }

    [Fact]
    public void LeastImportantSegmentsAreDroppedFirst()
    {
        // Tutti e tre chiedono 200 + 12 + 60 + 12 + 50 = 334; i primi due 272; il primo solo 200.
        Assert.Equal(2, StatusBarModel.SegmentsThatFit(500, 200, [200, 60, 50], 12)); // restano 300: l'ora salta
        Assert.Equal(1, StatusBarModel.SegmentsThatFit(450, 200, [200, 60, 50], 12)); // restano 250: salta anche la versione
        Assert.Equal(1, StatusBarModel.SegmentsThatFit(300, 200, [200, 60, 50], 12));
    }

    [Fact]
    public void TheFirstSegmentIsAlwaysKept()
    {
        Assert.Equal(1, StatusBarModel.SegmentsThatFit(50, 200, [200, 60, 50], 12));
        Assert.Equal(0, StatusBarModel.SegmentsThatFit(500, 200, [], 12));
    }

    [Fact]
    public void TotalCountsSeparatorsOnlyBetweenSegments()
    {
        Assert.Equal(0, StatusBarModel.Total([10, 20], 0, 5));
        Assert.Equal(10, StatusBarModel.Total([10, 20], 1, 5));
        Assert.Equal(35, StatusBarModel.Total([10, 20], 2, 5));
    }

    [Fact]
    public void MessageLevelFollowsTheWarningFlag()
    {
        Assert.Equal(HealthLevel.Warning, StatusBarModel.MessageLevel(true));
        Assert.Equal(HealthLevel.Good, StatusBarModel.MessageLevel(false));
    }

    [Fact]
    public void EmptyRailLineStaysEmpty()
    {
        Assert.Equal(string.Empty, StatusBarModel.CleanRailLine(""));
        Assert.Equal("nessuna azienda", StatusBarModel.CleanRailLine("nessuna azienda"));
    }
}
