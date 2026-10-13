using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace PresenzeXmlConverter;

/// <summary>
/// Misura il tempo delle fasi di avvio e lo scrive in un file, quando richiesto.
/// Serve a rispondere con un dato alla domanda "perché ci mette tanto?": senza
/// numeri la velocità di apertura è solo un'impressione. La misura è spenta per
/// impostazione predefinita — si attiva con la variabile d'ambiente
/// <c>PRESENZEFLOW_TRACE_STARTUP=1</c> — così non pesa sull'avvio normale.
/// </summary>
internal sealed class StartupTrace
{
    private readonly List<(string Phase, double Milliseconds)> _phases = new();
    private readonly Stopwatch _total = Stopwatch.StartNew();

    public const string EnvironmentVariable = "PRESENZEFLOW_TRACE_STARTUP";

    /// <summary>La misura è richiesta dall'ambiente? (Attiva solo con "1"/"true".)</summary>
    public static bool IsEnabled => Environment.GetEnvironmentVariable(EnvironmentVariable) is "1" or "true" or "TRUE";

    /// <summary>Esegue un'azione e ne registra la durata. Restituisce il valore prodotto.</summary>
    public T Measure<T>(string phase, Func<T> action)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            return action();
        }
        finally
        {
            Add(phase, start);
        }
    }

    /// <summary>Esegue un'azione e ne registra la durata.</summary>
    public void Measure(string phase, Action action) =>
        Measure(phase, () =>
        {
            action();
            return true;
        });

    /// <summary>Registra una fase già conclusa.</summary>
    public void Add(string phase, long startTimestamp) =>
        _phases.Add((
            phase,
            (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency));

    public IReadOnlyList<(string Phase, double Milliseconds)> Phases => _phases;

    public double TotalMilliseconds => _total.Elapsed.TotalMilliseconds;

    /// <summary>
    /// Riepilogo leggibile: una riga per fase, in ordine di durata decrescente,
    /// con il totale. La fase più lenta viene per prima perché è quella da
    /// guardare per prima.
    /// </summary>
    public string Describe()
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            $"Avvio {DateTime.Now:yyyy-MM-dd HH:mm:ss} — totale {Format(TotalMilliseconds)}");
        foreach (var (phase, milliseconds) in _phases.OrderByDescending(item => item.Milliseconds))
        {
            builder.AppendLine($"{Format(milliseconds),10}  {phase}");
        }
        return builder.ToString().TrimEnd();
    }

    /// <summary>Scrive il riepilogo in coda al file indicato, senza mai far fallire l'avvio.</summary>
    public void WriteTo(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, Describe() + Environment.NewLine + Environment.NewLine);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // La diagnosi non deve mai impedire l'avvio dell'applicazione.
        }
    }

    private static string Format(double milliseconds) =>
        milliseconds.ToString("0.0", CultureInfo.InvariantCulture) + " ms";
}
