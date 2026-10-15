using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>I passi di un aggiornamento, nell'ordine in cui avvengono.</summary>
internal enum UpdateStep
{
    Download,
    Checksum,
    Signature,
    Install
}

internal enum StepState
{
    Pending,
    Active,
    Done,
    Failed
}

/// <summary>Testi e stato dei passi mostrati nella finestra di aggiornamento (nessuna dipendenza dai controlli).</summary>
internal static class UpdateSteps
{
    public static readonly IReadOnlyList<UpdateStep> All =
        [UpdateStep.Download, UpdateStep.Checksum, UpdateStep.Signature, UpdateStep.Install];

    public static string Label(UpdateStep step) => step switch
    {
        UpdateStep.Download => "Scaricamento",
        UpdateStep.Checksum => "Controllo del file",
        UpdateStep.Signature => "Verifica della firma",
        _ => "Installazione"
    };

    /// <summary>Stato di un passo quando quello corrente è <paramref name="current"/> (nessuno se l'operazione è chiusa).</summary>
    public static StepState StateOf(UpdateStep step, UpdateStep current, bool failed = false) =>
        step < current ? StepState.Done
        : step == current ? (failed ? StepState.Failed : StepState.Active)
        : StepState.Pending;
}

/// <summary>
/// Velocità e tempo residuo di un download: la velocità è una media mobile esponenziale, così il tempo
/// stimato non salta a ogni pacchetto.
/// </summary>
internal sealed class TransferMeter
{
    private const double Smoothing = 0.3;
    private const double MinimumInterval = 0.25;

    private double _lastSeconds;
    private long _lastBytes;
    private double _speed;

    public long Received { get; private set; }

    public long Total { get; private set; }

    /// <summary>Byte al secondo (0 finché non c'è una misura).</summary>
    public double BytesPerSecond => _speed;

    public int Percent => Total <= 0 ? 0 : (int)Math.Min(100, Received * 100 / Total);

    public TimeSpan? Remaining =>
        _speed > 0 && Total > Received
            ? TimeSpan.FromSeconds((Total - Received) / _speed)
            : null;

    public void Add(long received, long total, TimeSpan elapsed)
    {
        Received = Math.Max(0, received);
        Total = Math.Max(0, total);
        var seconds = elapsed.TotalSeconds;
        var interval = seconds - _lastSeconds;
        if (_lastSeconds == 0 && seconds > 0)
        {
            _speed = Received / seconds;
            _lastSeconds = seconds;
            _lastBytes = Received;
        }
        else if (interval >= MinimumInterval)
        {
            var instant = Math.Max(0, Received - _lastBytes) / interval;
            _speed = _speed <= 0 ? instant : _speed + (instant - _speed) * Smoothing;
            _lastSeconds = seconds;
            _lastBytes = Received;
        }
    }

    public static string FormatSize(long bytes)
    {
        var megabytes = bytes / 1024d / 1024d;
        return megabytes >= 0.1 || bytes == 0
            ? Number(megabytes) + " MB"
            : Number(bytes / 1024d) + " KB";
    }

    public static string FormatSpeed(double bytesPerSecond) => FormatSize((long)bytesPerSecond) + "/s";

    /// <summary>«circa 8 s», «circa 1 min 05 s»; vuoto se non stimabile.</summary>
    public static string FormatRemaining(TimeSpan? remaining)
    {
        if (remaining is not { } value)
        {
            return string.Empty;
        }
        var seconds = (int)Math.Ceiling(value.TotalSeconds);
        return seconds < 60
            ? $"circa {Math.Max(1, seconds)} s"
            : $"circa {seconds / 60} min {seconds % 60:00} s";
    }

    /// <summary>«12,3 di 54,3 MB · 2,1 MB/s · circa 20 s» (le parti non note si omettono).</summary>
    public string Describe()
    {
        var parts = new List<string> { $"{FormatSize(Received)} di {FormatSize(Total)}" };
        if (_speed > 0)
        {
            parts.Add(FormatSpeed(_speed));
        }
        var left = FormatRemaining(Remaining);
        if (left.Length > 0)
        {
            parts.Add(left);
        }
        return string.Join(" · ", parts);
    }

    private static string Number(double value) =>
        value.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
}
