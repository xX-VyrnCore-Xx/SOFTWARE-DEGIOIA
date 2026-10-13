namespace PresenzeXmlConverter.Tests;

/// <summary>Un solo Vyrn Presence per utente: il secondo avvio avvisa il primo ed esce.</summary>
public class SingleInstanceTests
{
    // Gli eventi con nome esistono solo su Windows (l'app gira solo lì): altrove il test non ha senso.
    [Fact]
    public void OnlyTheFirstInstanceIsAllowedAndTheSecondAsksTheFirstToShow()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var suffix = Guid.NewGuid().ToString("N");
        var mutexName = $"Local\\presenzeflow-test-{suffix}";
        var signalName = $"Local\\presenzeflow-signal-{suffix}";

        using var first = SingleInstance.TryAcquire(mutexName, signalName);
        Assert.NotNull(first);
        using var activated = new ManualResetEventSlim(false);
        first!.OnActivationRequested(() => activated.Set());

        var second = SingleInstance.TryAcquire(mutexName, signalName);

        Assert.Null(second);
        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)), "La prima istanza non è stata avvisata.");
    }

    [Fact]
    public void AfterTheFirstInstanceExitsANewOneCanStart()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var suffix = Guid.NewGuid().ToString("N");
        var mutexName = $"Local\\presenzeflow-test-{suffix}";
        var signalName = $"Local\\presenzeflow-signal-{suffix}";

        var first = SingleInstance.TryAcquire(mutexName, signalName);
        Assert.NotNull(first);
        first!.Dispose();

        using var next = SingleInstance.TryAcquire(mutexName, signalName);
        Assert.NotNull(next);
    }

    [Fact]
    public void SignallingWithoutAnyRunningInstanceDoesNothing()
    {
        SingleInstance.SignalExisting($"Local\\presenzeflow-nobody-{Guid.NewGuid():N}");
    }
}
