using System.Windows;
using System.Windows.Controls;

namespace PresenzeXmlConverter.Wpf;

using Window = System.Windows.Window;
using CheckBox = System.Windows.Controls.CheckBox;

/// <summary>«Come vuoi chiudere il programma?»: restare in background nel tray oppure uscire; la scelta si può ricordare.</summary>
internal static class CloseChoiceWindow
{
    public static DialogWindow Create(out CheckBox remember, out Func<CloseAction?> result)
    {
        var rememberBox = new CheckBox { Content = "Ricorda questa scelta (non chiedere più)", Style = Ui.Style("Toggle") };
        var panel = new StackPanel();
        panel.Children.Add(Ui.Text(
            "Se resti in background ricevi una notifica quando una conversione termina, senza tenere la finestra aperta.",
            13.5, ink: "Brush.Muted", wrap: true));
        panel.Children.Add(rememberBox.WithMargin(new Thickness(0, 12, 0, 0)));

        CloseAction? chosen = null;
        var window = new DialogWindow("Chiudi Vyrn Presence", "Come vuoi chiudere il programma?", panel, "Resta in background", width: 560);
        window.Validate = () =>
        {
            chosen = CloseAction.MinimizeToTray;
            return null;
        };
        window.AddChoice("Esci dal programma", () => chosen = CloseAction.Exit);
        remember = rememberBox;
        result = () => window.Accepted ? chosen : null;
        return window;
    }

    /// <summary>Chiede come chiudere: null = l'utente ha annullato e la finestra resta aperta.</summary>
    public static CloseAction? Ask(Window? owner, out bool remember)
    {
        var window = Create(out var box, out var result);
        var accepted = window.ShowOn(owner);
        remember = box.IsChecked == true;
        return accepted ? result() : null;
    }
}
