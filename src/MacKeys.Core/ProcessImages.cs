using System.Text;

namespace MacKeys;

public readonly record struct ProcessPathQuery(string? Path, bool AccessDenied);

public static class ProcessImages
{
    public static string? TryGetName(int processId)
    {
        if (processId <= 0)
            return null;

        var snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.Th32CsSnapProcess, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return null;

        try
        {
            var entry = new NativeMethods.ProcessEntry32
            {
                dwSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.ProcessEntry32>(),
            };
            if (!NativeMethods.Process32FirstW(snapshot, ref entry))
                return null;

            do
            {
                if (entry.th32ProcessID == (uint)processId)
                    return string.IsNullOrWhiteSpace(entry.szExeFile) ? null : entry.szExeFile;
            }
            while (NativeMethods.Process32NextW(snapshot, ref entry));

            return null;
        }
        finally
        {
            NativeMethods.CloseHandle(snapshot);
        }
    }

    public static ProcessPathQuery TryGetPath(int processId)
    {
        if (processId <= 0)
            return new ProcessPathQuery(null, false);

        var handle = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, (uint)processId);
        if (handle == IntPtr.Zero)
        {
            var denied = System.Runtime.InteropServices.Marshal.GetLastWin32Error() == NativeMethods.ErrorAccessDenied;
            return new ProcessPathQuery(null, denied);
        }

        try
        {
            var buffer = new StringBuilder(2048);
            var size = buffer.Capacity;
            if (!NativeMethods.QueryFullProcessImageNameW(handle, 0, buffer, ref size))
            {
                var denied = System.Runtime.InteropServices.Marshal.GetLastWin32Error() == NativeMethods.ErrorAccessDenied;
                return new ProcessPathQuery(null, denied);
            }

            var path = buffer.ToString();
            return new ProcessPathQuery(string.IsNullOrWhiteSpace(path) ? null : path, false);
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }
}
