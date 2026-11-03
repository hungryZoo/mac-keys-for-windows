using Xunit;

namespace MacKeys.Tests;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("1.0.3", "1.0.3")]
    [InlineData("v1.0.3", "1.0.3")]
    [InlineData("1.0.3.0", "1.0.3")]
    [InlineData("1.0.3+sha", "1.0.3")]
    public void Parses_three_part_versions(string text, string expected)
    {
        Assert.True(ReleaseVersion.TryParse(text, out var version));
        Assert.Equal(expected, version.ToString());
    }

    [Fact]
    public void Newer_patch_sorts_after_the_current_release()
    {
        Assert.True(ReleaseVersion.TryParse("1.0.3", out var next));
        Assert.True(ReleaseVersion.TryParse("1.0.2", out var current));
        Assert.True(next.CompareTo(current) > 0);
        Assert.Equal(0, next.CompareTo(new ReleaseVersion(1, 0, 3)));
    }
}

public class UpdateOfferTests
{
    private const string Release = """
        {
          "tag_name": "v1.0.4",
          "body": "휠 위치를 고쳤습니다.",
          "assets": [
            {
              "name": "notes.txt",
              "browser_download_url": "https://github.com/hungryZoo/mac-keys-for-windows/releases/download/v1.0.4/notes.txt"
            },
            {
              "name": "MacKeys-1.0.4-win-x64.exe",
              "browser_download_url": "https://github.com/hungryZoo/mac-keys-for-windows/releases/download/v1.0.4/MacKeys-1.0.4-win-x64.exe"
            },
            {
              "name": "MacKeys-Setup-1.0.4.exe",
              "browser_download_url": "https://github.com/hungryZoo/mac-keys-for-windows/releases/download/v1.0.4/MacKeys-Setup-1.0.4.exe"
            }
          ]
        }
        """;

    [Fact]
    public void Newer_release_keeps_the_notes_and_the_setup_exe()
    {
        var offer = UpdateOffers.Choose("1.0.3", null, Release);
        Assert.NotNull(offer);
        Assert.Equal("1.0.4", offer.Version);
        Assert.Equal("휠 위치를 고쳤습니다.", offer.Notes);
        Assert.EndsWith("MacKeys-Setup-1.0.4.exe", offer.DownloadUrl);
    }

    [Fact]
    public void A_program_exe_is_not_used_as_the_installer()
    {
        const string programOnly = """
            {
              "tag_name": "v1.0.4",
              "body": "노트",
              "assets": [
                {
                  "name": "MacKeys-1.0.4-win-x64.exe",
                  "browser_download_url": "https://github.com/hungryZoo/mac-keys-for-windows/releases/download/v1.0.4/MacKeys-1.0.4-win-x64.exe"
                }
              ]
            }
            """;

        var offer = UpdateOffers.Choose("1.0.3", null, programOnly);
        Assert.NotNull(offer);
        Assert.Null(offer.DownloadUrl);
    }

    [Fact]
    public void Setup_name_match_is_case_insensitive()
    {
        const string json = """
            {
              "tag_name": "v1.0.5",
              "body": "노트",
              "assets": [
                {
                  "name": "MACKEYS-SETUP-1.0.5.EXE",
                  "browser_download_url": "https://github.com/hungryZoo/mac-keys-for-windows/releases/download/v1.0.5/MACKEYS-SETUP-1.0.5.EXE"
                }
              ]
            }
            """;

        var offer = UpdateOffers.Choose("1.0.4", null, json);
        Assert.NotNull(offer);
        Assert.EndsWith("MACKEYS-SETUP-1.0.5.EXE", offer.DownloadUrl);
    }

    [Fact]
    public void Same_version_and_a_skipped_version_are_quiet()
    {
        Assert.Null(UpdateOffers.Choose("1.0.4", null, Release));
        Assert.Null(UpdateOffers.Choose("1.0.4.0", null, Release));
        Assert.Null(UpdateOffers.Choose("1.0.3", "1.0.4", Release));
        Assert.Null(UpdateOffers.Choose("1.0.3", "v1.0.4", Release));
    }

    [Fact]
    public void An_older_skip_does_not_hide_a_later_release()
    {
        var offer = UpdateOffers.Choose("1.0.3", "1.0.3", Release);
        Assert.NotNull(offer);
        Assert.Equal("1.0.4", offer.Version);
    }

    [Fact]
    public void Broken_json_and_a_foreign_download_are_ignored()
    {
        Assert.Null(UpdateOffers.Choose("1.0.3", null, "{"));
        var foreign = Release.Replace(
            "https://github.com/hungryZoo/mac-keys-for-windows/releases/download/v1.0.4/MacKeys-Setup-1.0.4.exe",
            "https://example.com/MacKeys-Setup.exe",
            StringComparison.Ordinal);
        var offer = UpdateOffers.Choose("1.0.3", null, foreign);
        Assert.NotNull(offer);
        Assert.Null(offer.DownloadUrl);
    }
}

public class SkippedUpdateSettingsTests
{
    [Fact]
    public void Skipped_version_survives_a_round_trip()
    {
        var dir = Directory.CreateTempSubdirectory("mackeys-skip").FullName;
        try
        {
            var path = Path.Combine(dir, "settings.json");
            AppSettingsStore.Save(path, new AppSettings
            {
                SkippedUpdate = "1.0.9",
                Games = new List<GameEntry> { new() { ProcessName = "Game.exe", Path = @"C:\Games\Game.exe" } },
            });

            var loaded = AppSettingsStore.Load(path);
            Assert.Null(loaded.Error);
            Assert.Equal("1.0.9", loaded.Settings.SkippedUpdate);
            Assert.Single(loaded.Settings.Games);
            Assert.DoesNotContain("null", File.ReadAllText(path), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
