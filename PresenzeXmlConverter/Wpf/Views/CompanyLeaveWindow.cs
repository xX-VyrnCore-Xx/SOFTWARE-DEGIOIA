using System.Windows;
using System.Windows.Controls;

namespace PresenzeXmlConverter.Wpf;

using Window = System.Windows.Window;
using TextBox = System.Windows.Controls.TextBox;

/// <summary>
/// «Ferie e permessi» di un'azienda: quanto spetta in un anno di ogni giustificativo, una riga per giustificativo
/// («Ferie = 26 giorni», «Permessi = 104 ore»). Con le spettanze, le conversioni avvisano chi le supera e lo storico
/// mostra il saldo di ciascun dipendente.
/// </summary>
internal sealed class CompanyLeaveWindow
{
    private readonly TextBox _rules = new()
    {
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        Height = 140,
        VerticalContentAlignment = VerticalAlignment.Top,
        Padding = new Thickness(12, 8, 12, 8),
        Tag = "es.\nFerie = 26 giorni\nPermessi = 104 ore"
    };
    private IReadOnlyList<LeaveRule> _result = [];

    public CompanyLeaveWindow(CompanyRecord company)
    {
        var panel = new StackPanel();
        panel.Children.Add(Ui.Text("Quanto spetta a ogni dipendente in un anno. Scrivi il nome del giustificativo (o il suo codice) e la quantità in giorni o in ore, una riga per giustificativo.", 13.5, ink: "Brush.Muted", wrap: true).WithMargin(new Thickness(0, 0, 0, 8)));
        panel.Children.Add(DialogWindow.Field("Spettanze annue", _rules).WithMargin(new Thickness(0, 8, 0, 0)));
        panel.Children.Add(Ui.Text("I giorni si contano con la giornata standard dell'azienda. Le ore usate si leggono dalle esportazioni fatte con questa versione.", 12.5, ink: "Brush.Muted", wrap: true).WithMargin(new Thickness(0, 10, 0, 0)));

        Window = new DialogWindow($"Ferie e permessi · {company.Label}", "Ferie e permessi dell'azienda", panel, "Salva spettanze", width: 600);
        Window.Validate = () =>
        {
            if (!LeaveRule.TryParseLines(_rules.Text, out var rules, out var error))
            {
                return error;
            }
            _result = rules;
            return null;
        };
        _rules.Text = LeaveRule.ToLines(company.Leave);
    }

    public DialogWindow Window { get; }

    /// <summary>Le spettanze scelte (vuote = nessun saldo); valide solo se l'utente ha confermato.</summary>
    public IReadOnlyList<LeaveRule> Rules => _result;

    /// <summary>Il campo di testo (per i test).</summary>
    internal TextBox Field => _rules;

    public bool ShowOn(Window? owner) => Window.ShowOn(owner);
}
