namespace PresenzeXmlConverter;

/// <summary>Livello di una riga di stato, senza dipendere dai controlli (la finestra lo traduce in colore).</summary>
internal enum HealthLevel
{
    Neutral,
    Good,
    Warning,
    Bad
}

/// <summary>Testo e livello della riga «Certificato» di Stato del sistema.</summary>
internal static class CertificateStatus
{
    public const int WarnDays = 60;

    public static (string Text, HealthLevel Level) Describe(DateTime? notAfter, DateTime now)
    {
        if (notAfter is null)
        {
            return ("non firmato", HealthLevel.Neutral);
        }
        var days = (int)Math.Floor((notAfter.Value - now).TotalDays);
        if (days < 0)
        {
            return ("scaduto", HealthLevel.Bad);
        }
        return days <= WarnDays
            ? ($"scade fra {days} giorni", HealthLevel.Warning)
            : ($"valido fino a {notAfter.Value:MM/yyyy}", HealthLevel.Good);
    }
}

/// <summary>Geometria dell'anello che gira mentre il foglio viene letto: solo calcolo, il disegno è altrove.</summary>
internal static class SpinnerMath
{
    public const double RevolutionMs = 1100;
    public const float MinSweep = 70f;
    public const float MaxSweep = 250f;

    /// <summary>Angolo di partenza dell'arco (gradi, 0-360): un giro ogni <see cref="RevolutionMs"/>.</summary>
    public static float StartAngle(double elapsedMs) =>
        (float)(Mod(elapsedMs, RevolutionMs) / RevolutionMs * 360.0);

    /// <summary>Ampiezza dell'arco: si allunga e si accorcia piano, fra il minimo e il massimo.</summary>
    public static float Sweep(double elapsedMs)
    {
        var phase = Mod(elapsedMs, RevolutionMs * 2) / (RevolutionMs * 2);
        var wave = (1 - Math.Cos(phase * 2 * Math.PI)) / 2; // 0 → 1 → 0
        return (float)(MinSweep + ((MaxSweep - MinSweep) * wave));
    }

    private static double Mod(double value, double period)
    {
        var result = value % period;
        return result < 0 ? result + period : result;
    }
}
