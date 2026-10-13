using System.Globalization;

namespace PresenzeXmlConverter;

/// <summary>
/// Regole di conversione di un'azienda. Con «regole proprie» scegliere l'azienda in conversione
/// applica da solo giornata standard, sabato, rilevamento automatico e chiusure di quella azienda;
/// senza, valgono le regole generali delle Impostazioni.
/// </summary>
internal sealed class CompanyRulesDialog : Form
{
    private readonly int _referenceYear;
    private readonly GenerationOptions _general;
    private readonly CheckBox _own = new();
    private readonly RoundedField _hours = new();
    private readonly CheckBox _saturdays = new();
    private readonly CheckBox _autoDetect = new();
    private readonly TextBox _holidays = new();
    private readonly Label _error = new();
    private readonly Label _holidaysLabel = new();
    private readonly Label _hoursLabel = new();

    public CompanyRulesDialog(CompanyRecord company, GenerationOptions generalOptions, int referenceYear)
    {
        _general = generalOptions;
        _referenceYear = referenceYear;
        Text = $"Regole di conversione · {company.Label}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(560, 430);
        Theme.StyleDialog(this);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 9,
            Padding = new Padding(20, 16, 20, 8),
            BackColor = Theme.Canvas
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 40, 34, 24, 46, 34, 34, 24, 90, 24 })
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        }

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Come si calcolano le ore per questa azienda. Senza regole proprie valgono quelle generali delle Impostazioni.",
            ForeColor = Theme.MutedInk,
            Font = Theme.Fonts.Small
        }, 0, 0);

        StyleCheck(_own, "Usa regole proprie per questa azienda");
        _own.CheckedChanged += (_, _) => UpdateEnabled();
        layout.Controls.Add(_own, 0, 1);

        _hoursLabel.Text = "Ore di una giornata standard";
        StyleLabel(_hoursLabel);
        layout.Controls.Add(_hoursLabel, 0, 2);
        _hours.MaxLength = 5;
        _hours.Dock = DockStyle.Fill;
        _hours.InputFont = Theme.Fonts.Field;
        _hours.Placeholder = "es. 8  oppure  7,5";
        _hours.Input.AccessibleName = "Ore di una giornata standard dell'azienda";
        _hours.Margin = new Padding(0, 2, 0, 8);
        layout.Controls.Add(_hours, 0, 3);

        StyleCheck(_saturdays, "Il sabato è sempre lavorativo");
        layout.Controls.Add(_saturdays, 0, 4);
        StyleCheck(_autoDetect, "Rileva in automatico sabati e ore dalle date (consigliato)");
        layout.Controls.Add(_autoDetect, 0, 5);

        _holidaysLabel.Text = "Chiusure aziendali (una data per riga o separate da virgola, es. 02/06, 24/12)";
        StyleLabel(_holidaysLabel);
        layout.Controls.Add(_holidaysLabel, 0, 6);
        _holidays.Multiline = true;
        _holidays.BorderStyle = BorderStyle.None;
        _holidays.BackColor = Theme.FieldFill;
        _holidays.ForeColor = Theme.Ink;
        _holidays.Font = Theme.Fonts.Small;
        _holidays.AccessibleName = "Chiusure aziendali dell'azienda";
        var frame = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            SurfaceFill = Theme.FieldFill,
            SurfaceBorder = Theme.DividerStrong,
            Radius = 8,
            Padding = new Padding(8, 4, 8, 4),
            Margin = new Padding(0, 2, 0, 6)
        };
        _holidays.Dock = DockStyle.Fill;
        frame.Controls.Add(_holidays);
        layout.Controls.Add(frame, 0, 7);

        _error.Dock = DockStyle.Fill;
        _error.ForeColor = Theme.DangerInk;
        _error.Font = Theme.Fonts.Small;
        layout.Controls.Add(_error, 0, 8);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Theme.Canvas,
            Padding = new Padding(12)
        };
        var save = Theme.CreateButton("Salva regole", Theme.ButtonTone.Primary);
        save.Size = new Size(150, 36);
        var cancel = Theme.CreateButton("Annulla", Theme.ButtonTone.Neutral);
        cancel.Size = new Size(120, 36);
        cancel.DialogResult = DialogResult.Cancel;
        save.Click += (_, _) => TryCommit();
        actions.Controls.Add(save);
        actions.Controls.Add(cancel);

        Controls.Add(layout);
        Controls.Add(actions);
        AcceptButton = save;
        CancelButton = cancel;

        var current = company.Rules;
        _own.Checked = current is not null;
        Fill(current ?? CompanyRules.From(generalOptions, referenceYear));
        UpdateEnabled();
    }

    /// <summary>Regole scelte (null = quelle generali). Valide solo dopo «Salva regole».</summary>
    public CompanyRules? Rules { get; private set; }

    private static void StyleCheck(CheckBox box, string text)
    {
        box.Text = text;
        box.Dock = DockStyle.Fill;
        box.TextAlign = ContentAlignment.MiddleLeft;
        box.ForeColor = Theme.InkSecondary;
        box.Font = Theme.Fonts.Small;
        box.Margin = Padding.Empty;
        box.AccessibleName = text;
    }

    private static void StyleLabel(Label label)
    {
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.BottomLeft;
        label.ForeColor = Theme.InkSecondary;
        label.Font = Theme.Fonts.Small;
    }

    private void Fill(CompanyRules rules)
    {
        _hours.Value = rules.HoursPerDay.ToString("0.##", CultureInfo.CurrentCulture);
        _saturdays.Checked = rules.IncludeSaturdays;
        _autoDetect.Checked = rules.AutoDetect;
        _holidays.Text = rules.Holidays.Replace(", ", Environment.NewLine, StringComparison.Ordinal);
    }

    private void UpdateEnabled()
    {
        var on = _own.Checked;
        _hours.Enabled = on;
        _saturdays.Enabled = on;
        _autoDetect.Enabled = on;
        _holidays.Enabled = on;
        _hoursLabel.Enabled = on;
        _holidaysLabel.Enabled = on;
        if (!on)
        {
            // Senza regole proprie si vedono quelle generali (sola lettura).
            Fill(CompanyRules.From(_general, _referenceYear));
        }
        _error.Text = string.Empty;
    }

    private void TryCommit()
    {
        if (!_own.Checked)
        {
            Rules = null;
            DialogResult = DialogResult.OK;
            Close();
            return;
        }
        if (!GenerationSettings.TryParseHoursPerDay(_hours.Value, out var hours))
        {
            _error.Text =
                $"Le ore di una giornata devono essere un numero fra {GenerationSettings.MinimumHoursPerDay:0.##} e " +
                $"{GenerationSettings.MaximumHoursPerDay:0.##} (es. 8 oppure 7,5).";
            _hours.FocusInput();
            return;
        }
        var holidays = GenerationSettings.ParseHolidaysDetailed(_holidays.Text, _referenceYear, out var ignored);
        if (ignored.Count > 0)
        {
            _error.Text = $"Date non riconosciute: {string.Join(", ", ignored.Take(4))}. Usa gg/mm o gg/mm/aaaa.";
            return;
        }
        Rules = new CompanyRules(
            hours,
            _saturdays.Checked,
            _autoDetect.Checked,
            GenerationSettings.FormatHolidays(holidays, _referenceYear));
        DialogResult = DialogResult.OK;
        Close();
    }
}
