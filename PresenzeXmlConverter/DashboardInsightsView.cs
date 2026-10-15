using System.Drawing.Drawing2D;

namespace PresenzeXmlConverter;

/// <summary>
/// Card «Analisi del mese» della dashboard: una barra presenza/assenza, i giustificativi che pesano di più
/// con gli stessi colori del calendario e i giorni da controllare. Disegna solo ciò che entra nell'altezza
/// disponibile, così sul portatile non taglia mai una riga a metà.
/// </summary>
internal sealed class DashboardInsightsView : Control
{
    private const int BarHeight = 12;
    private const int RowHeight = 22;
    private const int BadgeHeight = 24;

    private static readonly Color DangerSoft = Color.FromArgb(255, 226, 223);
    private static readonly Color WarningSoft = Color.FromArgb(255, 238, 207);

    private DashboardInsights _insights = DashboardInsights.Empty;

    // 0 → 1 a ogni nuova analisi: le fasce colorate e le barrette crescono invece di comparire di colpo.
    private readonly AnimationClock _grow;

    public DashboardInsightsView()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.Surface;
        AccessibleRole = AccessibleRole.StaticText;
        AccessibleName = "Analisi del mese";
        _grow = new AnimationClock(this, 1f);
        _grow.Frame += (_, _) => Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _grow.Dispose();
        }
        base.Dispose(disposing);
    }

    public DashboardInsights Insights
    {
        get => _insights;
        set
        {
            var changed = !_insights.SameAs(value);
            _insights = value;
            AccessibleDescription = Describe(value);
            if (changed && value.HasData)
            {
                _grow.Jump(0f);
                _grow.AnimateTo(1f, Motion.Slow);
            }
            Invalidate();
        }
    }

    /// <summary>Ore e minuti come «12 h 30» (solo «8 h» se i minuti sono zero).</summary>
    public static string FormatMinutes(int minutes) =>
        minutes % 60 == 0 ? $"{minutes / 60:N0} h" : $"{minutes / 60:N0} h {minutes % 60:00}";

    private static string Describe(DashboardInsights insights) =>
        !insights.HasData
            ? "Nessun dato: genera l'anteprima."
            : $"Presenza {FormatMinutes(insights.PresenceMinutes)}, assenza {FormatMinutes(insights.AbsenceMinutes)}, " +
              $"{insights.CheckDays} giorni da controllare, {insights.NoticeDays} da verificare.";

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Surface);
        var width = ClientSize.Width;

        if (!_insights.HasData)
        {
            TextRenderer.DrawText(
                g,
                "Genera l'anteprima per vedere presenza, assenze e giorni da controllare.",
                Theme.Fonts.Small,
                new Rectangle(0, 0, width, ClientSize.Height),
                Theme.MutedInk,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        var y = 2;
        DrawBar(g, new Rectangle(0, y, width, BarHeight));
        y += BarHeight + 4;

        var summary = $"Presenza {FormatMinutes(_insights.PresenceMinutes)}   ·   Assenza {FormatMinutes(_insights.AbsenceMinutes)}  ({_insights.AbsenceRatio:P0})";
        TextRenderer.DrawText(
            g,
            summary,
            Theme.Fonts.Tiny,
            new Rectangle(0, y, width, 18),
            Theme.MutedInk,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        y += 22;

        var badgesNeeded = _insights.CheckDays + _insights.NoticeDays > 0;
        var reserved = badgesNeeded ? BadgeHeight + 4 : 0;
        var rows = Math.Min(_insights.Absences.Count, Math.Max(0, (ClientSize.Height - y - reserved) / RowHeight));
        var top = _insights.Absences.Count == 0 ? 1 : _insights.Absences.Max(share => share.Minutes);
        for (var index = 0; index < rows; index++)
        {
            DrawAbsenceRow(g, _insights.Absences[index], new Rectangle(0, y, width, RowHeight), top, _grow.Value);
            y += RowHeight;
        }

        if (badgesNeeded)
        {
            DrawBadges(g, new Rectangle(0, ClientSize.Height - BadgeHeight, width, BadgeHeight));
        }
    }

    private void DrawBar(Graphics g, Rectangle bounds)
    {
        using var track = RoundedPath(bounds, bounds.Height / 2);
        using var presence = new SolidBrush(Theme.Brand);
        g.FillPath(presence, track);
        if (_insights.AbsenceMinutes <= 0)
        {
            return;
        }

        var previous = g.Clip;
        g.SetClip(track, CombineMode.Intersect);
        var total = _insights.PresenceMinutes + _insights.AbsenceMinutes;
        var x = bounds.Right;
        // Le assenze partono da destra, ognuna con il proprio colore.
        foreach (var share in _insights.Absences)
        {
            var span = (int)Math.Round((double)bounds.Width * share.Minutes / total * _grow.Value);
            x -= span;
            using var brush = new SolidBrush(AbsencePalette.For(share.ColorIndex).Ink);
            g.FillRectangle(brush, x, bounds.Y, span + 1, bounds.Height);
        }
        g.Clip = previous;
    }

    private static void DrawAbsenceRow(Graphics g, AbsenceShare share, Rectangle row, int top, float grow)
    {
        var (fill, ink) = AbsencePalette.For(share.ColorIndex);
        using (var chip = new SolidBrush(fill))
        {
            var dot = new Rectangle(row.X, row.Y + (row.Height - 12) / 2, 12, 12);
            g.FillEllipse(chip, dot);
            using var pen = new Pen(ink, 1.5f);
            g.DrawEllipse(pen, dot);
        }

        var label = string.IsNullOrWhiteSpace(share.Policy) ? share.Code : $"{share.Code} · {share.Policy}";
        var hours = FormatMinutes(share.Minutes);
        var hoursWidth = TextRenderer.MeasureText(hours, Theme.Fonts.SmallBold).Width;
        TextRenderer.DrawText(
            g,
            label,
            Theme.Fonts.Small,
            new Rectangle(row.X + 20, row.Y, Math.Max(0, row.Width - hoursWidth - 130), row.Height),
            Theme.Ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        var barWidth = 90;
        var barRight = row.Right - hoursWidth - 10;
        var track = new Rectangle(barRight - barWidth, row.Y + (row.Height - 6) / 2, barWidth, 6);
        using (var back = new SolidBrush(Theme.Divider))
        using (var path = RoundedPath(track, 3))
        {
            g.FillPath(back, path);
        }
        var filled = Math.Max(6, (int)Math.Round((double)barWidth * share.Minutes / top * grow));
        using (var front = new SolidBrush(ink))
        using (var path = RoundedPath(new Rectangle(track.X, track.Y, filled, track.Height), 3))
        {
            g.FillPath(front, path);
        }

        TextRenderer.DrawText(
            g,
            hours,
            Theme.Fonts.SmallBold,
            new Rectangle(row.Right - hoursWidth, row.Y, hoursWidth, row.Height),
            Theme.Ink,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private void DrawBadges(Graphics g, Rectangle bounds)
    {
        var x = bounds.X;
        if (_insights.CheckDays > 0)
        {
            x += DrawBadge(g, x, bounds, $"{_insights.CheckDays} da controllare", DangerSoft, Theme.DangerInk) + 8;
        }
        if (_insights.NoticeDays > 0)
        {
            DrawBadge(g, x, bounds, $"{_insights.NoticeDays} da verificare", WarningSoft, Theme.WarningInk);
        }
    }

    private static int DrawBadge(Graphics g, int x, Rectangle bounds, string text, Color fill, Color ink)
    {
        var textSize = TextRenderer.MeasureText(text, Theme.Fonts.Pill, Size.Empty, TextFormatFlags.NoPadding);
        var rect = new Rectangle(x, bounds.Y + 2, textSize.Width + 20, bounds.Height - 4);
        using (var path = RoundedPath(rect, rect.Height / 2))
        using (var brush = new SolidBrush(fill))
        {
            g.FillPath(brush, path);
        }
        TextRenderer.DrawText(
            g,
            text,
            Theme.Fonts.Pill,
            rect,
            ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        return rect.Width;
    }

    private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
