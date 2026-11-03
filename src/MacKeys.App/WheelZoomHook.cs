using System.Runtime.InteropServices;

namespace MacKeys;

// Keyboard Manager cannot remap the wheel. This hook runs only while the daily profile is active.
internal sealed class WheelZoomHook : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmMouseWheel = 0x020A;
    private const uint LlmhfInjected = 0x00000001;
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseEventWheel = 0x0800;
    private const uint KeyEventKeyUp = 0x0002;
    private const ushort VkLControl = 0xA2;
    private const ushort VkLMenu = 0xA4;
    private const int VkLMenuState = 0xA4;
    private const int VkRMenuState = 0xA5;
    private const int VkControlState = 0x11;
    private const int VkShiftState = 0x10;
    private const int VkLWinState = 0x5B;
    private const int VkRWinState = 0x5C;

    private readonly HookProc _proc;
    private readonly object _injectGate = new();
    private IntPtr _hook;
    private bool _enabled;

    public WheelZoomHook()
    {
        _proc = Hook;
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled == _enabled)
            return;

        if (!enabled)
        {
            Remove();
            return;
        }

        _hook = SetWindowsHookExW(WhMouseLl, _proc, GetModuleHandleW(null), 0);
        _enabled = _hook != IntPtr.Zero;
    }

    public void Dispose() => Remove();

    private void Remove()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        _enabled = false;
    }

    private IntPtr Hook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0 || wParam.ToInt32() != WmMouseWheel || lParam == IntPtr.Zero)
            return CallNextHookEx(_hook, code, wParam, lParam);

        var data = Marshal.PtrToStructure<MsllHookStruct>(lParam);
        var delta = (short)(data.mouseData >> 16);
        var modifiers = new WheelModifiers(
            Down(VkLMenuState),
            Down(VkRMenuState),
            Down(VkControlState),
            Down(VkShiftState),
            Down(VkLWinState) || Down(VkRWinState),
            (data.flags & LlmhfInjected) != 0);
        if (delta == 0 || !WheelZoom.ShouldConvert(modifiers))
            return CallNextHookEx(_hook, code, wParam, lParam);

        var wheel = delta;
        ThreadPool.QueueUserWorkItem(_ => Inject(wheel));
        return (IntPtr)1;
    }

    private void Inject(int delta)
    {
        var restoreAlt = Down(VkLMenuState);
        var inputs = restoreAlt
            ? new[]
            {
                Key(VkLControl, keyUp: false),
                Key(VkLMenu, keyUp: true),
                Wheel(delta),
                Key(VkLControl, keyUp: true),
                Key(VkLMenu, keyUp: false),
            }
            : new[]
            {
                Key(VkLControl, keyUp: false),
                Wheel(delta),
                Key(VkLControl, keyUp: true),
            };

        lock (_injectGate)
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static bool Down(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static Input Key(ushort virtualKey, bool keyUp) => new()
    {
        Type = InputKeyboard,
        U = new InputUnion
        {
            Ki = new KeybdInput
            {
                wVk = virtualKey,
                dwFlags = keyUp ? KeyEventKeyUp : 0,
            },
        },
    };

    private static Input Wheel(int delta) => new()
    {
        Type = InputMouse,
        U = new InputUnion
        {
            Mi = new MouseInput
            {
                mouseData = unchecked((uint)delta),
                dwFlags = MouseEventWheel,
            },
        },
    };

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public int X;
        public int Y;
        public uint mouseData;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mi;
        [FieldOffset(0)]
        public KeybdInput Ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int idHook, HookProc proc, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? module);
}
