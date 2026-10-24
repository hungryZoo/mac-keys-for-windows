using System.ComponentModel;

namespace MacKeys;

public static class SettingsChangedEvent
{
    // PowerToys Keyboard Manager waits on this name in the current session.
    // The Global\ prefix would be a different object, so it is not used.
    public const string Name = "PowerToys_KeyboardManager_Event_Settings";

    public static void Signal()
    {
        var handle = NativeMethods.CreateEventW(IntPtr.Zero, false, false, Name);
        if (handle == IntPtr.Zero)
            throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());

        try
        {
            if (!NativeMethods.SetEvent(handle))
                throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }
}
