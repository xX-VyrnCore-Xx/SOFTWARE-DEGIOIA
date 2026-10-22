using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PresenzeXmlConverter.Wpf;

using TextBlock = System.Windows.Controls.TextBlock;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Button = System.Windows.Controls.Button;
using Window = System.Windows.Window;
using Orientation = System.Windows.Controls.Orientation;
using Application = System.Windows.Application;

/// <summary>
/// Una finestra di dialogo nello stile della nuova interfaccia: titolo, contenuto, messaggio d'errore e due pulsanti
/// («conferma» e «Annulla»). La conferma passa da <see cref="Validate"/>: se rifiuta, la finestra resta aperta con
/// il motivo scritto sotto il contenuto.
/// </summary>
internal sealed class DialogWindow : Window
{
    private readonly TextBlock _error = Ui.Text(string.Empty, 13, ink: "Brush.DangerInk", wrap: true);
    private readonly Button _confirm = new();
    private readonly Button _cancel = new() { Content = "Annulla" };
    private readonly StackPanel _buttons;
    private readonly List<Button> _choices = [];

    /// <summary>I pulsanti di scelta aggiunti con <see cref="AddChoice"/> (per i test).</summary>
    internal IReadOnlyList<Button> AddedChoices => _choices;

    public DialogWindow(string title, string heading, UIElement content, string confirmText, double width = 620, double height = double.NaN)
    {
        Title = title;
        Width = width;
        if (!double.IsNaN(height))
        {
            Height = height;
        }
        else
        {
            SizeToContent = SizeToContent.Height;
        }
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        FontFamily = Ui.Font;
        Background = Ui.Brush("Brush.Canvas");
        Foreground = Ui.Brush("Brush.Ink");
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        _confirm.Content = confirmText;
        _confirm.Style = Ui.Style("Primary");
        _confirm.IsDefault = true;
        _cancel.IsCancel = true;
        _confirm.Click += (_, _) => Confirm();
        _cancel.Click += (_, _) => Close(false);

        _buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        _cancel.Margin = new Thickness(0, 0, 8, 0);
        _buttons.Children.Add(_cancel);
        _buttons.Children.Add(_confirm);

        var root = new StackPanel { Margin = new Thickness(28, 24, 28, 22) };
        root.Children.Add(Ui.Text(heading, 20, FontWeights.SemiBold, wrap: true));
        root.Children.Add(content is FrameworkElement element ? element.WithMargin(new Thickness(0, 14, 0, 0)) : content);
        root.Children.Add(_error.WithMargin(new Thickness(0, 10, 0, 0)));
        root.Children.Add(_buttons);
        Content = root;
    }

    /// <summary>Il testo del pulsante che chiude senza confermare («Annulla» di norma).</summary>
    public string CancelText
    {
        get => _cancel.Content?.ToString() ?? string.Empty;
        set => _cancel.Content = value;
    }

    /// <summary>Nasconde «Annulla» (finestre che hanno solo «Chiudi»).</summary>
    public void HideCancel() => _cancel.Visibility = Visibility.Collapsed;

    /// <summary>Controlla le scelte: null = si può confermare, altrimenti il motivo da mostrare.</summary>
    public Func<string?>? Validate { get; set; }

    /// <summary>True se l'utente ha confermato.</summary>
    public bool Accepted { get; private set; }

    /// <summary>Il testo dell'errore mostrato sotto il contenuto (per i test).</summary>
    public string ErrorText => _error.Text;

    internal Button ConfirmButton => _confirm;

    /// <summary>Un terzo pulsante accanto ad «Annulla» (una scelta in più): chiude la finestra confermando, dopo aver eseguito l'azione.</summary>
    public Button AddChoice(string text, Action chosen)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 0, 8, 0) };
        _choices.Add(button);
        button.Click += (_, _) =>
        {
            chosen();
            Close(true);
        };
        _buttons.Children.Insert(_buttons.Children.IndexOf(_confirm), button);
        return button;
    }

    /// <summary>Conferma come se si fosse premuto il pulsante (per i test e le scorciatoie).</summary>
    public void Confirm()
    {
        var problem = Validate?.Invoke();
        if (problem is not null)
        {
            _error.Text = problem;
            return;
        }
        Close(true);
    }

    private void Close(bool accepted)
    {
        Accepted = accepted;
        try
        {
            DialogResult = accepted;
        }
        catch (InvalidOperationException)
        {
            // La finestra non è stata aperta con ShowDialog (nei test): basta chiuderla.
            Close();
        }
    }

    /// <summary>Apre la finestra sopra <paramref name="owner"/> e dice se l'utente ha confermato.</summary>
    public bool ShowOn(Window? owner)
    {
        if (owner is { IsLoaded: true })
        {
            Owner = owner;
        }
        return ShowDialog() == true && Accepted;
    }

    /// <summary>Un campo del dialogo: etichetta sopra, controllo sotto.</summary>
    public static StackPanel Field(string label, UIElement control)
    {
        var field = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        field.Children.Add(Ui.Text(label, 13.5, FontWeights.SemiBold, "Brush.InkSecondary", wrap: true).WithMargin(new Thickness(0, 0, 0, 5)));
        field.Children.Add(control);
        return field;
    }
}
