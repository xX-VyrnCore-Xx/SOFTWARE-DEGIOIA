namespace PresenzeXmlConverter.Tests;

using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// Controlli nuovi dell'interfaccia (interruttore, scelta segmentata, testo che si misura da solo): la logica
/// di selezione e di misura si verifica senza aprire finestre.
/// </summary>
public class SwitchControlsTests
{
    private static T? OnSta<T>(Func<T> probe)
        where T : struct
    {
        T result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = probe();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return failure is null ? result : null;
    }

    [Fact]
    public void ASegmentedControlReturnsTheValueOfTheChosenOption()
    {
        var result = OnSta(() =>
        {
            using var segmented = new SegmentedControl();
            segmented.AddItem("Chiedi", string.Empty);
            segmented.AddItem("Tray", "tray");
            segmented.AddItem("Esci", "exit");
            var raised = 0;
            segmented.SelectionChanged += (_, _) => raised++;
            segmented.SelectTag("tray");
            var afterTag = segmented.SelectedTag as string;
            segmented.Select(2);
            var afterClick = segmented.SelectedTag as string;
            return (afterTag, afterClick, raised, segmented.ItemCount);
        });
        if (result is null)
        {
            return;
        }
        Assert.Equal("tray", result.Value.afterTag);
        Assert.Equal("exit", result.Value.afterClick);
        // SelectTag non avvisa (è un'impostazione iniziale); la scelta dell'utente sì.
        Assert.Equal(1, result.Value.raised);
        Assert.Equal(3, result.Value.ItemCount);
    }

    [Fact]
    public void ASegmentedControlIgnoresOutOfRangeChoices()
    {
        var result = OnSta(() =>
        {
            using var segmented = new SegmentedControl();
            segmented.AddItem("A", 1);
            segmented.AddItem("B", 2);
            segmented.Select(99);
            var high = (int)segmented.SelectedTag!;
            segmented.Select(-5);
            var low = (int)segmented.SelectedTag!;
            return (high, low);
        });
        if (result is null)
        {
            return;
        }
        Assert.Equal(2, result.Value.high);
        Assert.Equal(1, result.Value.low);
    }

    [Fact]
    public void ATextBlockIsAsTallAsItsTextAndGrowsWhenItWraps()
    {
        var result = OnSta(() =>
        {
            using var block = new TextBlock
            {
                Text = "Una frase abbastanza lunga da andare a capo quando la colonna è stretta, e da stare su una riga quando è larga.",
                Font = Theme.Fonts.Small,
                Wrap = true
            };
            var wide = block.GetPreferredSize(new Size(900, 0));
            var narrow = block.GetPreferredSize(new Size(160, 0));
            return (wideHeight: wide.Height, narrowHeight: narrow.Height, line: Theme.Fonts.Small.Height);
        });
        if (result is null)
        {
            return;
        }
        Assert.True(result.Value.wideHeight >= result.Value.line, "il testo deve avere almeno l'altezza di una riga");
        Assert.True(
            result.Value.narrowHeight > result.Value.wideHeight,
            $"in una colonna stretta il testo va a capo e la riga cresce ({result.Value.narrowHeight} contro {result.Value.wideHeight})");
    }

    [Fact]
    public void AToggleSwitchIsAtLeastAsTallAsItsTrack()
    {
        var result = OnSta(() =>
        {
            using var toggle = new ToggleSwitch { Text = "Opzione", Font = Theme.Fonts.Small };
            var size = toggle.GetPreferredSize(new Size(500, 0));
            return (size.Height, size.Width);
        });
        if (result is null)
        {
            return;
        }
        Assert.True(result.Value.Height >= 24, "l'interruttore deve contenere la sua pista");
        Assert.Equal(500, result.Value.Width);
    }

    [Fact]
    public void ScaledMeasuresAreUnchangedBeforeTheWindowExists()
    {
        var result = OnSta(() =>
        {
            using var control = new Panel();
            return (control.Px(46), control.Px(1.5f) == 1.5f);
        });
        if (result is null)
        {
            return;
        }
        Assert.Equal(46, result.Value.Item1);
        Assert.True(result.Value.Item2);
    }
}
