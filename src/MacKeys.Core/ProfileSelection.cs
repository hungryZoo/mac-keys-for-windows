namespace MacKeys;

public enum ForegroundReadStatus
{
    Known,
    AccessDenied,
    Unavailable,
}

public readonly record struct ForegroundObservation(
    ForegroundReadStatus Status,
    string? ProcessName,
    string? Path,
    int ProcessId)
{
    public static ForegroundObservation Known(string processName, string? path = null, int processId = 0) =>
        new(ForegroundReadStatus.Known, processName, path, processId);

    public static ForegroundObservation Denied(int processId = 0) =>
        new(ForegroundReadStatus.AccessDenied, null, null, processId);

    public static ForegroundObservation Unavailable { get; } =
        new(ForegroundReadStatus.Unavailable, null, null, 0);
}

public static class ProfileSelection
{
    public static string ForForeground(
        bool suspended,
        ForegroundObservation foreground,
        IEnumerable<GameEntry> games)
    {
        if (suspended)
            return Profiles.Off;

        if (foreground.Status != ForegroundReadStatus.Known ||
            string.IsNullOrWhiteSpace(foreground.ProcessName))
        {
            return Profiles.Daily;
        }

        return GameList.Contains(games, foreground.ProcessName) ? Profiles.Game : Profiles.Daily;
    }

    public static bool FollowsForeground(bool suspended) => !suspended;

    public static string OnExit(bool suspended) => suspended ? Profiles.Off : Profiles.Daily;
}
