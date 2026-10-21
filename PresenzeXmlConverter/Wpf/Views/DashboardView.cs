using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Orientation = System.Windows.Controls.Orientation;

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
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Brush = System.Windows.Media.Brush;
using Application = System.Windows.Application;

/// <summary>
/// Prima schermata: a sinistra la scheda di lavoro (scegli il foglio, i tre passi, le azioni), a destra lo stato
/// del sistema; sotto le scorciatoie verso le altre sezioni. Tutto sta in una colonna che scorre se la finestra è bassa.
/// </summary>
internal sealed class DashboardView : ScrollViewer
{
    public DashboardView(Action<int> openSection)
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Focusable = false;

        Zone = new DropZoneView();
        Stepper = new StepperBar { Margin = new Thickness(0, 22, 0, 0) };
        Zone.ShowEmpty();

        var choose = new Button { Content = "Scegli il foglio Excel", Style = Ui.Style("Primary"), MinWidth = 180 };
        choose.Click += (_, _) => Zone.RaiseClicked();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
        actions.Children.Add(choose);

        var work = new StackPanel();
        work.Children.Add(Ui.Text("Nuova conversione", 18, FontWeights.SemiBold));
        work.Children.Add(Ui.Text("Trascina qui il foglio presenze: il programma lo legge, lo controlla e prepara il file XML.", 13.5, ink: "Brush.Muted", wrap: true).WithMargin(new Thickness(0, 4, 0, 16)));
        work.Children.Add(Zone);
        work.Children.Add(Stepper);
        work.Children.Add(actions);

        Database = new StatusRow("Archivio", Glyph.Database);
        Updates = new StatusRow("Aggiornamenti", Glyph.Refresh);
        Security = new StatusRow("Protezione dati", Glyph.Shield);
        Database.SetState("In controllo…", UiTone.Neutral);
        Updates.SetState("In controllo…", UiTone.Neutral);
        Security.SetState("Dati solo su questo PC", UiTone.Good);

        var status = new StackPanel();
        status.Children.Add(Ui.Text("Stato", 18, FontWeights.SemiBold).WithMargin(new Thickness(0, 0, 0, 16)));
        status.Children.Add(Database);
        status.Children.Add(Updates.WithMargin(new Thickness(0, 14, 0, 0)));
        status.Children.Add(Security.WithMargin(new Thickness(0, 14, 0, 0)));

        var top = Ui.Columns(Ui.Star(1.7), Ui.Px(20), Ui.Star(1));
        top.Children.Add(Ui.Card(work, new Thickness(26)).At(0, 0));
        top.Children.Add(Ui.Card(status, new Thickness(26)).At(0, 2));

        var tiles = new UniformGrid { Columns = 4, Margin = new Thickness(0, 20, 0, 0) };
        var items = new (string Title, string Subtitle, Glyph Glyph, int Section)[]
        {
            ("Conversione", "Controlla i movimenti e scarica l'XML", Glyph.Transfer, 1),
            ("Aziende", "Regole e orari di ogni azienda", Glyph.Building, 2),
            ("Codici", "Dipendenti e codici paga", Glyph.Users, 3),
            ("Storico", "Le consegne già fatte", Glyph.Clock, 4)
        };
        foreach (var item in items)
        {
            var tile = new TileButton(item.Title, item.Subtitle, item.Glyph) { Margin = new Thickness(0, 0, item.Section == 4 ? 0 : 14, 0) };
            var section = item.Section;
            tile.Clicked += (_, _) => openSection(section);
            tiles.Children.Add(tile);
        }

        var page = new StackPanel { Margin = new Thickness(0, 0, 6, 24) };
        page.Children.Add(top);
        page.Children.Add(tiles);
        Content = new PageScaffold("Buongiorno", "Da qui importi le presenze, le controlli e scarichi il file per Zucchetti.", page);
    }

    public DropZoneView Zone { get; }

    public StepperBar Stepper { get; }

    public StatusRow Database { get; }

    public StatusRow Updates { get; }

    public StatusRow Security { get; }
}
