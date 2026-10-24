using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MacKeys;

public sealed class AppSettings
{
    public bool Disabled { get; set; }

    public List<GameEntry> Games { get; set; } = new();
}

public readonly record struct AppSettingsLoad(AppSettings Settings, string? Error);

public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MacKeys",
            "settings.json");

    public static AppSettingsLoad Load(string path)
    {
        if (!File.Exists(path))
            return new AppSettingsLoad(new AppSettings(), null);

        try
        {
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            settings.Games = Deduplicate(settings.Games);
            return new AppSettingsLoad(settings, null);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException)
        {
            return new AppSettingsLoad(new AppSettings(), "설정 파일을 읽지 못해 게임 목록을 비운 채로 시작했습니다.");
        }
    }

    public static void Save(string path, AppSettings settings)
    {
        settings.Games = Deduplicate(settings.Games);
        AtomicFile.Write(path, JsonSerializer.Serialize(settings, Options));
    }

    private static List<GameEntry> Deduplicate(IEnumerable<GameEntry>? games)
    {
        var result = new List<GameEntry>();
        foreach (var game in games ?? Array.Empty<GameEntry>())
        {
            if (game == null || ProcessNames.Normalize(game.ProcessName).Length == 0)
                continue;
            if (result.Any(existing => ProcessNames.Equal(existing.ProcessName, game.ProcessName)))
                continue;
            result.Add(new GameEntry
            {
                ProcessName = Path.GetFileName(game.ProcessName.Trim()),
                Path = game.Path?.Trim() ?? "",
            });
        }

        return result;
    }
}
