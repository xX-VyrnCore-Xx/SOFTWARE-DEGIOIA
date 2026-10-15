using System.Diagnostics;

namespace PresenzeXmlConverter;

/// <summary>
/// Orologio di un'animazione: un <see cref="Tween"/> mosso da un timer da circa 60 fotogrammi al secondo che resta
/// acceso solo finché qualcosa si muove. Il tempo è quello vero, non il numero di fotogrammi: su un PC lento la
/// durata resta la stessa. Se Windows ha gli effetti disattivati (o la finestra non è ancora visibile) il valore
/// salta subito al traguardo.
/// </summary>
internal sealed class AnimationClock : IDisposable
{
    private readonly Control _owner;
    private readonly Tween _tween;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private readonly long _origin = Stopwatch.GetTimestamp();

    public AnimationClock(Control owner, float initial = 0f)
    {
        _owner = owner;
        _tween = new Tween(initial);
        _timer.Tick += (_, _) =>
        {
            var now = NowMs;
            Frame?.Invoke(this, EventArgs.Empty);
            if (!_tween.IsRunning(now))
            {
                _timer.Stop();
                Frame?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    /// <summary>Gli effetti di animazione di Windows sono attivi? (Accessibilità: chi li spegne non vede movimenti.)</summary>
    public static bool EffectsEnabled
    {
        get
        {
            try
            {
                return SystemInformation.UIEffectsEnabled;
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return true;
            }
        }
    }

    /// <summary>Si ridisegna a ogni fotogramma e a fine corsa.</summary>
    public event EventHandler? Frame;

    public double NowMs => (Stopwatch.GetTimestamp() - _origin) * 1000.0 / Stopwatch.Frequency;

    public float Value => _tween.ValueAt(NowMs);

    public float Target => _tween.Target;

    public bool IsRunning => _tween.IsRunning(NowMs);

    /// <summary>Scorre verso il traguardo; senza effetti o senza finestra visibile salta direttamente.</summary>
    public void AnimateTo(float target, int durationMs)
    {
        if (!EffectsEnabled || !_owner.IsHandleCreated || !_owner.Visible)
        {
            _timer.Stop();
            _tween.Jump(target);
            Frame?.Invoke(this, EventArgs.Empty);
            return;
        }
        _tween.MoveTo(target, NowMs, durationMs);
        _timer.Start();
    }

    /// <summary>Si ferma sul valore indicato senza animare.</summary>
    public void Jump(float value)
    {
        _timer.Stop();
        _tween.Jump(value);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
