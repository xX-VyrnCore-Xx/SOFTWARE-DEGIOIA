using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PresenzeXmlConverter.Wpf;

using TextBlock = System.Windows.Controls.TextBlock;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Cursors = System.Windows.Input.Cursors;
using DragDropEffects = System.Windows.DragDropEffects;
using Button = System.Windows.Controls.Button;
using Control = System.Windows.Controls.Control;
using Label = System.Windows.Controls.Label;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using Window = System.Windows.Window;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Orientation = System.Windows.Controls.Orientation;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Brush = System.Windows.Media.Brush;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using DataGrid = System.Windows.Controls.DataGrid;
using Binding = System.Windows.Data.Binding;
using ScrollViewer = System.Windows.Controls.ScrollViewer;

/// <summary>«Nuova versione disponibile» e «Vyrn Presence aggiornato»: le note della Release impaginate (titoli, elenchi, paragrafi).</summary>
internal static class ReleaseNotesWindow
{
    public static DialogWindow Create(string heading, string subtitle, string? notes, bool showUpdateButton)
    {
        var panel = new StackPanel();
        panel.Children.Add(Ui.Text(subtitle, 13.5, ink: "Brush.Muted", wrap: true).WithMargin(new Thickness(0, 0, 0, 10)));
        var blocks = new StackPanel { Margin = new Thickness(2, 0, 8, 0) };
        foreach (var block in ReleaseNotesDocument.Parse(notes))
        {
            blocks.Children.Add(block.Kind switch
            {
                NoteBlockKind.Heading => Ui.Text(block.Text, 15, FontWeights.SemiBold, wrap: true).WithMargin(new Thickness(0, 12, 0, 4)),
                NoteBlockKind.Bullet => Ui.Text("•  " + block.Text, 13.5, ink: "Brush.InkSecondary", wrap: true).WithMargin(new Thickness(8, 2, 0, 2)),
                _ => Ui.Text(block.Text, 13.5, ink: "Brush.InkSecondary", wrap: true).WithMargin(new Thickness(0, 4, 0, 4))
            });
        }
        if (blocks.Children.Count == 0)
        {
            blocks.Children.Add(Ui.Text("Nessuna nota di rilascio disponibile.", 13.5, ink: "Brush.Muted"));
        }
        panel.Children.Add(new ScrollViewer
        {
            MaxHeight = 360,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = blocks
        });
        var window = new DialogWindow(heading, heading, panel, showUpdateButton ? "Aggiorna ora" : "Chiudi", width: 640);
        if (showUpdateButton)
        {
            window.CancelText = "Più tardi";
        }
        else
        {
            window.HideCancel();
        }
        return window;
    }
}

/// <summary>
/// L'avanzamento di un aggiornamento: i quattro passi (scaricamento, controllo del file, verifica della firma, installazione),
/// la barra e i dettagli del download, e «Annulla» fino all'installazione.
/// </summary>
internal sealed class UpdateProgressWindow : Window, IUpdateInstallUi
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly List<(Border Dot, TextBlock Label)> _steps = [];
    private readonly Border _barFill = new() { HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(4), Height = 8 };
    private readonly Border _bar = new() { Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 14, 0, 0) };
    private readonly TextBlock _detail = Ui.Text("Connessione a GitHub…", 13, ink: "Brush.Muted", wrap: true);
    private readonly Button _cancel = new() { Content = "Annulla", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
    private readonly Func<string, string, string, bool>? _confirmOtherSigner;
    private UpdateStep _current = UpdateStep.Download;
    private int _percent;

    public UpdateProgressWindow(string version, Func<string, string, string, bool>? confirmOtherSigner = null)
    {
        _confirmOtherSigner = confirmOtherSigner;
        Title = "Aggiornamento di Vyrn Presence";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        FontFamily = Ui.Font;
        Background = Ui.Brush("Brush.Canvas");
        Foreground = Ui.Brush("Brush.Ink");
        UseLayoutRounding = true;

        var root = new StackPanel { Margin = new Thickness(28, 24, 28, 22) };
        root.Children.Add(Ui.Text($"Aggiornamento alla {version}", 20, FontWeights.SemiBold, wrap: true));
        var list = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        foreach (var step in UpdateSteps.All)
        {
            var dot = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            var label = Ui.Text(UpdateSteps.Label(step), 14);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            row.Children.Add(dot);
            row.Children.Add(label);
            list.Children.Add(row);
            _steps.Add((dot, label));
        }
        root.Children.Add(list);
        _bar.Background = Ui.Brush("Brush.Sunken");
        _bar.Child = _barFill;
        _barFill.Background = Ui.Brush("Brush.Brand");
        _bar.SizeChanged += (_, _) => ApplyBar();
        root.Children.Add(_bar);
        root.Children.Add(_detail.WithMargin(new Thickness(0, 10, 0, 0)));
        _cancel.Click += (_, _) =>
        {
            _cancel.IsEnabled = false;
            SetDetail("Annullamento in corso…");
            _cancellation.Cancel();
        };
        root.Children.Add(_cancel);
        Content = root;
        Paint();
        Closed += (_, _) => _cancellation.Dispose();
    }

    public CancellationToken Token => _cancellation.Token;

    /// <summary>Il passo mostrato e la percentuale (per i test).</summary>
    internal (UpdateStep Step, int Percent, string Detail, bool CanCancel) State => (_current, _percent, _detail.Text, _cancel.IsEnabled);

    public void SetStep(UpdateStep step)
    {
        _current = step;
        if (step >= UpdateStep.Install)
        {
            _cancel.IsEnabled = false;
        }
        if (step > UpdateStep.Download)
        {
            _percent = 100;
            ApplyBar();
        }
        _detail.Text = step switch
        {
            UpdateStep.Checksum => "Controllo che il file sia identico a quello pubblicato…",
            UpdateStep.Signature => "Verifica della firma digitale con Windows…",
            UpdateStep.Install => "Il programma si chiude e si riapre da solo…",
            _ => _detail.Text
        };
        Paint();
    }

    public void SetTransfer(TransferMeter meter)
    {
        _percent = meter.Percent;
        ApplyBar();
        _detail.Text = $"{meter.Percent}% · {meter.Describe()}";
    }

    public void SetDetail(string text) => _detail.Text = text;

    public bool ConfirmOtherSigner(string message, string currentSigner, string installerSigner) =>
        _confirmOtherSigner?.Invoke(message, currentSigner, installerSigner) ?? false;

    private void ApplyBar() => _barFill.Width = Math.Max(0, _bar.ActualWidth * _percent / 100.0);

    private void Paint()
    {
        for (var index = 0; index < _steps.Count; index++)
        {
            var state = UpdateSteps.StateOf(UpdateSteps.All[index], _current);
            _steps[index].Dot.Background = Ui.Brush(state switch
            {
                StepState.Done => "Brush.Success",
                StepState.Active => "Brush.Brand",
                StepState.Failed => "Brush.Danger",
                _ => "Brush.DividerStrong"
            });
            _steps[index].Label.Foreground = Ui.Brush(state == StepState.Pending ? "Brush.Muted" : "Brush.Ink");
        }
    }
}
