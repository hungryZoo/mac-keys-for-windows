namespace MacKeys;

internal static class SingleInstance
{
    private const string MutexName = @"Local\MacKeys.SingleInstance";
    private const string EventName = @"Local\MacKeys.ShowWindow";

    private static Mutex? _mutex;
    private static EventWaitHandle? _showEvent;
    private static Thread? _thread;

    public static bool TryBecomePrimary(Action show)
    {
        _mutex = new Mutex(false, MutexName);
        try
        {
            if (!_mutex.WaitOne(0))
            {
                SignalExisting();
                return false;
            }
        }
        catch (AbandonedMutexException)
        {
            // The previous process ended without releasing the mutex. This process owns it now.
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        _thread = new Thread(() =>
        {
            while (_showEvent.WaitOne())
                show();
        })
        {
            IsBackground = true,
            Name = "MacKeys.ShowWindow",
        };
        _thread.Start();
        return true;
    }

    private static void SignalExisting()
    {
        try
        {
            using var existing = EventWaitHandle.OpenExisting(EventName);
            existing.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // The first process is still starting. The user can click the tray icon.
        }
    }
}
