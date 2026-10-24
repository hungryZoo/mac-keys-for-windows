namespace MacKeys;

public sealed class GameEntry
{
    public string ProcessName { get; set; } = "";

    public string Path { get; set; } = "";
}

public readonly record struct GameListChange(IReadOnlyList<GameEntry> Games, bool UpdatedExisting);

public static class GameList
{
    public static bool Contains(IEnumerable<GameEntry> games, string? processName)
    {
        return games.Any(game =>
            ProcessNames.Equal(game.ProcessName, processName) ||
            ProcessNames.Equal(game.Path, processName));
    }

    public static GameListChange Upsert(IReadOnlyList<GameEntry> games, string processName, string? path)
    {
        var fileName = Path.GetFileName(processName.Trim().Trim('"'));
        if (ProcessNames.Normalize(fileName).Length == 0)
            throw new ArgumentException("프로세스 이름이 비어 있습니다.", nameof(processName));

        var copy = games.Select(Clone).ToList();
        var existing = copy.FirstOrDefault(game => ProcessNames.Equal(game.ProcessName, fileName));
        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(path))
                existing.Path = path;
            return new GameListChange(copy, true);
        }

        copy.Add(new GameEntry
        {
            ProcessName = fileName,
            Path = path?.Trim() ?? "",
        });
        return new GameListChange(copy, false);
    }

    public static IReadOnlyList<GameEntry> Remove(IReadOnlyList<GameEntry> games, string processName)
    {
        return games.Where(game => !ProcessNames.Equal(game.ProcessName, processName)).Select(Clone).ToList();
    }

    private static GameEntry Clone(GameEntry game)
    {
        return new GameEntry
        {
            ProcessName = game.ProcessName,
            Path = game.Path,
        };
    }
}
