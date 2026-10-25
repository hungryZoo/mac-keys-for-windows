using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace MacKeys.Tests;

public class ProcessNameTests
{
    [Theory]
    [InlineData("notepad.exe", "notepad")]
    [InlineData("NOTEPAD.EXE", "notepad")]
    [InlineData("notepad", "notepad")]
    [InlineData(@"C:\Windows\System32\notepad.exe", "notepad")]
    [InlineData(@"C:\Games\My Game\Game.exe", "game")]
    [InlineData("  \"game.exe\"  ", "game")]
    public void Normalize_strips_path_and_exe(string input, string expected)
    {
        Assert.Equal(expected, ProcessNames.Normalize(input), ignoreCase: true);
    }

    [Fact]
    public void Equal_matches_name_with_or_without_exe_ignoring_case()
    {
        Assert.True(ProcessNames.Equal("Game.EXE", "game"));
        Assert.True(ProcessNames.Equal(@"D:\later\GAME.exe", "game.exe"));
    }

    [Theory]
    [InlineData("note", "notepad")]
    [InlineData("game", "mygame")]
    [InlineData("game", "game2")]
    [InlineData("pad", "notepad")]
    [InlineData("", "notepad")]
    [InlineData(".exe", "notepad")]
    public void Equal_rejects_partial_names(string left, string right)
    {
        Assert.False(ProcessNames.Equal(left, right));
    }
}

public class GameListTests
{
    [Fact]
    public void Contains_uses_the_process_name_even_when_the_path_changes()
    {
        var games = new[]
        {
            new GameEntry { ProcessName = "Game.exe", Path = @"C:\Old\Game.exe" },
        };

        Assert.True(GameList.Contains(games, @"D:\New\game.EXE"));
        Assert.False(GameList.Contains(games, "GameLauncher.exe"));
    }

    [Fact]
    public void Upsert_does_not_merge_a_shorter_name()
    {
        var first = GameList.Upsert(Array.Empty<GameEntry>(), "notepad.exe", @"C:\Windows\notepad.exe");
        var second = GameList.Upsert(first.Games, "note", @"C:\note.exe");

        Assert.Equal(2, second.Games.Count);
        Assert.False(second.UpdatedExisting);
    }

    [Fact]
    public void Upsert_updates_the_path_for_the_same_name()
    {
        var first = GameList.Upsert(Array.Empty<GameEntry>(), @"C:\Old\Game.exe", @"C:\Old\Game.exe");
        var second = GameList.Upsert(first.Games, "GAME", @"D:\Moved\Game.exe");

        Assert.True(second.UpdatedExisting);
        Assert.Single(second.Games);
        Assert.Equal(@"D:\Moved\Game.exe", second.Games[0].Path);
    }
}

public class ProfileSelectionTests
{
    private static readonly GameEntry Game = new() { ProcessName = "game.exe", Path = @"C:\Games\game.exe" };

    [Fact]
    public void Suspended_profile_stays_off_and_ignores_the_foreground()
    {
        var foreground = ForegroundObservation.Known("game.exe");
        Assert.Equal(Profiles.Off, ProfileSelection.ForForeground(true, foreground, new[] { Game }));
        Assert.False(ProfileSelection.FollowsForeground(true));
        Assert.Equal(Profiles.Off, ProfileSelection.OnExit(true));
    }

    [Fact]
    public void Known_foreground_selects_game_or_daily_by_exact_name()
    {
        Assert.Equal(Profiles.Game, ProfileSelection.ForForeground(false, ForegroundObservation.Known("GAME.EXE"), new[] { Game }));
        Assert.Equal(Profiles.Daily, ProfileSelection.ForForeground(false, ForegroundObservation.Known("notepad.exe"), new[] { Game }));
        Assert.Equal(Profiles.Daily, ProfileSelection.ForForeground(false, ForegroundObservation.Known("gam"), new[] { Game }));
    }

    [Fact]
    public void Unreadable_foreground_stays_on_the_daily_profile()
    {
        Assert.Equal(Profiles.Daily, ProfileSelection.ForForeground(false, ForegroundObservation.Denied(99), new[] { Game }));
        Assert.Equal(Profiles.Daily, ProfileSelection.ForForeground(false, ForegroundObservation.Unavailable, new[] { Game }));
        Assert.Equal(Profiles.Daily, ProfileSelection.OnExit(false));
    }

