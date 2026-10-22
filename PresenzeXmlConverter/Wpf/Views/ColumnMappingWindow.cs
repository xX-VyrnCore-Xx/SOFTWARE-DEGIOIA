using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace PresenzeXmlConverter.Wpf;

using TextBlock = System.Windows.Controls.TextBlock;
using Window = System.Windows.Window;
using ComboBox = System.Windows.Controls.ComboBox;

/// <summary>
/// «Associa le colonne del file Excel» nella nuova interfaccia: cinque elenchi (nome, giustificativo, date, durata),
/// ognuno con una colonna diversa del foglio. Lo stesso controllo della finestra classica, fatto in <see cref="DialogLogic"/>.
/// </summary>
internal static class ColumnMappingWindow
{
    private static readonly string[] Labels = ["Nome dipendente", "Giustificativo (Policy)", "Data iniziale", "Data finale", "Durata (ore)"];

    /// <summary>Crea la finestra con le colonne già suggerite (se ce ne sono); gli elenchi restano in <paramref name="boxes"/>.</summary>
    public static DialogWindow Create(IReadOnlyList<string> headers, ColumnSelection? suggested, out IReadOnlyList<ComboBox> boxes, out Func<ColumnSelection?> result)
    {
        var suggestions = suggested is null
            ? [-1, -1, -1, -1, -1]
            : new[] { suggested.Employee, suggested.Policy, suggested.StartDate, suggested.EndDate, suggested.Duration };
        var panel = new StackPanel();
        var combos = new List<ComboBox>();
        for (var field = 0; field < Labels.Length; field++)
        {
            var combo = new ComboBox { IsEditable = false, MaxDropDownHeight = 260 };
            combo.Items.Add(new ComboBoxItem { Content = "Seleziona una colonna", Tag = -1 });
            for (var index = 0; index < headers.Count; index++)
            {
                combo.Items.Add(new ComboBoxItem { Content = DialogLogic.DescribeColumn(index, headers[index]), Tag = index });
            }
            var wanted = suggestions[field];
            combo.SelectedIndex = wanted >= 0 && wanted < headers.Count ? wanted + 1 : 0;
            System.Windows.Automation.AutomationProperties.SetName(combo, Labels[field]);
            combos.Add(combo);
            panel.Children.Add(DialogWindow.Field(Labels[field], combo));
        }
        panel.Children.Add(Ui.Text("Ogni campo richiede una colonna diversa del foglio Excel.", 13, ink: "Brush.Muted"));

        var window = new DialogWindow("Associa le colonne del file Excel", "Verifica le colonne da utilizzare per la conversione", panel, "Conferma colonne", width: 600);
        ColumnSelection? chosen = null;
        window.Validate = () =>
        {
            var indexes = combos.Select(combo => (combo.SelectedItem as ComboBoxItem)?.Tag is int index ? index : -1).ToArray();
            if (!DialogLogic.TryBuildColumns(indexes, out chosen, out var error))
            {
                return error;
            }
            return null;
        };
        boxes = combos;
        result = () => chosen;
        return window;
    }

    /// <summary>Apre la finestra e restituisce le colonne scelte (null = annullato).</summary>
    public static ColumnSelection? Ask(Window? owner, IReadOnlyList<string> headers, ColumnSelection? suggested = null)
    {
        var window = Create(headers, suggested, out _, out var result);
        return window.ShowOn(owner) ? result() : null;
    }
}
