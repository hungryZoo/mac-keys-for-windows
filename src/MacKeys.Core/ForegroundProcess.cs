namespace MacKeys;

public static class ForegroundProcess
{
    public static ForegroundObservation Query()
    {
        var window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero)
            return ForegroundObservation.Unavailable;

        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        if (processId == 0)
            return ForegroundObservation.Unavailable;

        return Read((int)processId);
    }

    public static ForegroundObservation Read(int processId)
    {
        var name = ProcessImages.TryGetName(processId);
        var path = ProcessImages.TryGetPath(processId);
        if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(path.Path))
            name = Path.GetFileName(path.Path);

        if (!string.IsNullOrWhiteSpace(name))
            return ForegroundObservation.Known(name, path.Path, processId);

        if (path.AccessDenied)
            return ForegroundObservation.Denied(processId);

        return new ForegroundObservation(ForegroundReadStatus.Unavailable, null, null, processId);
    }
}
