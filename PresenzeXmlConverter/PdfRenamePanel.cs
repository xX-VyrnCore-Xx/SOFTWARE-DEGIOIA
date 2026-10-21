namespace PresenzeXmlConverter;

/// <summary>
/// Sezione «Rinomina PDF»: si sceglie (o si trascina) uno ZIP o dei PDF, ogni pagina è un cliente e il suo
/// codice è nel piè di pagina. Il programma legge i codici, mostra in tabella cosa farà e segnala ciò che non torna
/// (codice mancante, doppio, di forma diversa); i codici si correggono a mano nella tabella. Solo alla conferma scrive
/// un nuovo ZIP con un PDF per pagina chiamato come il codice, poi lo riapre e verifica ogni file. L'originale non si tocca.
/// </summary>
internal sealed class PdfRenamePanel : UserControl
{
    private const string Dialog = "Rinomina PDF";
    private const int ColumnStatus = 0;
    private const int ColumnSource = 1;
    private const int ColumnPage = 2;
    private const int ColumnRead = 3;
    private const int ColumnCode = 4;
    private const int ColumnNote = 5;

    private readonly ThemedGrid _grid = new();
    private readonly TextBlock _summary = new();
    private readonly Button _saveButton;
    private readonly List<PdfRenameItem> _items = [];
    private string _sourceLabel = "pdf";
    private string? _sourceFolder;

