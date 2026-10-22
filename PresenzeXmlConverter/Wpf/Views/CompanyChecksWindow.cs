using System.Windows;
using System.Windows.Controls;

namespace PresenzeXmlConverter.Wpf;

using Window = System.Windows.Window;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;

/// <summary>
/// «Controlli» di un'azienda nella nuova interfaccia: le soglie con cui il foglio viene controllato (malattie e assenze
/// lunghe, variazione rispetto all'ultima consegna, tetto di ore al mese, dipendenti attesi). Valgono solo per
/// l'azienda scelta; le caselle ripropongono le soglie di serie.
/// </summary>
internal sealed class CompanyChecksWindow
{
    private readonly TextBox _illness = new() { Tag = "es. 30", MaxLength = 3 };
    private readonly TextBox _absence = new() { Tag = "vuoto = nessun controllo", MaxLength = 3 };
    private readonly TextBox _percent = new() { Tag = "es. 30", MaxLength = 3 };
    private readonly TextBox _maxHours = new() { Tag = "vuoto = nessun tetto", MaxLength = 6 };
    private readonly CheckBox _overlaps = new() { Content = "Segnala le richieste che si sovrappongono", Style = Ui.Style("Toggle") };
    private readonly TextBox _expected = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 96, VerticalContentAlignment = VerticalAlignment.Top, Padding = new Thickness(12, 8, 12, 8) };
    private ValidationRules? _rules;

    public CompanyChecksWindow(CompanyRecord company)
    {
        var panel = new StackPanel();
        panel.Children.Add(Ui.Text("Soglie dei controlli per questa azienda. Le segnalazioni compaiono nella scheda Controlli della conversione.", 13.5, ink: "Brush.Muted", wrap: true).WithMargin(new Thickness(0, 0, 0, 8)));
        panel.Children.Add(DialogWindow.Field("Malattia di fila oltre (giorni)", _illness).WithMargin(new Thickness(0, 8, 0, 0)));
        panel.Children.Add(DialogWindow.Field("Altre assenze di fila oltre (giorni)", _absence).WithMargin(new Thickness(0, 10, 0, 0)));
        panel.Children.Add(DialogWindow.Field("Variazione rispetto all'ultima consegna oltre (%)", _percent).WithMargin(new Thickness(0, 10, 0, 0)));
        panel.Children.Add(DialogWindow.Field("Tetto di ore al mese per dipendente", _maxHours).WithMargin(new Thickness(0, 10, 0, 10)));
        panel.Children.Add(_overlaps);
        panel.Children.Add(DialogWindow.Field("Dipendenti attesi nel foglio (uno per riga): se mancano, avviso", _expected).WithMargin(new Thickness(0, 10, 0, 0)));

        Window = new DialogWindow($"Controlli · {company.Label}", "Controlli dell'azienda", panel, "Salva controlli", width: 600);
        Window.Validate = () =>
        {
            if (!DialogLogic.TryBuildValidationRules(_illness.Text, _absence.Text, _percent.Text, _overlaps.IsChecked == true, _maxHours.Text, _expected.Text, out var rules, out var error))
            {
                return error;
            }
            _rules = rules;
            return null;
        };
        Fill(company.Checks ?? ValidationRules.Default);
    }

    public DialogWindow Window { get; }

    /// <summary>Le soglie scelte; valide solo se l'utente ha confermato.</summary>
    public ValidationRules Rules => _rules ?? ValidationRules.Default;

    /// <summary>I campi (per i test).</summary>
    internal (TextBox Illness, TextBox Absence, TextBox Percent, TextBox MaxHours, CheckBox Overlaps, TextBox Expected) Controls =>
        (_illness, _absence, _percent, _maxHours, _overlaps, _expected);

    private void Fill(ValidationRules rules)
    {
        var (illness, absence, percent, maxHours, expected) = DialogLogic.FormatValidationRules(rules);
        _illness.Text = illness;
        _absence.Text = absence;
        _percent.Text = percent;
        _maxHours.Text = maxHours;
        _overlaps.IsChecked = rules.CheckOverlaps;
        _expected.Text = expected;
    }

    /// <summary>Apre la finestra: false = annullato; true = confermato (le soglie sono in <see cref="Rules"/>).</summary>
    public bool ShowOn(Window? owner) => Window.ShowOn(owner);
}
