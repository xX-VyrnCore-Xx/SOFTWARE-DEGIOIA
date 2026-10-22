using System.Windows;
using System.Windows.Interop;
using MessageBox = System.Windows.MessageBox;

namespace PresenzeXmlConverter.Wpf;

/// <summary>
/// Le domande della conversione nella nuova interfaccia: per ora usa le stesse finestre di quella classica
/// (colonne, abbinamenti dei codici) come finestre modali sopra la finestra WPF, così nulla cambia nei dati.
/// </summary>
internal sealed class WpfPrompts : IConversionPrompts
{
    private readonly Window _owner;

    public WpfPrompts(Window owner) => _owner = owner;

    private System.Windows.Forms.IWin32Window Owner => WpfInterop.Owner(_owner);

    public ColumnSelection? AskColumns(IReadOnlyList<string> headers)
    {
        using var dialog = new ColumnMappingDialog(headers, null);
        return dialog.ShowDialog(Owner) == System.Windows.Forms.DialogResult.OK ? dialog.Selection : null;
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
        using var dialog = new MappingEditorDialog(
            database.GetEmployeeMappings(),
            database.GetPolicyMappings(),
            missingEmployees.ToArray(),
            missingPolicies.ToArray(),
            new Dictionary<string, string>(),
            database.GetEmployeeProfiles());
        var accepted = dialog.ShowDialog(Owner) == System.Windows.Forms.DialogResult.OK;
        if (accepted || dialog.Committed)
        {
            // Anche un salvataggio a metà resta: i codici inseriti non si perdono.
            foreach (var mapping in dialog.EmployeeMappings)
            {
                database.SaveEmployeeMapping(mapping.Key, mapping.Value);
            }
            foreach (var mapping in dialog.PolicyMappings)
            {
                database.SavePolicyMapping(mapping.Key, mapping.Value);
            }
            foreach (var profile in dialog.EmployeeProfiles)
            {
                database.SaveEmployeeProfile(profile.Key, profile.Value);
            }
        }
        return accepted;
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