    [Fact]
    public void Foreground_poll_interval_is_between_300_and_500_milliseconds()
    {
        Assert.InRange(Profiles.ForegroundPollMilliseconds, 300, 500);
    }
}

public class ProfileDocumentTests
{
    private static readonly (string Original, string Target)[] ExpectedShortcuts =
    {
        ("164;67", "162;67"),
        ("164;86", "162;86"),
        ("164;88", "162;88"),
        ("164;90", "162;90"),
        ("164;160;90", "162;89"),
        ("164;83", "162;83"),
        ("164;87", "162;87"),
        ("164;65", "162;65"),
        ("164;70", "162;70"),
        ("164;78", "162;78"),
        ("164;160;78", "162;160;78"),
        ("164;84", "162;84"),
        ("164;160;84", "162;160;84"),
        ("164;82", "162;82"),
        ("164;81", "164;115"),
        ("164;37", "36"),
        ("164;39", "35"),
        ("164;38", "33"),
        ("164;40", "34"),
    };

    [Fact]
    public void Daily_profile_has_caps_lock_and_the_fixed_shortcuts()
    {
        var root = Parse(ProfileDocuments.Daily());
        AssertCapsLock(root, present: true);
        var global = Shortcuts(root);
        Assert.Equal(ExpectedShortcuts.Length, global.Count);
        for (var index = 0; index < ExpectedShortcuts.Length; index++)
        {
            var item = global[index]!.AsObject();
            Assert.Equal(ExpectedShortcuts[index].Original, item["originalKeys"]!.GetValue<string>());
            Assert.Equal(ExpectedShortcuts[index].Target, item["newRemapKeys"]!.GetValue<string>());
            Assert.False(item["exactMatch"]!.GetValue<bool>());
            Assert.Equal(0, item["operationType"]!.GetValue<int>());
            AssertStringKeys(item);
        }

        Assert.Empty(root["remapShortcuts"]!["appSpecific"]!.AsArray());
        AssertEmptyTextMaps(root);
        AssertNoForbiddenKeys(root);
    }

    [Fact]
    public void Daily_profile_keeps_app_specific_shortcuts_and_unlisted_remaps()
    {
        const string existing = """
            {
              "remapKeys": { "inProcess": [
                { "originalKeys": "20", "newRemapKeys": "99" },
                { "originalKeys": "112", "newRemapKeys": "113" }
              ] },
              "remapKeysToText": { "inProcess": [ { "originalKeys": "113", "unicodeText": "kept" } ] },
              "remapShortcuts": {
                "global": [
                  { "originalKeys": "164;67", "exactMatch": true, "operationType": 1, "newRemapKeys": "1" },
                  { "originalKeys": "164;66", "exactMatch": false, "operationType": 0, "newRemapKeys": "162;66" }
                ],
                "appSpecific": [
                  { "originalKeys": "164;87", "exactMatch": false, "operationType": 0, "newRemapKeys": "162;87", "targetApp": "aloha" },
                  { "originalKeys": "164;87", "exactMatch": false, "operationType": 0, "newRemapKeys": "162;87", "targetApp": "chrome" },
                  { "originalKeys": "164;87", "exactMatch": false, "operationType": 0, "newRemapKeys": "162;87", "targetApp": "whale" }
                ]
              },
              "remapShortcutsToText": { "global": [], "appSpecific": [] }
            }
            """;

        var root = Parse(ProfileDocuments.Daily(existing));
        var keys = root["remapKeys"]!["inProcess"]!.AsArray();
        Assert.Equal(2, keys.Count);
        Assert.Equal("20", keys[0]!["originalKeys"]!.GetValue<string>());
        Assert.Equal("21", keys[0]!["newRemapKeys"]!.GetValue<string>());
        Assert.Equal("112", keys[1]!["originalKeys"]!.GetValue<string>());
        Assert.Equal("113", keys[1]!["newRemapKeys"]!.GetValue<string>());

        var global = Shortcuts(root);
        Assert.Equal(ExpectedShortcuts.Length + 1, global.Count);
        Assert.Equal("162;67", global.First(item => item!["originalKeys"]!.GetValue<string>() == "164;67")!["newRemapKeys"]!.GetValue<string>());
        Assert.False(global.First(item => item!["originalKeys"]!.GetValue<string>() == "164;67")!["exactMatch"]!.GetValue<bool>());
        var extra = Assert.Single(global, item => item!["originalKeys"]!.GetValue<string>() == "164;66")!.AsObject();
        Assert.Equal("162;66", extra["newRemapKeys"]!.GetValue<string>());

        var apps = root["remapShortcuts"]!["appSpecific"]!.AsArray();
        Assert.Equal(new[] { "aloha", "chrome", "whale" }, apps.Select(item => item!["targetApp"]!.GetValue<string>()));
        Assert.Equal("kept", root["remapKeysToText"]!["inProcess"]![0]!["unicodeText"]!.GetValue<string>());
    }

