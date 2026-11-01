using System.Security.Cryptography;
using System.Text.Json;

namespace JARVIS;

public sealed record AvailableUpdate(Version Version, string Tag, string Notes, Uri InstallerUrl,
    string FileName, long Bytes, string Sha256);

public sealed class AppUpdateService
{
    private const string Repository = "3bitfun/jarvis";
    private const long MaximumInstallerBytes = 512L * 1024 * 1024;
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<AvailableUpdate?> CheckAsync(Version current, CancellationToken token = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The built-in installer is for Windows.");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{Repository}/releases/latest");
        request.Headers.UserAgent.ParseAdd("JARVIS-Updater/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await HttpPayload.ReadTextAsync(response, 1_000_000, token));
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version))
            throw new InvalidDataException("The latest GitHub release has an invalid version tag.");
        if (version <= current) return null;
        var expectedName = $"JARVIS-Setup-{version}-win-x64.exe";
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != expectedName) continue;
            var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
            if (url.Scheme != Uri.UriSchemeHttps || url.Host != "github.com" ||
                !url.AbsolutePath.StartsWith($"/{Repository}/releases/download/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The release installer URL is not a GitHub release asset.");
            var size = asset.GetProperty("size").GetInt64();
            var digest = asset.TryGetProperty("digest", out var digestValue) ? digestValue.GetString() ?? "" : "";
            if (size <= 0 || size > MaximumInstallerBytes || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                digest.Length != 71 || !digest[7..].All(Uri.IsHexDigit))
                throw new InvalidDataException("The release installer is missing a valid SHA-256 digest or size.");
            return new AvailableUpdate(version, tag, root.GetProperty("body").GetString() ?? "", url,
                expectedName, size, digest[7..]);
        }
        throw new InvalidDataException("The latest release has no Windows x64 installer. Open the release page for details.");
    }

    public async Task<string> DownloadAsync(AvailableUpdate update, IProgress<DownloadProgress>? progress = null,
        CancellationToken token = default)
    {
        var directory = Path.Combine(AppPaths.DataDirectory, "updates");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, update.FileName);
        if (File.Exists(destination) && await MatchesAsync(destination, update, token)) return destination;
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".download";
        try
        {
            using var response = await Client.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is { } length && length != update.Bytes)
                throw new InvalidDataException("The installer size differs from the release metadata.");
            await using var input = await response.Content.ReadAsStreamAsync(token);
            await using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[128 * 1024];
            long received = 0;
            while (true)
            {
                var count = await input.ReadAsync(buffer, token);
                if (count == 0) break;
                received += count;
                if (received > MaximumInstallerBytes || received > update.Bytes)
                    throw new InvalidDataException("The installer is larger than the release metadata.");
                hash.AppendData(buffer, 0, count);
                await output.WriteAsync(buffer.AsMemory(0, count), token);
                progress?.Report(new DownloadProgress(received, update.Bytes));
            }
            await output.FlushAsync(token);
            if (received != update.Bytes || !Convert.ToHexString(hash.GetHashAndReset()).Equals(update.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Installer verification failed. The downloaded file was discarded.");
            output.Close();
            File.Move(temporary, destination, true);
            return destination;
        }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } }
    }

    private static async Task<bool> MatchesAsync(string path, AvailableUpdate update, CancellationToken token)
    {
        if (new FileInfo(path).Length != update.Bytes) return false;
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token))
            .Equals(update.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}
