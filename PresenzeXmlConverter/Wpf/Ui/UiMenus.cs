using System.Windows;
using System.Windows.Controls;

namespace PresenzeXmlConverter.Wpf;

using Brush = System.Windows.Media.Brush;
using Clipboard = System.Windows.Clipboard;

/// <summary>Una voce di un menù a comparsa: testo, icona, azione e se è attiva; oppure un separatore.</summary>
internal sealed record MenuEntry(string? Text, Glyph? Glyph, Action? Click, bool Enabled = true, bool Danger = false)
{
    public bool IsSeparator => Text is null;

    public static MenuEntry Separator { get; } = new(null, null, null);
}

/// <summary>
/// I menù a comparsa (tasto destro su tabelle e calendario) nello stile dell'app: angoli tondi, ombra leggera, icone dello stesso
/// disegno della barra laterale e voce evidenziata al passaggio. Costruiti da un elenco di <see cref="MenuEntry"/>, così le voci e
/// la loro disponibilità si verificano con un test senza aprire il menù.
/// </summary>
internal static class UiMenus
{
    public static ContextMenu Create(IEnumerable<MenuEntry> entries)
    {
        var menu = new ContextMenu { HasDropShadow = true, Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint };
        Fill(menu, entries);
        return menu;
    }

    /// <summary>Riempie un menù (di nuovo a ogni apertura, perché le voci dipendono da ciò che è selezionato).</summary>
    public static void Fill(ContextMenu menu, IEnumerable<MenuEntry> entries)
    {
        menu.Items.Clear();
        var previousSeparator = true;
        foreach (var entry in entries)
        {
            if (entry.IsSeparator)
            {
                // Mai un separatore in testa né due di fila.
                if (!previousSeparator)
                {
                    menu.Items.Add(new Separator());
                    previousSeparator = true;
                }
                continue;
            }
            var item = new MenuItem
            {
                Header = entry.Text,
                IsEnabled = entry.Enabled
            };
            if (entry.Glyph is { } glyph)
            {
                item.Icon = Icons.Create(glyph, Ui.Brush(entry.Danger ? "Brush.DangerInk" : "Brush.InkSecondary"), 16, 1.6);
            }
            if (entry.Danger)
            {
                item.Foreground = Ui.Brush("Brush.DangerInk");
            }
            var click = entry.Click;
            item.Click += (_, _) => click?.Invoke();
            menu.Items.Add(item);
            previousSeparator = false;
        }
        if (menu.Items.Count > 0 && menu.Items[^1] is Separator)
        {
            menu.Items.RemoveAt(menu.Items.Count - 1);
        }
    }

    /// <summary>Collega a un elemento un menù che si ricostruisce a ogni apertura (senza voci non si apre).</summary>
    public static void Attach(FrameworkElement element, Func<IEnumerable<MenuEntry>> entries)
    {
        var menu = new ContextMenu { HasDropShadow = true };
        element.ContextMenu = menu;
        menu.Opened += (_, _) => Fill(menu, entries());
        element.ContextMenuOpening += (_, args) =>
        {
            Fill(menu, entries());
            if (menu.Items.Count == 0)
            {
                args.Handled = true;
            }
        };
    }

    /// <summary>Con il tasto destro la riga sotto il mouse diventa quella selezionata (le tabelle di WPF non lo fanno da sole).</summary>
    public static void SelectRowOnRightClick(DataGrid grid) =>
        grid.PreviewMouseRightButtonDown += (_, args) =>
        {
            var source = args.OriginalSource as DependencyObject;
            while (source is not null && source is not DataGridRow)
            {
                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }
            if (source is DataGridRow row)
            {
                row.IsSelected = true;
                row.Focus();
            }
        };

    /// <summary>Il testo delle voci (i separatori come «—») per i test.</summary>
    internal static IReadOnlyList<string> Describe(ContextMenu menu) =>
        menu.Items.Cast<object>().Select(item => item switch
        {
            MenuItem menuItem => (menuItem.IsEnabled ? string.Empty : "~") + menuItem.Header,
            _ => "—"
        }).ToArray();

    /// <summary>Copia un testo negli appunti senza mai lanciare (gli appunti possono essere occupati da un altro programma).</summary>
    public static bool Copy(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }
}
