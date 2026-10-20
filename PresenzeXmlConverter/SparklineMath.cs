namespace PresenzeXmlConverter;

/// <summary>Il disegno di una minigrafico (sparkline) come calcolo: da una serie di valori a posizioni 0–1, verificabile con un test.</summary>
internal static class SparklineMath
{
    /// <summary>
    /// Porta la serie fra 0 (valore più basso) e 1 (più alto). Una serie piatta sta a metà altezza: una linea dritta,
    /// non una riga appoggiata sul fondo. Meno di due valori non fanno una linea (vuoto).
    /// </summary>
    public static float[] Normalize(IReadOnlyList<float> values)
    {
        if (values.Count < 2)
        {
            return [];
        }
        var min = values.Min();
        var max = values.Max();
        var range = max - min;
        return values.Select(value => range <= 0f ? 0.5f : (value - min) / range).ToArray();
    }
}
