namespace MacKeys;

public sealed class KeyboardManagerStore
{
    public KeyboardManagerStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Keyboard Manager 폴더가 비어 있습니다.", nameof(directory));
        DirectoryPath = directory;
    }

    public string DirectoryPath { get; }

    public static string DefaultDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "Keyboard Manager");

    public void WriteProfiles()
    {
        var dailyPath = Path.Combine(DirectoryPath, "default.json");
        var existing = File.Exists(dailyPath) ? File.ReadAllText(dailyPath) : null;
        var daily = ProfileDocuments.Daily(existing);
        AtomicFile.Write(dailyPath, daily);
        AtomicFile.Write(Path.Combine(DirectoryPath, "game.json"), ProfileDocuments.Game());
        AtomicFile.Write(Path.Combine(DirectoryPath, "off.json"), ProfileDocuments.Off());
    }

    public void SetActiveConfiguration(string name)
    {
        var path = Path.Combine(DirectoryPath, "settings.json");
        string? existing = File.Exists(path) ? File.ReadAllText(path) : null;
        AtomicFile.Write(path, KeyboardManagerSettingsFile.Patch(existing, name));
    }
}
