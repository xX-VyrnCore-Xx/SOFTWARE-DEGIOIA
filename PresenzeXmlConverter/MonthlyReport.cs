using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>
/// Report PDF del mese da consegnare o archiviare: totali per dipendente (presenza, assenza, ore teoriche,
/// differenza, giorni scoperti), dettaglio delle assenze e elenco di ciò che va controllato. Si compone dagli
/// stessi dati del calendario e del riepilogo CSV, quindi i numeri coincidono ovunque.
/// </summary>
internal static class MonthlyReport
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    private static readonly PdfColor Ink = new(29, 29, 31);
    private static readonly PdfColor Muted = new(110, 110, 115);
    private static readonly PdfColor Brand = new(0, 122, 255);
    private static readonly PdfColor Line = new(209, 209, 214);
    private static readonly PdfColor Band = new(245, 245, 247);
    private static readonly PdfColor HeaderFill = new(232, 242, 255);
    private static readonly PdfColor Danger = new(214, 40, 30);
    private static readonly PdfColor Warning = new(176, 100, 0);
    private static readonly PdfColor Success = new(24, 110, 48);

    private const double Left = 40;
    private const double Right = PdfBuilder.A4Width - 40;
    private const double Bottom = PdfBuilder.A4Height - 46;
    private const double RowHeight = 17;

    private sealed class Layout(PdfBuilder document, string company, DateTime generated)
    {
        public PdfBuilder Document { get; } = document;
        public PdfPage Page { get; private set; } = null!;
        public double Y { get; set; }
        public string Company { get; } = company;
        public DateTime Generated { get; } = generated;

        public void NewPage()
        {
            Page = Document.AddPage();
            Page.Text(Left, 52, "Vyrn Presence", 15, bold: true, color: Brand);
            Page.TextRight(Right, 52, "Riepilogo ore", 15, bold: true);
            Page.Text(Left, 68, Company, 9.5, color: Muted);
            Page.TextRight(Right, 68, $"Generato il {Generated:dd/MM/yyyy HH:mm}", 9.5, color: Muted);
            Page.Line(Left, 78, Right, 78, 0.8, Line);
            Y = 98;
        }

        public void EnsureSpace(double height)
        {
            if (Y + height > Bottom)
            {
                NewPage();
            }
        }
    }

    /// <param name="company">Azienda mostrata in testa a ogni pagina (es. «Azienda AZ001 · Rossi Srl»).</param>
    public static byte[] Build(
        string company,
        IReadOnlyList<MonthlySummaryRow> rows,
        IReadOnlyList<CalendarAlert> alerts,
        DateTime generated)
    {
        var document = new PdfBuilder { Title = "Riepilogo ore" };
        var layout = new Layout(document, company, generated);
        layout.NewPage();

        DrawTiles(layout, rows, alerts);
        DrawTable(layout, rows);
        DrawAbsences(layout, rows);
        DrawAlerts(layout, alerts);
        DrawFooters(document);
        return document.Build();
    }

    private static string Hours(int minutes) => (minutes / 60m).ToString("0.##", Italian) + " h";

    private static string Signed(int minutes) => minutes == 0 ? "—" : (minutes > 0 ? "+" : "−") + Hours(Math.Abs(minutes));

    private static void DrawTiles(Layout layout, IReadOnlyList<MonthlySummaryRow> rows, IReadOnlyList<CalendarAlert> alerts)
    {
        var employees = rows.Select(row => row.EmployeeCode).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var checkDays = alerts
            .Where(alert => alert.Severity == AlertSeverity.Check)
            .Select(alert => (alert.EmployeeName, alert.Date))
            .Distinct()
            .Count();
        var tiles = new (string Label, string Value, PdfColor Color)[]
        {
            ("Dipendenti", employees.ToString(Italian), Ink),
            ("Ore registrate", Hours(rows.Sum(row => row.TotalMinutes)), Ink),
            ("Ore di assenza", Hours(rows.Sum(row => row.AbsenceMinutes)), Brand),
            ("Giorni da controllare", checkDays.ToString(Italian), checkDays > 0 ? Danger : Success)
        };
        var width = (Right - Left - (3 * 10)) / 4;
        for (var index = 0; index < tiles.Length; index++)
        {
            var x = Left + (index * (width + 10));
            layout.Page.Rectangle(x, layout.Y, width, 52, Band, Line);
            layout.Page.Text(x + 10, layout.Y + 18, tiles[index].Label, 8.5, color: Muted);
            layout.Page.Text(x + 10, layout.Y + 40, tiles[index].Value, 16, bold: true, color: tiles[index].Color);
        }
        layout.Y += 52 + 22;
    }

    private static void DrawTable(Layout layout, IReadOnlyList<MonthlySummaryRow> rows)
    {
        var showTheoretical = rows.Any(row => row.ExpectedMinutes is not null);
        // Margini destri delle colonne (indice 1 codice, 2 presenza, 3 assenza, 4 totale, 5 teoriche, 6 differenza,
        // 7 scoperti); il nome occupa lo spazio a sinistra.
        double[] rightEdges = showTheoretical
            ? [0, 225, 285, 340, 395, 450, 504, 549]
            : [0, 240, 340, 440, 549, 0, 0, 0];

        foreach (var month in rows.GroupBy(row => (row.Month.Year, row.Month.Month)).OrderBy(group => group.Key))
        {
            layout.EnsureSpace(RowHeight * 3);
            var title = new DateTime(month.Key.Year, month.Key.Month, 1).ToString("MMMM yyyy", Italian);
            layout.Page.Text(Left, layout.Y + 12, char.ToUpper(title[0], Italian) + title[1..], 12, bold: true);
            layout.Y += 20;
            DrawTableHeader(layout, showTheoretical, rightEdges);

            var index = 0;
            foreach (var row in month.OrderBy(item => item.EmployeeName, StringComparer.CurrentCultureIgnoreCase))
            {
                if (layout.Y + RowHeight > Bottom)
                {
                    layout.NewPage();
                    DrawTableHeader(layout, showTheoretical, rightEdges);
                }
                if (index++ % 2 == 1)
                {
                    layout.Page.Rectangle(Left, layout.Y, Right - Left, RowHeight, Band);
                }
                var baseline = layout.Y + 12;
                layout.Page.Text(Left + 6, baseline, PdfPage.Fit(row.EmployeeName, 130, 9), 9);
                layout.Page.TextRight(rightEdges[1], baseline, row.EmployeeCode, 9, color: Muted);
                layout.Page.TextRight(rightEdges[2], baseline, Hours(row.PresenceMinutes), 9);
                layout.Page.TextRight(rightEdges[3], baseline, Hours(row.AbsenceMinutes), 9);
                layout.Page.TextRight(rightEdges[4], baseline, Hours(row.TotalMinutes), 9, bold: true);
                if (showTheoretical)
                {
                    var difference = row.ExpectedMinutes is { } expected ? row.TotalMinutes - expected : 0;
                    layout.Page.TextRight(rightEdges[5], baseline, row.ExpectedMinutes is { } theoretical ? Hours(theoretical) : "—", 9, color: Muted);
                    layout.Page.TextRight(
                        rightEdges[6],
                        baseline,
                        row.ExpectedMinutes is null ? "—" : Signed(difference),
                        9,
                        bold: difference != 0,
                        color: difference == 0 ? Muted : Warning);
                    layout.Page.TextRight(
                        rightEdges[7],
                        baseline,
                        row.UncoveredDays?.ToString(Italian) ?? "—",
                        9,
                        bold: row.UncoveredDays is > 0,
                        color: row.UncoveredDays is > 0 ? Danger : Muted);
                }
                layout.Y += RowHeight;
            }
            layout.Page.Line(Left, layout.Y, Right, layout.Y, 0.6, Line);
            layout.Y += 22;
        }
    }

    private static void DrawTableHeader(Layout layout, bool showTheoretical, double[] rightEdges)
    {
        layout.Page.Rectangle(Left, layout.Y, Right - Left, RowHeight + 2, HeaderFill);
        var baseline = layout.Y + 12.5;
        layout.Page.Text(Left + 6, baseline, "Dipendente", 8.5, bold: true, color: Brand);
        layout.Page.TextRight(rightEdges[1], baseline, "Codice", 8.5, bold: true, color: Brand);
        layout.Page.TextRight(rightEdges[2], baseline, "Presenza", 8.5, bold: true, color: Brand);
        layout.Page.TextRight(rightEdges[3], baseline, "Assenza", 8.5, bold: true, color: Brand);
        layout.Page.TextRight(rightEdges[4], baseline, "Totale", 8.5, bold: true, color: Brand);
        if (showTheoretical)
        {
            layout.Page.TextRight(rightEdges[5], baseline, "Teoriche", 8.5, bold: true, color: Brand);
            layout.Page.TextRight(rightEdges[6], baseline, "Diff.", 8.5, bold: true, color: Brand);
            layout.Page.TextRight(rightEdges[7], baseline, "Scoperti", 8.5, bold: true, color: Brand);
        }
        layout.Y += RowHeight + 2;
    }

    private static void DrawAbsences(Layout layout, IReadOnlyList<MonthlySummaryRow> rows)
    {
        var withAbsences = rows.Where(row => row.Absences.Count > 0).ToArray();
        if (withAbsences.Length == 0)
        {
            return;
        }
        layout.EnsureSpace(60);
        layout.Page.Text(Left, layout.Y + 12, "Assenze per giustificativo", 12, bold: true);
        layout.Y += 24;
        foreach (var row in withAbsences.OrderBy(item => item.EmployeeName, StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.Month))
        {
            layout.EnsureSpace(RowHeight);
            var detail = string.Join(
                "  ·  ",
                row.Absences.Select(item => $"{item.Code}{(item.Policy.Length > 0 ? $" ({item.Policy})" : string.Empty)}: {Hours(item.Minutes)}"));
            var label = withAbsences.Select(item => item.Month).Distinct().Count() > 1
                ? $"{row.EmployeeName} · {row.Month.ToString("MMM yyyy", Italian)}"
                : row.EmployeeName;
            layout.Page.Text(Left + 6, layout.Y + 11, PdfPage.Fit(label, 150, 9, bold: true), 9, bold: true);
            layout.Page.Text(Left + 165, layout.Y + 11, PdfPage.Fit(detail, Right - Left - 165, 9), 9, color: Muted);
            layout.Y += RowHeight - 2;
        }
        layout.Y += 18;
    }

    private static void DrawAlerts(Layout layout, IReadOnlyList<CalendarAlert> alerts)
    {
        layout.EnsureSpace(60);
        layout.Page.Text(Left, layout.Y + 12, "Da controllare", 12, bold: true);
        layout.Y += 24;
        if (alerts.Count == 0)
        {
            layout.Page.Text(Left + 6, layout.Y + 10, "Nessuna anomalia rilevata nei movimenti.", 9, color: Success);
            layout.Y += 20;
            return;
        }

        const int maximum = 60;
        var ordered = alerts
            .OrderByDescending(alert => alert.Severity)
            .ThenBy(alert => alert.EmployeeName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(alert => alert.Date)
            .ToArray();
        foreach (var alert in ordered.Take(maximum))
        {
            layout.EnsureSpace(RowHeight);
            var color = alert.Severity == AlertSeverity.Check ? Danger : Warning;
            layout.Page.Rectangle(Left + 6, layout.Y + 4, 6, 6, color);
            var text = $"{alert.EmployeeName} · {alert.Date:dd/MM}: {alert.Text}";
            layout.Page.Text(Left + 20, layout.Y + 11, PdfPage.Fit(text, Right - Left - 20, 9), 9);
            layout.Y += RowHeight - 3;
        }
        if (ordered.Length > maximum)
        {
            layout.EnsureSpace(RowHeight);
            layout.Page.Text(Left + 20, layout.Y + 11, $"… e altri {ordered.Length - maximum} giorni segnalati (vedi il calendario).", 9, color: Muted);
            layout.Y += RowHeight;
        }
    }

    private static void DrawFooters(PdfBuilder document)
    {
        var count = document.Pages.Count;
        for (var index = 0; index < count; index++)
        {
            var page = document.Pages[index];
            page.Line(Left, PdfBuilder.A4Height - 38, Right, PdfBuilder.A4Height - 38, 0.6, Line);
            page.Text(Left, PdfBuilder.A4Height - 24, "Documento generato da Vyrn Presence · i dati vengono dal foglio importato", 8, color: Muted);
            page.TextRight(Right, PdfBuilder.A4Height - 24, $"Pagina {index + 1} di {count}", 8, color: Muted);
        }
    }
}
