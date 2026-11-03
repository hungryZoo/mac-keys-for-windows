using System.IO;
using System.Net.Http;

namespace MacKeys;

internal static class UpdateClient
{
    public static async Task<UpdateOffer?> FindNewerAsync(string current, string? skipped, CancellationToken cancel = default)
    {
        try
        {
            using var client = Create(TimeSpan.FromSeconds(20));
            using var response = await client.GetAsync(
                "https://api.github.com/repos/hungryZoo/mac-keys-for-windows/releases/latest",
                cancel);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancel);
            return UpdateOffers.Choose(current, skipped, json);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
    }

    public static async Task DownloadAsync(
        string url,
        string destination,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancel)
    {
        if (!UpdateOffers.IsReleaseAssetUrl(url))
            throw new InvalidOperationException("릴리즈 파일 주소가 올바르지 않습니다.");

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var client = Create(TimeSpan.FromMinutes(5));
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancel);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long received = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancel);
            if (read == 0)
                break;

            await output.WriteAsync(buffer.AsMemory(0, read), cancel);
            received += read;
            progress?.Report(new DownloadProgress(received, total));
        }
    }

    private static HttpClient Create(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MacKeys");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}

internal readonly record struct DownloadProgress(long Received, long? Total);
