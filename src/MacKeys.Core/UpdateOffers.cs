using System.Text.Json.Nodes;

namespace MacKeys;

public sealed record UpdateOffer(string Version, string Notes, string? DownloadUrl);

public static class UpdateOffers
{
    public static UpdateOffer? Choose(string currentText, string? skippedText, string releaseJson)
    {
        if (!ReleaseVersion.TryParse(currentText, out var current))
            return null;

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(releaseJson) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }

        if (root == null || !ReleaseVersion.TryParse(Text(root["tag_name"]), out var latest))
            return null;
        if (latest.CompareTo(current) <= 0)
            return null;
        if (ReleaseVersion.TryParse(skippedText, out var skipped) && skipped.CompareTo(latest) == 0)
            return null;

        var notes = Text(root["body"])?.Trim() ?? "";
        if (notes.Length == 0)
            notes = "변경 내용이 없습니다.";

        var assets = new List<(string Name, string Url)>();
        if (root["assets"] is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not JsonObject asset)
                    continue;
                var name = Text(asset["name"]);
                var url = Text(asset["browser_download_url"]);
                if (name != null && url != null)
                    assets.Add((name, url));
            }
        }

        return new UpdateOffer(latest.ToString(), notes, SetupExeUrl(assets));
    }

    // 1.0.3은 이름에 win-x64가 있는 exe를 골라 실행 중인 파일 위에 복사한다.
    // 릴리즈에 그 파일도 두는 이유는, 설치 파일만 있으면 1.0.3이 설치 파일을 자기 자리에 덮어쓰기 때문이다.
    // 이 버전은 이름에 setup이 있는 exe만 설치 파일로 받는다.
    public static string? SetupExeUrl(IReadOnlyList<(string Name, string Url)> assets)
    {
        foreach (var (name, url) in assets)
        {
            if (!IsReleaseAssetUrl(url) || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                continue;
            if (name.Contains("setup", StringComparison.OrdinalIgnoreCase))
                return url;
        }

        return null;
    }

    public static bool IsReleaseAssetUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return false;

        return uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
