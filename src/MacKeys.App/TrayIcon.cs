using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MacKeys;

internal sealed class TrayIcon : IDisposable
{
    private const int CallbackMessage = 0x8000 + 25;
    private const uint NimAdd = 0;
    private const uint NimModify = 1;
    private const uint NimDelete = 2;
    private const uint NifMessage = 0x1;
    private const uint NifIcon = 0x2;
    private const uint NifTip = 0x4;
    private const uint MfString = 0;
    private const uint MfSeparator = 0x800;
    private const uint TpmReturnCmd = 0x100;
    private const uint TpmRightButton = 0x2;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonUp = 0x0205;
    private const int WmLButtonDblClk = 0x0203;
    private const uint WmNull = 0;
    private const uint SpiGetForegroundLockTimeout = 0x2000;
    private const uint SpiSetForegroundLockTimeout = 0x2001;

    private readonly HwndSource _source;
    private readonly IntPtr _icon;
    private bool _added;

    public TrayIcon()
    {
        // A message-only window cannot become foreground, so TrackPopupMenu closes immediately.
        // This hidden popup stays off-screen and out of Alt+Tab, and it can own the tray menu.
        var parameters = new HwndSourceParameters("MacKeys.Tray")
        {
            Width = 1,
            Height = 1,
            PositionX = -32000,
            PositionY = -32000,
            WindowStyle = unchecked((int)0x80000000),
            ExtendedWindowStyle = 0x00000080,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        _icon = AppIcon.CreateIcon();
        Add(App.ProductName);
    }

    public event Action? OpenRequested;
    public event Action? ApplyRequested;
    public event Action? SuspendToggleRequested;
    public event Action? ExitRequested;
    public Func<bool>? IsSuspended { get; set; }

    public void SetTooltip(string text)
    {
        if (!_added)
            return;

        var data = CreateData(text);
        Shell_NotifyIconW(NimModify, ref data);
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = CreateData(App.ProductName);
            Shell_NotifyIconW(NimDelete, ref data);
            _added = false;
        }

        if (_icon != IntPtr.Zero)
            DestroyIcon(_icon);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private void Add(string tip)
    {
        var data = CreateData(tip);
        _added = Shell_NotifyIconW(NimAdd, ref data);
    }

    private NotifyIconData CreateData(string tip)
    {
        var data = new NotifyIconData
        {
            hWnd = _source.Handle,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = CallbackMessage,
            hIcon = _icon,
            szTip = tip.Length > 127 ? tip[..127] : tip,
        };
        data.cbSize = Marshal.SizeOf<NotifyIconData>();
        return data;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        if (message != CallbackMessage)
            return IntPtr.Zero;

        var mouse = lparam.ToInt32();
        if (mouse is WmLButtonUp or WmLButtonDblClk)
        {
            OpenRequested?.Invoke();
            handled = true;
        }
        else if (mouse == WmRButtonUp)
        {
            ShowMenu(hwnd);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void ShowMenu(IntPtr hwnd)
    {
        var menu = CreatePopupMenu();
        AppendMenuW(menu, MfString, 1, "열기");
        AppendMenuW(menu, MfString, 2, "적용하기");
        if (IsSuspended?.Invoke() != true)
            AppendMenuW(menu, MfString, 3, "끄기");
        AppendMenuW(menu, MfSeparator, 0, null);
        AppendMenuW(menu, MfString, 4, "종료");

        GetCursorPos(out var point);
        // TrackPopupMenu closes at once unless this window is the foreground window.
        // A tray click does not always clear the foreground lock, so drop the lock
        // for the duration of the menu and attach to the thread that currently has it.
        uint previousTimeout = 0;
        SystemParametersInfoW(SpiGetForegroundLockTimeout, 0, ref previousTimeout, 0);
        uint unlocked = 0;
        SystemParametersInfoW(SpiSetForegroundLockTimeout, 0, ref unlocked, 0);
        var foreground = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        var currentThread = GetCurrentThreadId();
        var attached = foregroundThread != 0
            && foregroundThread != currentThread
            && AttachThreadInput(foregroundThread, currentThread, true);
        SetForegroundWindow(hwnd);
        var command = TrackPopupMenuEx(menu, TpmReturnCmd | TpmRightButton, point.X, point.Y, hwnd, IntPtr.Zero);
        PostMessageW(hwnd, WmNull, IntPtr.Zero, IntPtr.Zero);
        if (attached)
            AttachThreadInput(foregroundThread, currentThread, false);
        SystemParametersInfoW(SpiSetForegroundLockTimeout, 0, ref previousTimeout, 0);
        DestroyMenu(menu);

        switch (command)
        {
            case 1:
                OpenRequested?.Invoke();
                break;
            case 2:
                ApplyRequested?.Invoke();
                break;
            case 3:
                SuspendToggleRequested?.Invoke();
                break;
            case 4:
                ExitRequested?.Invoke();
                break;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr menu, uint flags, nuint id, string? text);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr window, IntPtr unused);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint sourceThread, uint targetThread, bool attach);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfoW(uint action, uint param, ref uint value, uint winIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessageW(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
}
