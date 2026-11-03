namespace MacKeys;

internal static class SingleInstance
{
    private const string MutexName = @"Local\MacKeys.SingleInstance";
    private const string ShowEventName = @"Local\MacKeys.ShowWindow";
    private const string ExitEventName = @"Local\MacKeys.Exit";

    private static Mutex? _mutex;
    private static EventWaitHandle? _showEvent;
    private static EventWaitHandle? _exitEvent;
    private static Thread? _thread;

    public static bool TryBecomePrimary(Action show, Action exit)
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

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName);
        var handles = new WaitHandle[] { _showEvent, _exitEvent };
        _thread = new Thread(() =>
        {
            while (true)
            {
                switch (WaitHandle.WaitAny(handles))
                {
                    case 0:
                        show();
                        break;
                    default:
                        exit();
                        return;
                }
            }
        })
        {
            IsBackground = true,
            Name = "MacKeys.InstanceEvents",
        };
        _thread.Start();
        return true;
    }

    private static void SignalExisting()
    {
        try
        {
            using var existing = EventWaitHandle.OpenExisting(ShowEventName);
            existing.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // The first process is still starting. The user can click the tray icon.
        }
    }
}