    [Fact]
    public void Daily_profile_refuses_invalid_existing_json()
    {
        Assert.ThrowsAny<JsonException>(() => ProfileDocuments.Daily("{"));
    }

    [Fact]
    public void Game_profile_keeps_only_caps_lock()
    {
        var root = Parse(ProfileDocuments.Game());
        AssertCapsLock(root, present: true);
        Assert.Empty(Shortcuts(root));
        Assert.Empty(root["remapShortcuts"]!["appSpecific"]!.AsArray());
        AssertEmptyTextMaps(root);
    }

    [Fact]
    public void Off_profile_has_empty_arrays()
    {
        var root = Parse(ProfileDocuments.Off());
        Assert.Empty(root["remapKeys"]!["inProcess"]!.AsArray());
        Assert.Empty(Shortcuts(root));
        Assert.Empty(root["remapShortcuts"]!["appSpecific"]!.AsArray());
        AssertEmptyTextMaps(root);
    }

    private static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();

    private static JsonArray Shortcuts(JsonObject root) => root["remapShortcuts"]!["global"]!.AsArray();

    private static void AssertCapsLock(JsonObject root, bool present)
    {
        var keys = root["remapKeys"]!["inProcess"]!.AsArray();
        if (!present)
        {
            Assert.Empty(keys);
            return;
        }

        var only = Assert.Single(keys)!.AsObject();
        Assert.Equal("20", only["originalKeys"]!.GetValue<string>());
        Assert.Equal("21", only["newRemapKeys"]!.GetValue<string>());
        Assert.Equal(JsonValueKind.String, only["originalKeys"]!.GetValueKind());
        Assert.Equal(JsonValueKind.String, only["newRemapKeys"]!.GetValueKind());
    }

    private static void AssertEmptyTextMaps(JsonObject root)
    {
        Assert.Empty(root["remapKeysToText"]!["inProcess"]!.AsArray());
        Assert.Empty(root["remapShortcutsToText"]!["global"]!.AsArray());
        Assert.Empty(root["remapShortcutsToText"]!["appSpecific"]!.AsArray());
    }

    private static void AssertStringKeys(JsonObject item)
    {
        Assert.Equal(JsonValueKind.String, item["originalKeys"]!.GetValueKind());
        Assert.Equal(JsonValueKind.String, item["newRemapKeys"]!.GetValueKind());
    }

    private static void AssertNoForbiddenKeys(JsonObject root)
    {
        var forbidden = new HashSet<string> { "9", "91", "92", "165" };
        var singleModifiers = new HashSet<string> { "160", "161", "162", "163", "164" };
        foreach (var item in Shortcuts(root))
        {
            var original = item!["originalKeys"]!.GetValue<string>().Split(';');
            var target = item["newRemapKeys"]!.GetValue<string>().Split(';');
            Assert.DoesNotContain(original, forbidden.Contains);
            Assert.DoesNotContain(target, forbidden.Contains);
            Assert.True(original.Length >= 2);
            Assert.False(original.All(singleModifiers.Contains));
        }
    }
}

