namespace PresenzeXmlConverter.Tests;

using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;

/// <summary>
/// Regressione grafica: rende la finestra principale e i dialoghi e verifica
/// che non compaiano colori di sistema non temizzati (la striscia grigia del
/// TabControl nativo, per esempio) né testo tagliato fuori dalla sua scatola.
/// In ambienti senza desktop la verifica viene saltata.
/// </summary>
public class UiGraphicsRegressionTests
{
    [Fact]
    public void MainWindowIsFullyThemedAndNothingIsClipped()
    {
        var result = RunOnStaThread(() =>
        {
            var report = new StringBuilder();
            var form = new MainForm();
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-4000, -4000);
            form.Show();
            Application.DoEvents();
            form.PerformLayout();
            Application.DoEvents();
            var clipped = new List<string>();
            var overlaps = new List<string>();
            var gray = 0.0;
            var grayRegions = string.Empty;
            // Ogni scheda dell'area di revisione viene disegnata e controllata:
            // una scheda lasciata fuori dal test può nascondere un testo tagliato.
            foreach (var (select, what) in RenderStates(form))
            {
                select();
                Application.DoEvents();
                form.PerformLayout();
                Application.DoEvents();
                // Due controlli nella stessa cella di una tabella si sovrappongono e
                // scompaginano le altezze delle righe: qui vengono segnalati subito.
                var overlapsHere = CollectCellOverlaps(form, report);
                if (!string.IsNullOrEmpty(overlapsHere))
                {
                    overlaps.Add($"{what}:{Environment.NewLine}{overlapsHere}");
                }
                var clippedHere = CollectClippedLabels(form, report);
                if (!string.IsNullOrEmpty(clippedHere))
                {
                    clipped.Add($"{what}:{Environment.NewLine}{clippedHere}");
                }
                var snapshot = GrayReport(Render(form));
                if (snapshot.Ratio > gray)
                {
                    gray = snapshot.Ratio;
                    grayRegions = $"{what}: {snapshot.Regions}";
                }
            }
            // Diagnostica nel messaggio di errore: se il layout viene compresso
            // serve sapere con quale schermo e quale finestra è successo.
            report.AppendLine(
                $"Diagnostica: finestra {form.ClientSize.Width}x{form.ClientSize.Height}, " +
                $"schermo {Screen.PrimaryScreen?.Bounds.Width}x{Screen.PrimaryScreen?.Bounds.Height}, " +
                $"area di lavoro {Screen.FromControl(form).WorkingArea.Width}x{Screen.FromControl(form).WorkingArea.Height}, " +
                $"scala del sistema {form.DeviceDpi} dpi.");
            form.Dispose();
            return (
                clipped: string.Join(Environment.NewLine, clipped),
                overlaps: string.Join(Environment.NewLine, overlaps),
                gray: gray,
                regions: grayRegions,
                report: report.ToString());
        });
        if (result is null)
        {
            return;
        }
        Assert.True(
            string.IsNullOrEmpty(result.Value.clipped),
            result.Value.clipped + Environment.NewLine + result.Value.report);
        Assert.True(
            string.IsNullOrEmpty(result.Value.overlaps),
            result.Value.overlaps + Environment.NewLine + result.Value.report);
        Assert.True(
            result.Value.gray < 0.002,
            $"Colori di sistema non temizzati nel rendering ({result.Value.gray:P2}). " +
            $"Aree grigie: {result.Value.regions}. {result.Value.report}");
    }

    /// <summary>
    /// Titolo e tooltip della tray portano la versione in esecuzione: e' il modo
    /// piu' rapido per sapere quale build e' installata, senza aprire nulla.
    /// </summary>
    [Fact]
    public void MainWindowTitleCarriesTheRunningVersion()
    {
        var result = RunOnStaThread(() =>
        {
            using var form = new MainForm();
            return (title: form.Text, ok: true);
        });
        if (result is null)
        {
            return;
        }
        var version = typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? string.Empty;
        Assert.False(string.IsNullOrWhiteSpace(version), "la versione dell'assembly non e' leggibile");
        Assert.Equal($"PresenzeFlow {version}", result.Value.title);
    }

    /// <summary>
    /// Ogni sezione della barra di navigazione viene disegnata e controllata.
    /// Una sezione che non è quella aperta all'avvio non verrebbe mai resa: un
    /// testo tagliato o due controlli sovrapposti (dashboard, codici, storico)
    /// resterebbero invisibili fino al primo utente.
    /// </summary>
    [Fact]
    public void EverySectionOfTheWindowIsFullyThemedAndNothingIsClipped()
    {
        var result = RunOnStaThread(() =>
        {
            var report = new StringBuilder();
            var problems = new List<string>();
            var overlaps = new List<string>();
            var gray = 0.0;
            var grayRegions = string.Empty;
            var form = new MainForm();
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-4000, -4000);
            form.Show();
            Application.DoEvents();
            form.PerformLayout();
            Application.DoEvents();
            for (var section = 0; section < form.SectionCount; section++)
            {
                form.ShowSection(section);
                // Dentro la sezione vengono selezionate anche le sue schede (area
                // di revisione e griglie dei codici): nessuna resta fuori.
                foreach (var (select, what) in RenderStates(form))
                {
                    select();
                    Application.DoEvents();
                    form.PerformLayout();
                    Application.DoEvents();
                    var overlapsHere = CollectCellOverlaps(form, report);
                    if (!string.IsNullOrEmpty(overlapsHere))
                    {
                        overlaps.Add($"sezione {section}, {what}:{Environment.NewLine}{overlapsHere}");
                    }
                    var clippedHere = CollectClippedLabels(form, report);
                    if (!string.IsNullOrEmpty(clippedHere))
                    {
                        problems.Add($"sezione {section}, {what}:{Environment.NewLine}{clippedHere}");
                    }
                    var snapshot = GrayReport(Render(form));
                    if (snapshot.Ratio > gray)
                    {
                        gray = snapshot.Ratio;
                        grayRegions = $"sezione {section}, {what}: {snapshot.Regions}";
                    }
                }
            }
            report.AppendLine(
                $"Diagnostica: finestra {form.ClientSize.Width}x{form.ClientSize.Height}, " +
                $"{form.SectionCount} sezioni, area di lavoro " +
                $"{Screen.FromControl(form).WorkingArea.Width}x{Screen.FromControl(form).WorkingArea.Height}.");
            var sectionCount = form.SectionCount;
            form.Dispose();
            return (
                sections: sectionCount,
                clipped: string.Join(Environment.NewLine, problems),
                overlaps: string.Join(Environment.NewLine, overlaps),
                gray,
                regions: grayRegions,
                report: report.ToString());
        });
        if (result is null)
        {
            return;
        }
        // Un test che non disegna nulla non verifica nulla: con la finestra vuota
        // (interfaccia mai costruita) il ciclo sulle sezioni non girava e il test
        // risultava verde. Le sezioni devono esserci tutte e quattro.
        Assert.True(
            result.Value.sections >= 4,
            $"La finestra ha {result.Value.sections} sezioni invece di 4: l'interfaccia non è stata costruita. " +
            result.Value.report);
        Assert.True(
            string.IsNullOrEmpty(result.Value.clipped),
            result.Value.clipped + Environment.NewLine + result.Value.report);
        Assert.True(
            string.IsNullOrEmpty(result.Value.overlaps),
            result.Value.overlaps + Environment.NewLine + result.Value.report);
        Assert.True(
            result.Value.gray < 0.002,
            $"Colori di sistema non temizzati nelle sezioni ({result.Value.gray:P2}). " +
            $"Aree grigie: {result.Value.regions}. {result.Value.report}");
    }

    [Fact]
    public void DialogsAreFullyThemed()
    {
        var result = RunOnStaThread(() =>
        {
            var report = new StringBuilder();
            var problems = new List<string>();
            var gray = 0.0;
            var grayRegions = string.Empty;
            foreach (var dialog in new Form[]
                     {
                         new CloseBehaviorDialog(),
                         new ColumnMappingDialog(new[] { "Nominativo", "Tipo assenza", "Dal", "Al", "Ore" }, null),
                         new MappingEditorDialog(
                             new Dictionary<string, string> { ["Rossi Mario"] = "0012" },
                             new Dictionary<string, string> { ["Ferie"] = "F01", ["Malattia"] = "" },
                             new[] { "Malattia" },
                             new[] { "Malattia" },
                             new Dictionary<string, string> { ["Malattia"] = "M01" }),
                         new UpdateProgressDialog("1.0.0"),
                         // Impostazioni: ogni scheda entra intera nella finestra, quindi
                         // il dialogo non ha barre di scorrimento di sistema.
                         new SettingsDialog(
                             GenerationOptions.Default,
                             DateTime.Now.Year,
                             true),
                         // Novità di una Release: note lunghe, con la barra di
                         // scorrimento disegnata con il tema dell'app.
                         new ReleaseNotesDialog(
                             "Novità di PresenzeFlow",
                             "Riepilogo delle modifiche di questa versione.",
                             LongReleaseNotes,
                             true)
                     })
            {
                using (dialog)
                {
                    dialog.StartPosition = FormStartPosition.Manual;
                    dialog.Location = new Point(-4000, -4000);
                    dialog.Show();
                    Application.DoEvents();
                    dialog.PerformLayout();
                    foreach (var (select, what) in RenderStates(dialog))
                    {
                        select();
                        Application.DoEvents();
                        dialog.PerformLayout();
                        var clippedHere = CollectClippedLabels(dialog, report);
                        if (!string.IsNullOrEmpty(clippedHere))
                        {
                            problems.Add($"{dialog.GetType().Name}, {what}:{Environment.NewLine}{clippedHere}");
                        }
                        var snapshot = GrayReport(Render(dialog));
                        if (snapshot.Ratio > gray)
                        {
                            gray = snapshot.Ratio;
                            grayRegions = $"{dialog.GetType().Name}, {what}: {snapshot.Regions}";
                        }
                    }
                    dialog.Hide();
                }
            }
            return (
                problems: string.Join(Environment.NewLine, problems),
                gray: gray,
                regions: grayRegions,
                report: report.ToString());
        });
        if (result is null)
        {
            return;
        }
        Assert.True(
            result.Value.problems.Length == 0,
            result.Value.problems + Environment.NewLine + result.Value.report);
        // Resta solo il pulsante a tendina nativo delle caselle combinate,
        // che Windows disegna sempre in grisaglia.
        Assert.True(
            result.Value.gray < 0.01,
            $"Colori di sistema non temizzati nei dialoghi ({result.Value.gray:P2}). " +
            $"Aree grigie: {result.Value.regions}. {result.Value.report}");
    }

    /// <summary>
    /// Stati in cui disegnare un dialogo: ogni scheda, se ne ha (un dialogo senza
    /// schede viene disegnato una volta sola). Rende visibile la verifica anche
    /// sulle parti che non comparirebbero aprendo la finestra.
    /// </summary>
    private static IEnumerable<(Action Select, string What)> RenderStates(Form dialog)
    {
        var tabs = new List<ThemedTabControl>();
        CollectTabs(dialog, tabs);
        if (tabs.Count == 0)
        {
            yield return (() => { }, "finestra");
            yield break;
        }
        foreach (var tab in tabs)
        {
            for (var index = 0; index < tab.PageCount; index++)
            {
                var target = index;
                var host = tab;
                yield return (() => host.SelectedIndex = target, $"scheda \"{index + 1} di {tab.PageCount}\"");
            }
        }
    }

    private static void CollectTabs(Control root, List<ThemedTabControl> found)
    {
        if (root is ThemedTabControl tab)
        {
            found.Add(tab);
        }
        foreach (Control child in root.Controls)
        {
            CollectTabs(child, found);
        }
    }

    /// <summary>
    /// Note di una Release abbastanza lunghe da non entrare nella finestra: è il
    /// caso in cui la barra di scorrimento deve essere disegnata dal programma e
    /// non da Windows.
    /// </summary>
    private const string LongReleaseNotes = """
        ### Grafica Windows
        - La barra del titolo segue il tema dell'app: era chiara sopra l'intestazione scura.
        - Titolo e angoli arrotondati si adattano alla versione di Windows in uso.
        - Il titolo della finestra mostra la versione in esecuzione.

        ### Trascinamento del foglio Excel
        - Il file si può trascinare direttamente sulla finestra, senza aprire il dialogo.
        - Vengono accettati solo fogli Excel esistenti: cartelle e altri file sono ignorati.
        - Dialogo di apertura e trascinamento passano dallo stesso percorso.

        ### Firma digitale
        - Il workflow di Release non pubblica più eseguibili non firmati.
        - Il certificato pubblico segue la Release e viene allegato.
        - In Impostazioni si legge se l'eseguibile in uso è firmato.

        ### Analisi del foglio
        - Le righe problematiche vengono segnalate e non bloccate.
        - Il foglio di un file con più fogli si cambia senza reimportare il file.
        - Intestazioni, date e durate vengono riconosciute in più formati.

        ### Controlli
        - Elenco unico ordinato per gravità, con doppio clic per raggiungere il movimento.
        - I controlli si aggiornano a ogni modifica della griglia.
        - Copia elenco per condividere le segnalazioni con chi le deve risolvere.

        ### Impostazioni
        - Giornata standard, sabato lavorativo e chiusure aziendali configurabili.
        - Token delle Release verificato e attivato senza riavviare il programma.
        - Rilevamento automatico di sabati e ore effettive dalle date.
        """;

    private static T? RunOnStaThread<T>(Func<T> probe)
        where T : struct
    {
        var result = default(T);
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
        // Sessione senza desktop (runner headless): la verifica grafica non può
        // eseguire e non deve far fallire la suite.
        return failure is null ? result : null;
    }

    private static Bitmap Render(Form form)
    {
        var bitmap = new Bitmap(form.Width, form.Height, PixelFormat.Format24bppRgb);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        return bitmap;
    }

    /// <summary>
    /// Colori di sistema che sfuggono al tema: il grigio delle finestre di Windows
    /// (barre di scorrimento native, pulsanti a tendina, superfici non ridipinte).
    /// </summary>
    private static bool IsSystemGray(Color color) =>
        color.R is 240 or 241 or 243 or 245 && color.G == color.R && color.B == color.R;

    /// <summary>
    /// Quota di grigio di sistema nel rendering e aree che lo producono: dal
    /// messaggio di errore si capisce subito quale controllo resta fuori tema
    /// (una barra di scorrimento, per esempio, è una striscia alta e stretta).
    /// </summary>
    private static (double Ratio, string Regions) GrayReport(Bitmap bitmap)
    {
        var total = 0;
        var gray = 0;
        // Fasce orizzontali consecutive di grigio, con il loro rettangolo.
        var bands = new List<(int Top, int Bottom, int Left, int Right, int Count)>();
        for (var y = 0; y < bitmap.Height; y += 2)
        {
            var left = int.MaxValue;
            var right = -1;
            var count = 0;
            for (var x = 0; x < bitmap.Width; x += 2)
            {
                total++;
                if (!IsSystemGray(bitmap.GetPixel(x, y)))
                {
                    continue;
                }
                gray++;
                count++;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
            if (count == 0)
            {
                continue;
            }
            if (bands.Count > 0 && bands[^1].Bottom == y - 2)
            {
                var last = bands[^1];
                bands[^1] = (last.Top, y, Math.Min(last.Left, left), Math.Max(last.Right, right), last.Count + count);
            }
            else
            {
                bands.Add((y, y, left, right, count));
            }
        }
        bitmap.Dispose();

        var regions = string.Join(
            "; ",
            bands
                .OrderByDescending(band => band.Count)
                .Take(5)
                .Select(band =>
                    $"{band.Count * 4:N0} px fra ({band.Left},{band.Top}) e ({band.Right + 1},{band.Bottom + 1})"));
        return (total == 0 ? 0 : gray * 1.0 / total, regions.Length == 0 ? "nessuna" : regions);
    }

    /// <summary>
    /// Contenitore e cella del controllo: nel messaggio di errore fa capire subito
    /// se la colpa è di una riga troppo bassa, di una cella occupata o di un
    /// contenitore rimpicciolito.
    /// </summary>
    private static string DescribeParent(Control control)
    {
        if (control.Parent is not Control parent)
        {
            return string.Empty;
        }
        var details = $" Contenitore {parent.GetType().Name} {parent.ClientSize.Width}x{parent.ClientSize.Height}";
        if (parent is TableLayoutPanel table)
        {
            var row = table.GetRow(control);
            var column = table.GetColumn(control);
            var style = row >= 0 && row < table.RowStyles.Count ? table.RowStyles[row] : null;
            details += style is null
                ? $" cella ({column},{row}) fuori dalle righe dichiarate"
                : $" cella ({column},{row}) riga {style.SizeType} {style.Height} su {table.RowStyles.Count} righe";
        }
        if (parent.Parent is Control grandparent)
        {
            details += $" dentro {grandparent.GetType().Name} {grandparent.ClientSize.Width}x{grandparent.ClientSize.Height}";
        }
        return details;
    }

    /// <summary>
    /// Controlli diversi nella stessa cella di una tabella: è un errore di layout
    /// che si vede solo a schermo (controlli sovrapposti) e che sballa anche le
    /// altezze delle righe.
    /// </summary>
    private static string CollectCellOverlaps(Control root, StringBuilder report)
    {
        var problems = new List<string>();
        WalkCells(root, problems);
        foreach (var problem in problems.Take(5))
        {
            report.AppendLine(problem);
        }
        return string.Join(Environment.NewLine, problems);
    }

    private static void WalkCells(Control control, List<string> problems)
    {
        if (control is TableLayoutPanel table)
        {
            foreach (var cell in table.Controls
                         .Cast<Control>()
                         .GroupBy(child => table.GetCellPosition(child))
                         .Where(group => group.Count() > 1))
            {
                problems.Add(
                    $"Più controlli nella stessa cella ({cell.Key.Column},{cell.Key.Row}): " +
                    string.Join(" + ", cell.Select(DescribeControl)) + ".");
            }
        }
        foreach (Control child in control.Controls)
        {
            WalkCells(child, problems);
        }
    }

    private static string DescribeControl(Control control) => control switch
    {
        Label label => $"etichetta \"{label.Text}\"",
        Button button => $"pulsante \"{button.Text}\"",
        _ => control.GetType().Name
    };

    /// <summary>Ritua le etichette il cui testo non sta nella propria scatola.</summary>
    private static string CollectClippedLabels(Control root, StringBuilder report)
    {
        var problems = new List<string>();
        Walk(root, problems);
        foreach (var problem in problems.Take(5))
        {
            report.AppendLine(problem);
        }
        return string.Join(Environment.NewLine, problems);
    }

    private static void Walk(Control control, List<string> problems)
    {
        if (control is Label label && !label.AutoSize && label.Text.Length > 0 && label.Visible)
        {
            var measured = TextRenderer.MeasureText(
                label.Text,
                label.Font,
                new Size(Math.Max(1, label.ClientSize.Width), int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
            if (measured.Height > label.ClientSize.Height + 2)
            {
                problems.Add(
                    $"Testo tagliato in “{label.Text}”: servono {measured.Height}px, la scatola è {label.ClientSize.Height}px." +
                    DescribeParent(label));
            }
        }
        foreach (Control child in control.Controls)
        {
            Walk(child, problems);
        }
    }

}
