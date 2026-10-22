using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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

/// <summary>
/// Gli avvisi dentro la finestra: un cartellino per evento (conversione finita, file salvato, aggiornamento pronto…) in basso a
/// destra, che si ritira da solo dopo qualche secondo. Passando il mouse il conto alla rovescia si ferma; un clic chiude il cartellino
/// ed esegue l'azione collegata. Lo stesso testo già visibile viene solo rinnovato; al massimo quattro cartellini insieme.
/// </summary>
internal sealed class ToastLayer : Grid
{
    private const int MaxVisible = 4;

    private readonly StackPanel _stack = new()
    {
        VerticalAlignment = VerticalAlignment.Bottom,
        HorizontalAlignment = HorizontalAlignment.Right,
        Margin = new Thickness(0, 0, 24, 24),
        Width = 372
    };

    private readonly List<ToastCard> _cards = [];

    public ToastLayer()
    {
        IsHitTestVisible = true;
        Background = null;
        // Il livello lascia passare i clic che non toccano un cartellino.
        Children.Add(_stack);
    }

    /// <summary>I cartellini attualmente visibili (per i test).</summary>
    internal IReadOnlyList<(ToastKind Kind, string Title, string Message)> Visible =>
        _cards.Select(card => (card.Kind, card.Title, card.Message)).ToList();

    public void Show(ToastKind kind, string title, string message, Action? onClick = null)
    {
        var existing = _cards.FirstOrDefault(card => card.Title == title && card.Message == message);
        if (existing is not null)
        {
            existing.Renew();
            return;
        }
        var card = new ToastCard(kind, title, message, onClick);
        card.Closed += (_, _) => Remove(card);
        _cards.Add(card);
        while (_cards.Count > MaxVisible)
        {
            Remove(_cards[0]);
        }
        _stack.Children.Add(card.Visual);
        UiMotion.FadeIn(card.Visual, rise: 14);
        card.Start();
    }

    /// <summary>Chiude tutti i cartellini (per i test e alla chiusura della finestra).</summary>
    public void Clear()
    {
        foreach (var card in _cards.ToList())
        {
            Remove(card);
        }
    }

    private void Remove(ToastCard card)
    {
        card.Stop();
        _cards.Remove(card);
        _stack.Children.Remove(card.Visual);
    }

    private sealed class ToastCard
    {
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(80) };
        private readonly Action? _onClick;
        private readonly int _lifetime;
        private int _remaining;
        private bool _hover;

        public ToastCard(ToastKind kind, string title, string message, Action? onClick)
        {
            Kind = kind;
            Title = title;
            Message = message;
            _onClick = onClick;
            _lifetime = kind is ToastKind.Error or ToastKind.Warning ? 9000 : 5500;
            _remaining = _lifetime;

            var accent = Ui.Brush(kind switch
            {
                ToastKind.Success => "Brush.Success",
                ToastKind.Warning => "Brush.Warning",
                ToastKind.Error => "Brush.Danger",
                _ => "Brush.Brand"
            });
            var stripe = new Border { Width = 4, Background = accent, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 12, 0) };
            var texts = new StackPanel();
            texts.Children.Add(Ui.Text(title, 14, FontWeights.SemiBold, trimming: TextTrimming.CharacterEllipsis));
            texts.Children.Add(Ui.Text(message, 13, ink: "Brush.InkSecondary", wrap: true).WithMargin(new Thickness(0, 3, 0, 0)));
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(stripe.At(0, 0));
            row.Children.Add(texts.At(0, 1));
            Visual = Ui.Card(row, new Thickness(14, 12, 16, 12), radius: 12);
            Visual.Margin = new Thickness(0, 8, 0, 0);
            Visual.Cursor = onClick is null ? Cursors.Arrow : Cursors.Hand;
            Visual.MouseEnter += (_, _) => _hover = true;
            Visual.MouseLeave += (_, _) =>
            {
                _hover = false;
                _remaining = Math.Max(_remaining, 1800);
            };
            Visual.MouseLeftButtonUp += (_, _) =>
            {
                Closed?.Invoke(this, EventArgs.Empty);
                _onClick?.Invoke();
            };
            _timer.Tick += (_, _) =>
            {
                if (_hover)
                {
                    return;
                }
                _remaining -= (int)_timer.Interval.TotalMilliseconds;
                if (_remaining <= 0)
                {
                    Closed?.Invoke(this, EventArgs.Empty);
                }
            };
        }

        public ToastKind Kind { get; }

        public string Title { get; }

        public string Message { get; }

        public Border Visual { get; }

        public event EventHandler? Closed;

        public void Start() => _timer.Start();

        public void Stop() => _timer.Stop();

        public void Renew() => _remaining = _lifetime;
    }
}
