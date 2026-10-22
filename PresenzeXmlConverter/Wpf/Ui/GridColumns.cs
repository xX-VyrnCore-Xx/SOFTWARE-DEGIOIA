using System.Windows;
using System.Windows.Controls;
using Application = System.Windows.Application;
using TextBlock = System.Windows.Controls.TextBlock;
using DataGrid = System.Windows.Controls.DataGrid;

namespace PresenzeXmlConverter.Wpf;

/// <summary>
/// Rifiniture comuni delle tabelle: il testo che non sta in una cella finisce con i puntini (mai tagliato di netto) e le colonne
/// di cifre hanno l'intestazione allineata a destra come i valori.
/// </summary>
internal static class GridColumns
{
    private static bool _registered;
    private static Style? _trimmed;

    /// <summary>Il testo normale di una cella, con i puntini quando è troppo lungo.</summary>
    public static Style Trimmed => _trimmed ??= Build();

    // TextBlock non espone il pennello della proprietà: i puntini si impostano quando la cella compare.
    private static readonly EventSetter Ellipsis = new(
        FrameworkElement.LoadedEvent,
        new RoutedEventHandler((sender, _) =>
        {
            if (sender is TextBlock block)
            {
                block.TextTrimming = TextTrimming.CharacterEllipsis;
            }
        }));

    private static Style Build()
    {
        var style = new Style(typeof(TextBlock), DataGridTextColumn.DefaultElementStyle);
        style.Setters.Add(Ellipsis);
        style.Seal();
        return style;
    }

    /// <summary>Una colonna di cifre: valori e intestazione a destra, puntini se non sta.</summary>
    public static DataGridTextColumn RightAligned(this DataGridTextColumn column)
    {
        var text = new Style(typeof(TextBlock), DataGridTextColumn.DefaultElementStyle);
        text.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        text.Setters.Add(Ellipsis);
        column.ElementStyle = text;
        var header = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        if (Application.Current?.TryFindResource(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)) is Style basic)
        {
            header.BasedOn = basic;
        }
        header.Setters.Add(new Setter(System.Windows.Controls.Control.HorizontalContentAlignmentProperty, System.Windows.HorizontalAlignment.Right));
        column.HeaderStyle = header;
        return column;
    }

    /// <summary>Dà i puntini a tutte le colonne di testo di una tabella che non hanno già uno stile proprio.</summary>
    public static void Polish(DataGrid grid)
    {
        foreach (var column in grid.Columns.OfType<DataGridTextColumn>())
        {
            if (column.ReadLocalValue(DataGridTextColumn.ElementStyleProperty) == DependencyProperty.UnsetValue)
            {
                column.ElementStyle = Trimmed;
            }
        }
    }

    /// <summary>Applica <see cref="Polish"/> a ogni tabella dell'applicazione quando compare (una volta sola per processo).</summary>
    public static void RegisterForAllGrids()
    {
        if (_registered)
        {
            return;
        }
        _registered = true;
        EventManager.RegisterClassHandler(
            typeof(DataGrid),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is DataGrid grid)
                {
                    Polish(grid);
                }
            }));
    }
}