public class KeyboardManagerSettingsFileTests
{
    [Fact]
    public void Patch_keeps_unrelated_keys_and_adds_the_three_profiles()
    {
        const string existing = """
            {
              "properties": {
                "activeConfiguration": { "value": "default" },
                "keyboardConfigurations": { "value": [ "default", "custom" ] },
                "editorShortcut": { "win": false, "ctrl": true, "alt": false, "shift": false, "code": 81, "key": "" },
                "futureFlag": true
              },
              "name": "Keyboard Manager",
              "version": "1",
              "extraRoot": { "a": 1 }
            }
            """;

        var root = JsonNode.Parse(KeyboardManagerSettingsFile.Patch(existing, Profiles.Game))!.AsObject();
        var properties = root["properties"]!.AsObject();
        Assert.Equal(Profiles.Game, properties["activeConfiguration"]!["value"]!.GetValue<string>());
        var names = properties["keyboardConfigurations"]!["value"]!.AsArray().Select(item => item!.GetValue<string>()).ToArray();
        Assert.Equal(new[] { "default", "custom", "game", "off" }, names);
        Assert.True(properties["futureFlag"]!.GetValue<bool>());
        Assert.Equal(81, properties["editorShortcut"]!["code"]!.GetValue<int>());
        Assert.Equal(1, root["extraRoot"]!["a"]!.GetValue<int>());
        Assert.Equal("Keyboard Manager", root["name"]!.GetValue<string>());
        Assert.Equal("1", root["version"]!.GetValue<string>());
    }

    [Fact]
    public void Patch_creates_a_file_when_settings_are_missing()
    {
        var root = JsonNode.Parse(KeyboardManagerSettingsFile.Patch(null, Profiles.Off))!.AsObject();
        Assert.Equal(Profiles.Off, root["properties"]!["activeConfiguration"]!["value"]!.GetValue<string>());
        var names = root["properties"]!["keyboardConfigurations"]!["value"]!.AsArray().Select(item => item!.GetValue<string>());
        Assert.Equal(Profiles.All, names);
    }

    [Fact]
    public void Patch_refuses_invalid_json_instead_of_replacing_it()
    {
        Assert.ThrowsAny<JsonException>(() => KeyboardManagerSettingsFile.Patch("{", Profiles.Daily));
    }
}

public class ProfileSessionTests
{
    [Fact]
    public void Apply_writes_three_profiles_and_signals_once()
    {
        using var dir = new TempDirectory();
        var signals = 0;
        var session = new ProfileSession(new KeyboardManagerStore(dir.Path), () => signals++, false, Array.Empty<GameEntry>());

        var result = session.ApplyNow(ForegroundObservation.Known("notepad.exe"));

        Assert.Equal(Profiles.Daily, result.Profile);
        Assert.True(File.Exists(System.IO.Path.Combine(dir.Path, "default.json")));
        Assert.True(File.Exists(System.IO.Path.Combine(dir.Path, "game.json")));
        Assert.True(File.Exists(System.IO.Path.Combine(dir.Path, "off.json")));
        Assert.Equal(1, signals);
        Assert.Equal(Profiles.Daily, ReadActive(dir.Path));
    }

