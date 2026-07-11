namespace DynamicIslandBar;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string DefaultInstanceName = "DynamicIslandBar-8D0C8F9E";
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listenerTask;
    private bool _disposed;

    private SingleInstanceCoordinator(Mutex mutex, EventWaitHandle activationEvent)
    {
        _mutex = mutex;
        _activationEvent = activationEvent;
    }

    public static bool TryAcquire(out SingleInstanceCoordinator? coordinator)
    {
        return TryAcquire(DefaultInstanceName, out coordinator);
    }

    internal static bool TryAcquire(
        string instanceName,
        out SingleInstanceCoordinator? coordinator)
    {
        var mutex = new Mutex(
            initiallyOwned: true,
            name: $@"Local\{instanceName}-Mutex",
            createdNew: out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            coordinator = null;
            return false;
        }

        var activationEvent = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: $@"Local\{instanceName}-Activate");
        coordinator = new SingleInstanceCoordinator(mutex, activationEvent);
        return true;
    }

    public static void SignalExistingInstance()
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                using var activationEvent = EventWaitHandle.OpenExisting(
                    $@"Local\{DefaultInstanceName}-Activate");
                activationEvent.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException) when (attempt < 3)
            {
                Thread.Sleep(75);
            }
            catch
            {
                return;
            }
        }
    }

    public void StartListening(Action activationRequested)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_listenerTask != null)
        {
            return;
        }

        _listenerTask = Task.Run(() =>
        {
            var handles = new WaitHandle[] { _activationEvent, _cancellation.Token.WaitHandle };
            while (!_cancellation.IsCancellationRequested)
            {
                var signaled = WaitHandle.WaitAny(handles);
                if (signaled != 0 || _cancellation.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    activationRequested();
                }
                catch
                {
                    // Activation is best-effort; keep listening for later launches.
                }
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        _activationEvent.Set();
        try
        {
            _listenerTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
        }

        _cancellation.Dispose();
        _activationEvent.Dispose();
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }
        _mutex.Dispose();
    }
}
