namespace PresenzeXmlConverter.Tests;

/// <summary>
/// La card di sinistra non deve mai chiedere più spazio di quello che ha:
/// quando succede compare la barra di scorrimento di sistema, che Windows disegna
/// in grigio chiaro e che il test grafico segnala come colore fuori tema (con la
/// finestra compressa dal runner CI, 1024x768, è già successo).
/// </summary>
public class SetupCardLayoutTests
{
    /// <summary>Altezza della finestra usata dal runner CI: schermo 1024x768 con barra delle applicazioni.</summary>
    private const int CiWindowClientHeight = 681;

    [Fact]
    public void TheCardFitsWithoutScrollingAtEveryAllowedWindowSize()
    {
        var smallestClient = SetupCardLayout.MinimumWindowHeight - SetupCardLayout.WindowFrameHeight;
        for (var clientHeight = smallestClient; clientHeight <= 1200; clientHeight++)
        {
            var available = SetupCardLayout.ContentHeight(clientHeight);
            Assert.True(
                SetupCardLayout.StackHeight(available) <= available,
                $"Finestra alta {clientHeight} px: la card chiede {SetupCardLayout.StackHeight(available)} px " +
                $"ma ne ha {available} px (comparirebbe la barra di scorrimento di sistema).");
        }
    }

    [Fact]
    public void TheStepsGuideStaysVisibleOnTheCiWindow()
    {
        var available = SetupCardLayout.ContentHeight(CiWindowClientHeight);

        var steps = SetupCardLayout.StepsHeight(available);
        Assert.Equal(SetupCardLayout.StepsMaximumHeight, steps);
        Assert.True(
            SetupCardLayout.StackHeight(available) <= available,
            "Contenuto più alto della card: barra di scorrimento di sistema.");
    }

    [Fact]
    public void TheStepsGuideShrinksAndThenDisappearsInsteadOfScrolling()
    {
        // Con meno spazio la guida si accorcia restando sopra l'altezza del testo...
        var narrowed = SetupCardLayout.FixedRowsHeight + SetupCardLayout.StepsMinimumHeight + 4;
        Assert.Equal(
            SetupCardLayout.StepsMinimumHeight + 4,
            SetupCardLayout.StepsHeight(narrowed));
        // ...e sotto quella soglia sparisce del tutto, invece di far scorrere la card.
        Assert.Equal(0, SetupCardLayout.StepsHeight(SetupCardLayout.FixedRowsHeight + SetupCardLayout.StepsMinimumHeight - 1));
        Assert.Equal(SetupCardLayout.FixedRowsHeight, SetupCardLayout.StackHeight(SetupCardLayout.FixedRowsHeight));
    }

    [Fact]
    public void TheFixedRowsLeaveRoomForTheStepsGuideOnTheCiWindow()
    {
        var available = SetupCardLayout.ContentHeight(CiWindowClientHeight);
        Assert.True(
            SetupCardLayout.FixedRowsHeight + SetupCardLayout.StepsMinimumHeight <= available,
            "La guida ai passi non entrerebbe nella finestra del runner CI.");
    }
}
