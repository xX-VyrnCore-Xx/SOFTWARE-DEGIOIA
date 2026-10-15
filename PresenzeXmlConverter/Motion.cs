namespace PresenzeXmlConverter;

/// <summary>Curve e interpolazioni delle animazioni: calcolo puro, senza finestre né orologio, verificabile con un test.</summary>
internal static class Motion
{
    /// <summary>Durate comuni (millisecondi): brevi, perché l'interfaccia deve restare pronta a ogni clic.</summary>
    public const int Quick = 140;

    public const int Standard = 220;

    public const int Slow = 420;

    /// <summary>Decelerazione cubica: parte veloce e si ferma dolcemente, come i movimenti dei sistemi operativi.</summary>
    public static float EaseOut(float progress)
    {
        var t = Math.Clamp(progress, 0f, 1f);
        var inverse = 1f - t;
        return 1f - (inverse * inverse * inverse);
    }

    /// <summary>Avanzamento da 0 a 1 dopo <paramref name="elapsedMs"/> su <paramref name="durationMs"/> (durata nulla = subito finito).</summary>
    public static float Progress(double elapsedMs, int durationMs) =>
        durationMs <= 0 ? 1f : (float)Math.Clamp(elapsedMs / durationMs, 0d, 1d);

    public static float Lerp(float from, float to, float amount) => from + ((to - from) * amount);
}

/// <summary>
/// Un valore che scorre verso un traguardo con la curva di <see cref="Motion.EaseOut"/>. Se il traguardo cambia a metà
/// corsa riparte da dove si trova, quindi non scatta mai. Il tempo lo passa chi lo usa (in millisecondi).
/// </summary>
internal sealed class Tween
{
    private float _from;
    private float _to;
    private double _startMs;
    private int _durationMs;

    public Tween(float initial = 0f)
    {
        _from = initial;
        _to = initial;
    }

    public float Target => _to;

    /// <summary>Posizione all'istante indicato.</summary>
    public float ValueAt(double nowMs) =>
        Motion.Lerp(_from, _to, Motion.EaseOut(Motion.Progress(nowMs - _startMs, _durationMs)));

    public bool IsRunning(double nowMs) =>
        _durationMs > 0 && Motion.Progress(nowMs - _startMs, _durationMs) < 1f;

    /// <summary>Parte da dove si trova adesso verso <paramref name="target"/>; durata 0 = salto immediato.</summary>
    public void MoveTo(float target, double nowMs, int durationMs)
    {
        _from = durationMs <= 0 ? target : ValueAt(nowMs);
        _to = target;
        _startMs = nowMs;
        _durationMs = durationMs;
    }

    /// <summary>Si ferma subito sul valore indicato (nessuna animazione).</summary>
    public void Jump(float value)
    {
        _from = value;
        _to = value;
        _durationMs = 0;
    }
}
