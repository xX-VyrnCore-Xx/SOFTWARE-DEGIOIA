namespace PresenzeXmlConverter;

/// <summary>Una barra del grafico «Ore al giorno», già in pixel: dove sta e quanto sono alte la parte di presenza e quella di assenza.</summary>
internal readonly record struct DayBar(DateTime Date, int X, int Width, int PresenceHeight, int AbsenceHeight)
{
    public int Height => PresenceHeight + AbsenceHeight;
}

/// <summary>
/// Disposizione del grafico «Ore al giorno»: una colonna per ogni giorno di calendario fra il primo e l'ultimo
/// con movimenti (i giorni senza ore restano vuoti, così si vedono i fine settimana). Solo calcolo.
/// </summary>
internal static class DayBars
{
    public const int MaximumSlots = 62;

    /// <summary>Colonne e altezze per un'area di <paramref name="plotWidth"/> × <paramref name="plotHeight"/> pixel; <paramref name="grow"/> va da 0 a 1.</summary>
    public static IReadOnlyList<DayBar> Layout(IReadOnlyList<DayTotal> days, int plotWidth, int plotHeight, float grow)
    {
        if (days.Count == 0 || plotWidth <= 0 || plotHeight <= 0)
        {
            return [];
        }

        var first = days[0].Date.Date;
        var slots = Math.Min(MaximumSlots, ((days[^1].Date.Date - first).Days) + 1);
        var slotWidth = plotWidth / (double)slots;
        var gap = slotWidth >= 6 ? 2 : 1;
        var peak = days.Max(day => day.TotalMinutes);
        if (peak <= 0)
        {
            return [];
        }

        var bars = new List<DayBar>(days.Count);
        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            var slot = (day.Date.Date - first).Days;
            if (slot < 0 || slot >= slots || day.TotalMinutes <= 0)
            {
                continue;
            }
            var progress = Progress(grow, slot, slots);
            var total = Math.Max(1, (int)Math.Round(plotHeight * (day.TotalMinutes / (double)peak) * progress));
            var absence = day.AbsenceMinutes <= 0
                ? 0
                : Math.Clamp((int)Math.Round(total * (day.AbsenceMinutes / (double)day.TotalMinutes)), 1, total);
            var x = (int)Math.Round(slot * slotWidth);
            var width = Math.Max(2, (int)Math.Round(slotWidth) - gap);
            bars.Add(new DayBar(day.Date.Date, x, width, total - absence, absence));
        }
        return bars;
    }

    /// <summary>Avanzamento di una colonna (0-1): le prime partono prima, a cascata, e arrivano tutte insieme alla fine.</summary>
    public static float Progress(float grow, int slot, int slots)
    {
        var delay = slots <= 1 ? 0f : 0.4f * slot / (slots - 1);
        var local = (Math.Clamp(grow, 0f, 1f) - delay) / 0.6f;
        local = Math.Clamp(local, 0f, 1f);
        // Rallenta verso la fine (ease-out cubico).
        return 1f - ((1f - local) * (1f - local) * (1f - local));
    }
}
