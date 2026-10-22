using System.Windows;
using System.Windows.Controls;

namespace PresenzeXmlConverter.Wpf;

using Window = System.Windows.Window;

/// <summary>
/// «Completa i codici mancanti» durante la conversione: la stessa tabella della sezione «Gestione codici» dentro una finestra.
/// I codici si salvano da soli (anche a metà), così chiudere con «Annulla» non butta via quelli già scritti.
/// </summary>
internal static class CodesDialog
{
    public static DialogWindow Create(ConversionService conversion, Func<Window?> owner, out CodesView view)
    {
        var codes = new CodesView(conversion, owner, inDialog: true) { Height = 400 };
        var window = new DialogWindow(
            "Codici dipendenti e giustificativi",
            "Completa i codici mancanti",
            codes,
            "Salva abbinamenti",
            width: 960,
            height: 680)
        {
            ResizeMode = ResizeMode.CanResize,
            MinWidth = 760,
            MinHeight = 520
        };
        window.Validate = () =>
        {
            codes.CommitAndCountMissing();
            return null;
        };
        window.Loaded += (_, _) => codes.OnShown();
        view = codes;
        return window;
    }
}