    [Fact]
    public void Apply_keeps_existing_app_specific_shortcuts_without_duplicating_them()
    {
        using var dir = new TempDirectory();
        var dailyPath = System.IO.Path.Combine(dir.Path, "default.json");
        File.WriteAllText(
            dailyPath,
            """
            {"remapShortcuts":{"global":[],"appSpecific":[{"originalKeys":"164;87","exactMatch":false,"operationType":0,"newRemapKeys":"162;87","targetApp":"aloha"},{"originalKeys":"164;87","exactMatch":false,"operationType":0,"newRemapKeys":"162;87","targetApp":"chrome"},{"originalKeys":"164;87","exactMatch":false,"operationType":0,"newRemapKeys":"162;87","targetApp":"whale"}]}}
            """);

        var session = new ProfileSession(new KeyboardManagerStore(dir.Path), () => { }, false, Array.Empty<GameEntry>());
        session.ApplyNow(ForegroundObservation.Known("notepad.exe"));
        session.ApplyNow(ForegroundObservation.Known("notepad.exe"));

        var root = JsonNode.Parse(File.ReadAllText(dailyPath))!.AsObject();
        var apps = root["remapShortcuts"]!["appSpecific"]!.AsArray();
        Assert.Equal(new[] { "aloha", "chrome", "whale" }, apps.Select(item => item!["targetApp"]!.GetValue<string>()));
        var originals = root["remapShortcuts"]!["global"]!.AsArray().Select(item => item!["originalKeys"]!.GetValue<string>()).ToList();
        Assert.Contains("164;37", originals);
        Assert.Contains("164;40", originals);
        Assert.Equal(originals.Count, originals.Distinct().Count());

        var game = JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(dir.Path, "game.json")))!.AsObject();
        Assert.Empty(game["remapShortcuts"]!["global"]!.AsArray());
        Assert.Empty(game["remapShortcuts"]!["appSpecific"]!.AsArray());
    }

    [Fact]
    public void Apply_does_not_replace_profiles_when_the_current_default_json_is_invalid()
    {
        using var dir = new TempDirectory();
        var dailyPath = System.IO.Path.Combine(dir.Path, "default.json");
        File.WriteAllText(dailyPath, "{");
        var session = new ProfileSession(new KeyboardManagerStore(dir.Path), () => { }, false, Array.Empty<GameEntry>());

        Assert.ThrowsAny<JsonException>(() => session.ApplyNow(ForegroundObservation.Known("notepad.exe")));
        Assert.Equal("{", File.ReadAllText(dailyPath));
        Assert.False(File.Exists(System.IO.Path.Combine(dir.Path, "game.json")));
        Assert.False(File.Exists(System.IO.Path.Combine(dir.Path, "off.json")));
    }

    [Fact]
    public void Foreground_game_switches_once_and_suspended_mode_does_not()
    {
        using var dir = new TempDirectory();
        var signals = 0;
        var games = new[] { new GameEntry { ProcessName = "game.exe", Path = @"C:\Games\game.exe" } };
        var session = new ProfileSession(new KeyboardManagerStore(dir.Path), () => signals++, false, games);
        session.ApplyNow(ForegroundObservation.Known("notepad.exe"));

        var switched = session.OnForeground(ForegroundObservation.Known("game.exe"));
        var again = session.OnForeground(ForegroundObservation.Known("GAME.EXE"));

        Assert.Equal(Profiles.Game, switched.Profile);
        Assert.True(switched.Changed);
        Assert.False(again.Changed);
        Assert.Equal(2, signals);

        session.SetSuspended(true, ForegroundObservation.Known("game.exe"));
        var ignored = session.OnForeground(ForegroundObservation.Known("other.exe"));
        Assert.Equal(Profiles.Off, ReadActive(dir.Path));
        Assert.False(ignored.Changed);
        Assert.Equal(Profiles.Off, ignored.Profile);
    }

    [Fact]
    public void Exit_restores_daily_unless_suspended()
    {
        using var dir = new TempDirectory();
        var signals = 0;
        var daily = new ProfileSession(new KeyboardManagerStore(dir.Path), () => signals++, false, Array.Empty<GameEntry>());
        daily.ApplyNow(ForegroundObservation.Known("game.exe", null));
        daily.OnForeground(ForegroundObservation.Known("notepad.exe"));
        signals = 0;
        daily.Exit();
        Assert.Equal(Profiles.Daily, ReadActive(dir.Path));
        Assert.Equal(1, signals);

        signals = 0;
        var suspended = new ProfileSession(new KeyboardManagerStore(dir.Path), () => signals++, true, Array.Empty<GameEntry>());
        suspended.ApplyNow(ForegroundObservation.Known("game.exe"));
        signals = 0;
        suspended.Exit();
        Assert.Equal(Profiles.Off, ReadActive(dir.Path));
        Assert.Equal(0, signals);
    }

    [Fact]
    public void Access_denied_foreground_uses_the_daily_profile()
    {
        using var dir = new TempDirectory();
        var session = new ProfileSession(
            new KeyboardManagerStore(dir.Path),
            () => { },
            false,
            new[] { new GameEntry { ProcessName = "game.exe" } });
        session.ApplyNow(ForegroundObservation.Known("game.exe"));

        var result = session.OnForeground(ForegroundObservation.Denied(10));

        Assert.Equal(Profiles.Daily, result.Profile);
        Assert.Equal(Profiles.Daily, ReadActive(dir.Path));
    }

    private static string ReadActive(string directory)
    {
        var root = JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(directory, "settings.json")))!;
        return root["properties"]!["activeConfiguration"]!["value"]!.GetValue<string>();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = Directory.CreateTempSubdirectory("mackeys-test").FullName;
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

