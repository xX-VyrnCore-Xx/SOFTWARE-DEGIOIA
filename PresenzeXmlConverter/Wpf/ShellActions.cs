using System.Diagnostics;

namespace PresenzeXmlConverter.Wpf;

/// <summary>Piccole azioni del sistema usate dalla nuova interfaccia (aprire un file, mostrarlo in Esplora risorse).</summary>
internal static class ShellActions
{
    /// <summary>Apre un file con il programma associato (il PDF nel lettore, il CSV in Excel).</summary>
    public static void OpenFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Nessun programma associato: il file resta dov'è.
        }
    }
}
