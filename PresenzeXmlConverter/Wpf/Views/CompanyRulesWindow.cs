using System.Windows;
using System.Windows.Controls;

namespace PresenzeXmlConverter.Wpf;

using TextBlock = System.Windows.Controls.TextBlock;
using Window = System.Windows.Window;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;

/// <summary>
/// «Regole di conversione» di un'azienda nella nuova interfaccia. Con «regole proprie» scegliere l'azienda in conversione
/// applica da solo giornata, sabato, rilevamento automatico e chiusure; senza, valgono quelle generali delle Impostazioni.
/// </summary>
internal sealed class CompanyRulesWindow
{
    private readonly CheckBox _own = new() { Content = "Usa regole proprie per questa azienda", Style = Ui.Style("Toggle") };
    private readonly TextBox _hours = new() { Tag = "es. 8  oppure  7,5", MaxLength = 5 };
    private readonly CheckBox _saturdays = new() { Content = "Il sabato è sempre lavorativo", Style = Ui.Style("Toggle") };
    private readonly CheckBox _autoDetect = new() { Content = "Rileva in automatico sabati e ore dalle date (consigliato)", Style = Ui.Style("Toggle") };
    private readonly TextBox _holidays = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 96, VerticalContentAlignment = VerticalAlignment.Top, Padding = new Thickness(12, 8, 12, 8) };
    private readonly GenerationOptions _general;
    private readonly int _referenceYear;
    private CompanyRules? _rules;

    public CompanyRulesWindow(CompanyRecord company, GenerationOptions generalOptions, int referenceYear)
    {
        _general = generalOptions;
        _referenceYear = referenceYear;
        var panel = new StackPanel();
        panel.Children.Add(Ui.Text("Come si calcolano le ore per questa azienda. Senza regole proprie valgono quelle generali delle Impostazioni.", 13.5, ink: "Brush.Muted", wrap: true).WithMargin(new Thickness(0, 0, 0, 8)));
        panel.Children.Add(_own);
        panel.Children.Add(DialogWindow.Field("Ore di una giornata standard", _hours).WithMargin(new Thickness(0, 10, 0, 10)));
        panel.Children.Add(_saturdays);
        panel.Children.Add(_autoDetect);
        panel.Children.Add(DialogWindow.Field("Chiusure aziendali (una data per riga o separate da virgola, es. 02/06, 24/12)", _holidays).WithMargin(new Thickness(0, 10, 0, 0)));

        Window = new DialogWindow($"Regole di conversione · {company.Label}", "Regole di conversione", panel, "Salva regole", width: 600);
        Window.Validate = () =>
        {
            if (!DialogLogic.TryBuildCompanyRules(_own.IsChecked == true, _hours.Text, _saturdays.IsChecked == true, _autoDetect.IsChecked == true, _holidays.Text, _referenceYear, out _rules, out var error))
            {
                return error;
            }
            return null;
        };
        _own.Checked += (_, _) => UpdateEnabled();
        _own.Unchecked += (_, _) => UpdateEnabled();
        _own.IsChecked = company.Rules is not null;
        Fill(company.Rules ?? CompanyRules.From(generalOptions, referenceYear));
        UpdateEnabled();
    }

    public DialogWindow Window { get; }

    /// <summary>Le regole scelte (null = quelle generali). Valide solo se l'utente ha confermato.</summary>
    public CompanyRules? Rules => _rules;

    /// <summary>Le scelte attuali nei campi (per i test).</summary>
    internal (bool Own, TextBox Hours, TextBox Holidays, CheckBox Saturdays, CheckBox AutoDetect, CheckBox OwnBox) Controls =>
        (_own.IsChecked == true, _hours, _holidays, _saturdays, _autoDetect, _own);

    private void Fill(CompanyRules rules)
    {
        var (hours, holidays) = DialogLogic.FormatRules(rules);
        _hours.Text = hours;
        _saturdays.IsChecked = rules.IncludeSaturdays;
        _autoDetect.IsChecked = rules.AutoDetect;
        _holidays.Text = holidays;
    }

    private void UpdateEnabled()
    {
        var on = _own.IsChecked == true;
        _hours.IsEnabled = on;
        _saturdays.IsEnabled = on;
        _autoDetect.IsEnabled = on;
        _holidays.IsEnabled = on;
        if (!on)
        {
            // Senza regole proprie si vedono quelle generali (sola lettura).
            Fill(CompanyRules.From(_general, _referenceYear));
        }
    }

    /// <summary>Apre la finestra: false = annullato; true = confermato (le regole sono in <see cref="Rules"/>).</summary>
    public bool ShowOn(Window? owner) => Window.ShowOn(owner);
}
