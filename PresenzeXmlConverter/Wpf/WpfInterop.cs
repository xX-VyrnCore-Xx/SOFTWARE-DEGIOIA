using System.Windows;
using System.Windows.Interop;

namespace PresenzeXmlConverter.Wpf;

/// <summary>Ponte verso le finestre di Windows Forms ancora usate dalla nuova interfaccia (regole azienda, colonne, codici).</summary>
internal static class WpfInterop
{
    private sealed class Win32Owner(IntPtr handle) : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }

    public static System.Windows.Forms.IWin32Window Owner(Window window) => new Win32Owner(new WindowInteropHelper(window).Handle);
}

/// <summary>Una pagina che si aggiorna ogni volta che viene mostrata (i dati possono essere cambiati altrove).</summary>
internal interface IRefreshablePage
{
    void OnShown();
}
