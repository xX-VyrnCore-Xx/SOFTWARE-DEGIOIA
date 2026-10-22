using System.Windows;
using System.Windows.Interop;
using MessageBox = System.Windows.MessageBox;

namespace PresenzeXmlConverter.Wpf;

/// <summary>
/// Le domande della conversione nella nuova interfaccia: colonne del foglio e codici mancanti sono finestre WPF
/// modali sopra la finestra principale; i dati si scrivono con gli stessi servizi della finestra classica.
/// </summary>
internal sealed class WpfPrompts : IConversionPrompts
{
    private readonly Window _owner;
    private readonly ConversionService _conversion;

    public WpfPrompts(Window owner, ConversionService conversion)
    {
        _owner = owner;
        _conversion = conversion;
    }

    public ColumnSelection? AskColumns(IReadOnlyList<string> headers)
    {
        return ColumnMappingWindow.Ask(_owner, headers);
    }

    public bool ConfirmCodeReplace(string employee, string fileCode, string savedCode) =>
        MessageBox.Show(
            _owner,
            $"Per “{employee}” il codice nel file Excel ({fileCode}) è diverso da quello salvato ({savedCode}). Aggiornare il mapping locale e usare il codice del file?",
            "Codice PAGHE modificato",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public bool CompleteMappings(IReadOnlyList<string> missingEmployees, IReadOnlyList<string> missingPolicies, LocalDatabase database)
    {
        var window = CodesDialog.Create(_conversion, () => _owner, out _);
        return window.ShowOn(_owner);
    }

    public bool ConfirmAlreadyDelivered(ExportRecord previous) =>
        MessageBox.Show(
            _owner,
            $"Questi stessi movimenti sono già stati esportati {ExportHistory.DescribeDay(previous.LocalTime, DateTime.Now)} " +
            $"nel file «{previous.FileName}».{Environment.NewLine}{Environment.NewLine}" +
            "Mandare due volte lo stesso file a PAGHE duplica le assenze. Vuoi salvarlo di nuovo?",
            "Consegna già esportata",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;

    public void ShowProblem(string title, string message) =>
        MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
}
