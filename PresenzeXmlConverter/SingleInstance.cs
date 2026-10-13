namespace PresenzeXmlConverter;

/// <summary>
/// Un solo PresenzeFlow per utente. Un secondo avvio (doppio clic sul collegamento
/// mentre l'app è nel tray, per esempio) non apre una seconda finestra sullo stesso
/// database: avvisa la prima, che torna in primo piano, ed esce. Così non ci sono due
/// icone nel tray né due processi che scrivono sugli stessi file.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string DefaultMutexName = @"Local\DeGioia.PresenzeFlow.Instance";
    private const string DefaultSignalName = @"Local\DeGioia.PresenzeFlow.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _signal;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle signal)
    {
        _mutex = mutex;
        _signal = signal;
    }

    /// <summary>
    /// Prova a diventare l'istanza principale. Restituisce null se ce n'è già una:
    /// in quel caso ha già chiesto alla prima di mostrarsi. Se il sistema non permette
    /// di creare gli oggetti di sincronizzazione (caso raro) l'app parte lo stesso.
    /// </summary>
    public static SingleInstance? TryAcquire(
        string mutexName = DefaultMutexName,
        string signalName = DefaultSignalName)
    {
        try
        {
            var mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                SignalExisting(signalName);
                return null;
            }
            var signal = new EventWaitHandle(false, EventResetMode.AutoReset, signalName);
            return new SingleInstance(mutex, signal);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or
            IOException or
            WaitHandleCannotBeOpenedException or
            NotSupportedException)
        {
            return new SingleInstance(new Mutex(false), new EventWaitHandle(false, EventResetMode.AutoReset));
        }
    }

    /// <summary>Chiede alla prima istanza di mostrare la finestra.</summary>
    public static void SignalExisting(string signalName = DefaultSignalName)
    {
        try
        {
            using var signal = EventWaitHandle.OpenExisting(signalName);
            signal.Set();
        }
        catch (Exception exception) when (
            exception is WaitHandleCannotBeOpenedException or
            UnauthorizedAccessException or
            IOException or
            NotSupportedException)
        {
            // La prima istanza si sta chiudendo: niente da mostrare.
        }
    }

    /// <summary>
    /// Esegue <paramref name="onActivate"/> ogni volta che un altro avvio lo chiede
    /// (su un thread del pool: chi lo usa deve passare al thread dell'interfaccia).
    /// </summary>
    public void OnActivationRequested(Action onActivate)
    {
        _registration?.Unregister(null);
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _signal,
            (_, _) => onActivate(),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Mutex non posseduto da questo thread (ripiego senza sincronizzazione).
        }
        _mutex.Dispose();
        _signal.Dispose();
    }
}
