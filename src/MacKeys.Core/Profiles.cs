namespace MacKeys;

public static class Profiles
{
    public const string Daily = "default";
    public const string Game = "game";
    public const string Off = "off";

    public static readonly IReadOnlyList<string> All = new[] { Daily, Game, Off };

    public const int ForegroundPollMilliseconds = 400;
}