    public PdfRenamePanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Canvas;
        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 0, 0, 8),
            BackColor = Theme.Canvas
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            BackColor = Theme.Canvas,
            Margin = new Padding(0, 0, 0, 8)
        };
        var choose = Theme.CreateButton("Scegli ZIP o PDF…", Theme.ButtonTone.Neutral);
        choose.Click += (_, _) => ChooseFiles();
        _saveButton = Theme.CreateButton("Salva ZIP rinominato…", Theme.ButtonTone.Primary);
        _saveButton.Enabled = false;
        _saveButton.Click += (_, _) => SaveZip();
        _saveButton.Margin = new Padding(10, 3, 0, 3);
        choose.Margin = new Padding(0, 3, 0, 3);
        buttons.Controls.Add(choose);
        buttons.Controls.Add(_saveButton);
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.Controls.Add(buttons, 0, 0);

        _summary.Dock = DockStyle.Fill;
        _summary.Wrap = true;
        _summary.ForeColor = Theme.InkSecondary;
        _summary.Font = Theme.Fonts.Ui;
        _summary.Margin = new Padding(0, 0, 0, 6);
        _summary.Text = "Nessun file scelto.";
        top.Controls.Add(_summary, 0, 1);

        BuildGrid();
        var gridHost = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.Surface,
            SurfaceBorder = Theme.Divider,
            Radius = 12,
            Padding = new Padding(2, 2, 2, 2),
            Margin = new Padding(0, 0, 0, 8)
        };
        gridHost.Controls.Add(_grid);
        Controls.Add(gridHost);
        Controls.Add(top);
    }

    private void BuildGrid()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Stato", FillWeight = 14, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "File", FillWeight = 22, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Pagina", FillWeight = 8, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Letto", FillWeight = 14, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Codice (modificabile)", FillWeight = 16 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nota", FillWeight = 40, ReadOnly = true });
        _grid.CellEndEdit += (_, e) =>
        {
            if (e.ColumnIndex == ColumnCode && e.RowIndex >= 0 && e.RowIndex < _items.Count)
            {
                _items[e.RowIndex].FinalCode = Convert.ToString(_grid.Rows[e.RowIndex].Cells[ColumnCode].Value)?.Trim();
                PdfRenamePlanner.Recheck(_items);
                RefreshRows();
            }
        };
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.Any(IsSupported)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            LoadPaths(paths.Where(IsSupported).ToArray());
        }
    }

    private static bool IsSupported(string path) =>
        path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private void ChooseFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Scegli lo ZIP o i PDF da rinominare",
            Filter = "ZIP o PDF (*.zip;*.pdf)|*.zip;*.pdf|Tutti i file (*.*)|*.*",
            Multiselect = true
        };
        dialog.ClientGuid = FileDialogs.ClientId;
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            LoadPaths(dialog.FileNames);
        }
    }

    private void LoadPaths(string[] paths)
    {
        if (paths.Length == 0)
        {
            return;
        }
        var unreadable = new List<string>();
        try
        {
            UseWaitCursor = true;
            Application.DoEvents();
            var files = PdfRenamePlanner.LoadFiles(paths);
            _items.Clear();
            _items.AddRange(PdfRenamePlanner.Build(files, unreadable));
            _sourceLabel = Path.GetFileNameWithoutExtension(paths[0]);
            _sourceFolder = Path.GetDirectoryName(paths[0]);
            RefreshRows();
            if (files.Count == 0)
            {
                _summary.Text = "Non ho trovato PDF nei file scelti.";
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(FindForm(), "Non riesco a leggere il file: " + exception.Message, Dialog, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
        if (unreadable.Count > 0)
        {
            MessageBox.Show(
                FindForm(),
                "Questi PDF non si sono potuti leggere e sono stati saltati:" + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, unreadable),
                Dialog,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void RefreshRows()
    {
        _grid.Rows.Clear();
        foreach (var item in _items)
        {
            var status = item.Status switch
            {
                PdfCodeStatus.Ok => "✓ Giusto",
                PdfCodeStatus.Check => "⚠ Da controllare",
                _ => "✗ Manca il codice"
            };
            var index = _grid.Rows.Add(status, item.SourceName, item.Page, item.Code ?? "—", item.FinalCode ?? string.Empty, item.Note);
            var row = _grid.Rows[index];
            row.DefaultCellStyle.BackColor = item.Status switch
            {
                PdfCodeStatus.Check => Theme.WarningSoft,
                PdfCodeStatus.Missing => Theme.DangerSoft,
                _ => Theme.Surface
            };
            row.Cells[ColumnStatus].Style.ForeColor = item.Status switch
            {
                PdfCodeStatus.Ok => Theme.SuccessInk,
                PdfCodeStatus.Check => Theme.WarningInk,
                _ => Theme.DangerInk
            };
        }
        var ok = _items.Count(item => item.Status == PdfCodeStatus.Ok);
        var check = _items.Count(item => item.Status == PdfCodeStatus.Check);
        var missing = _items.Count(item => item.Status == PdfCodeStatus.Missing);
        _summary.Text = _items.Count == 0
            ? "Nessuna pagina trovata."
            : $"{_items.Count} pagine · {ok} giuste · {check} da controllare · {missing} senza codice. " +
              (missing > 0 ? "Scrivi il codice nella tabella: le pagine senza codice non verranno salvate. " : string.Empty) +
              "Controlla le righe gialle e rosse prima di salvare.";
        _saveButton.Enabled = _items.Any(item => item.FileName is not null);
    }

    private void SaveZip()
    {
        var withName = _items.Count(item => item.FileName is not null);
        var skipped = _items.Count - withName;
        if (skipped > 0 &&
            MessageBox.Show(
                FindForm(),
                $"{skipped} pagine senza codice non verranno salvate. Procedo con le altre {withName}?",
                Dialog,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = "Salva lo ZIP con i PDF rinominati",
            Filter = "File ZIP (*.zip)|*.zip",
            FileName = _sourceLabel + "-rinominati.zip",
            InitialDirectory = _sourceFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            OverwritePrompt = true
        };
        dialog.ClientGuid = FileDialogs.ClientId;
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }
        try
        {
            UseWaitCursor = true;
            Application.DoEvents();
            var written = PdfRenameWriter.WriteZip(_items, dialog.FileName);
            var problems = PdfRenameWriter.Verify(_items, dialog.FileName);
            UseWaitCursor = false;
            if (problems.Count == 0)
            {
                MessageBox.Show(
                    FindForm(),
                    $"Fatto: {written} PDF salvati in {Path.GetFileName(dialog.FileName)}." + Environment.NewLine +
                    "Ho riaperto lo ZIP e controllato ogni file: una pagina ciascuno, con lo stesso testo dell'originale.",
                    Dialog,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    FindForm(),
                    "Lo ZIP è stato scritto ma il controllo ha trovato dei problemi:" + Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, problems.Take(15)),
                    Dialog,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            UseWaitCursor = false;
            MessageBox.Show(FindForm(), "Non riesco a scrivere lo ZIP: " + exception.Message, Dialog, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }
}