public class AppSettingsStoreTests
{
    [Fact]
    public void Round_trip_keeps_games_and_the_disabled_flag()
    {
        using var dir = new ProfileSessionTestsDir();
        var path = System.IO.Path.Combine(dir.Path, "settings.json");
        AppSettingsStore.Save(path, new AppSettings
        {
            Disabled = true,
            Games = new List<GameEntry>
            {
                new() { ProcessName = "Game.exe", Path = @"C:\Games\Game.exe" },
                new() { ProcessName = "game.exe", Path = @"C:\Other\Game.exe" },
            },
        });

        var loaded = AppSettingsStore.Load(path);
        Assert.Null(loaded.Error);
        Assert.True(loaded.Settings.Disabled);
        var game = Assert.Single(loaded.Settings.Games);
        Assert.Equal("Game.exe", game.ProcessName);
    }

    [Fact]
    public void Corrupt_file_is_not_replaced_by_load()
    {
        var dir = Directory.CreateTempSubdirectory("mackeys-settings").FullName;
        try
        {
            var path = System.IO.Path.Combine(dir, "settings.json");
            File.WriteAllText(path, "{");
            var loaded = AppSettingsStore.Load(path);
            Assert.NotNull(loaded.Error);
            Assert.Empty(loaded.Settings.Games);
            Assert.Equal("{", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class ProfileSessionTestsDir : IDisposable
    {
        public ProfileSessionTestsDir() => Path = Directory.CreateTempSubdirectory("mackeys-appsettings").FullName;
        public string Path { get; }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

public class PowerToysLocatorTests
{
    [Fact]
    public void Detects_an_exe_without_calling_winget()
    {
        var dir = Directory.CreateTempSubdirectory("mackeys-pt").FullName;
        try
        {
            var exe = System.IO.Path.Combine(dir, "PowerToys.exe");
            File.WriteAllText(exe, "");
            var found = PowerToysLocator.Detect(new PowerToysDetectOptions(new[] { exe }, false, false, false));
            Assert.True(found.Installed);
            Assert.Equal(System.IO.Path.GetFullPath(exe), found.ExecutablePath);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Missing_exe_stays_missing_when_lookup_is_disabled()
    {
        var found = PowerToysLocator.Detect(new PowerToysDetectOptions(
            new[] { System.IO.Path.Combine(System.IO.Path.GetTempPath(), "missing-powertoys", "PowerToys.exe") },
            false,
            false,
            false));
        Assert.False(found.Installed);
    }

    [Fact]
    public void Settings_event_name_has_no_global_prefix()
    {
        Assert.Equal("PowerToys_KeyboardManager_Event_Settings", SettingsChangedEvent.Name);
        Assert.False(SettingsChangedEvent.Name.StartsWith("Global\\", StringComparison.OrdinalIgnoreCase));
    }
}

public class StartupRegistrationTests
{
    [Fact]
    public void Checkbox_registration_is_off_until_enabled()
    {
        var registration = new StartupRegistration(@"Software\MacKeysTest\Run");
        try
        {
            registration.Disable();
            Assert.False(registration.IsEnabled());
            registration.Enable(@"C:\Tools\MacKeys.exe");
            Assert.True(registration.IsEnabled());
            Assert.Equal("\"C:\\Tools\\MacKeys.exe\"", StartupRegistration.Quote(@"C:\Tools\MacKeys.exe"));
        }
        finally
        {
            registration.Disable();
        }
    }
}

public class ProcessImageTests
{
    [Fact]
    public void Can_read_this_process_name_and_path()
    {
        var name = ProcessImages.TryGetName(Environment.ProcessId);
        var path = ProcessImages.TryGetPath(Environment.ProcessId);
        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.False(path.AccessDenied);
        Assert.False(string.IsNullOrWhiteSpace(path.Path));
        Assert.Null(ProcessImages.TryGetName(-1));
    }
}
