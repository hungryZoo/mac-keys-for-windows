using System.Text;

namespace MacKeys;

public sealed record RunningProcessRow(
    string ProcessName,
    string? WindowTitle,
    string? Path,
    int ProcessId,
    bool PathUnavailable);

public static class RunningProcesses
{
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase)
    {
        "system",
        "registry",
        "smss",
        "csrss",
        "wininit",
        "winlogon",
        "services",
        "lsass",
        "lsaiso",
        "svchost",
        "fontdrvhost",
        "dwm",
        "secure system",
        "memory compression",
        "idle",
    };

    public static IReadOnlyList<RunningProcessRow> List()
    {
        var titles = ReadWindowTitles();
        var snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.Th32CsSnapProcess, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return Array.Empty<RunningProcessRow>();

        var rows = new Dictionary<string, RunningProcessRow>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var entry = new NativeMethods.ProcessEntry32
            {
                dwSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.ProcessEntry32>(),
            };
            if (!NativeMethods.Process32FirstW(snapshot, ref entry))
                return Array.Empty<RunningProcessRow>();

            do
            {
                var pid = (int)entry.th32ProcessID;
                if (pid <= 4 || string.IsNullOrWhiteSpace(entry.szExeFile))
                    continue;

                var normalized = ProcessNames.Normalize(entry.szExeFile);
                if (normalized.Length == 0 || Skipped.Contains(normalized))
                    continue;

                titles.TryGetValue((uint)pid, out var title);
                var path = ProcessImages.TryGetPath(pid);
                var row = new RunningProcessRow(
                    entry.szExeFile,
                    string.IsNullOrWhiteSpace(title) ? null : title,
                    path.Path,
                    pid,
                    path.Path == null);

                rows[normalized] = rows.TryGetValue(normalized, out var current)
                    ? Merge(current, row)
                    : row;
            }
            while (NativeMethods.Process32NextW(snapshot, ref entry));
        }
        finally
        {
            NativeMethods.CloseHandle(snapshot);
        }

        return rows.Values
            .OrderByDescending(row => row.WindowTitle != null)
            .ThenBy(row => row.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static RunningProcessRow Merge(RunningProcessRow current, RunningProcessRow candidate)
    {
        var path = current.Path ?? candidate.Path;
        var title = current.WindowTitle ?? candidate.WindowTitle;
        var processId = current.Path != null ? current.ProcessId : candidate.ProcessId;
        return new RunningProcessRow(current.ProcessName, title, path, processId, path == null);
    }

    private static Dictionary<uint, string> ReadWindowTitles()
    {
        var titles = new Dictionary<uint, string>();
        NativeMethods.EnumWindows((window, _) =>
        {
            if (!NativeMethods.IsWindowVisible(window))
                return true;

            var length = NativeMethods.GetWindowTextLengthW(window);
            if (length <= 0)
                return true;

            var buffer = new StringBuilder(length + 1);
            NativeMethods.GetWindowTextW(window, buffer, buffer.Capacity);
            var title = buffer.ToString().Trim();
            if (title.Length == 0)
                return true;

            NativeMethods.GetWindowThreadProcessId(window, out var processId);
            if (processId != 0 && !titles.ContainsKey(processId))
                titles[processId] = title;
            return true;
        }, IntPtr.Zero);
        return titles;
    }
}
