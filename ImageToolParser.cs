using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace JARVIS;
public sealed record ImageActionResult(string Reply, IReadOnlyList<string> Paths);
public static class ImageToolParser
{
    public const string Protocol = "When the user explicitly requests an image generation, invoke this tool exactly once using a fenced jarvis-image JSON object: {\"tool\":\"generate_image\",\"prompt\":\"detailed image prompt\"}. Do not claim generation succeeded until the image tool result confirms it. Otherwise answer normally. Do not invent image URLs.";
    private static readonly Regex Tool = new(@"```jarvis-image\s*(\{[\s\S]*?\})\s*```", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
    public static async Task<ImageActionResult> ProcessAsync(string response, ProviderProfile provider, string workspace, CancellationToken token)
    {
        var paths = new List<string>();
        var matches = Tool.Matches(response);
        if (matches.Count > 3) throw new InvalidDataException("A response may request at most three images.");
        var rewrittenBuilder = new System.Text.StringBuilder();
        var cursor = 0;
        foreach (Match match in matches)
        {
            token.ThrowIfCancellationRequested();
            rewrittenBuilder.Append(response, cursor, match.Index - cursor);
            string toolReply;
            try
            {
                using var json = JsonDocument.Parse(match.Groups[1].Value);
                var root = json.RootElement;
                if (root.GetProperty("tool").GetString() != "generate_image") throw new InvalidDataException("Unsupported image tool.");
                var prompt = root.GetProperty("prompt").GetString();
                if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 2000) throw new InvalidDataException("Image prompts must contain 1–2,000 characters.");
                var image = await GenerateAsync(provider, prompt, token);
                var directory = Path.Combine(Path.GetFullPath(workspace), "images");
                Directory.CreateDirectory(directory);
                var extension = image.ContentType switch { "image/jpeg" => ".jpg", "image/webp" => ".webp", _ => ".png" };
                var path = Path.Combine(directory, "jarvis-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + extension);
                await File.WriteAllBytesAsync(path, image.Bytes, token);
                paths.Add(path);
                toolReply = $"Generated image saved to `{path}`.";
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { toolReply = "Image generation failed: " + ex.Message; }
            rewrittenBuilder.Append(toolReply);
            cursor = match.Index + match.Length;
        }
        rewrittenBuilder.Append(response, cursor, response.Length - cursor);
        var rewritten = rewrittenBuilder.ToString();
        return new ImageActionResult(rewritten, paths);
    }
    private static async Task<(byte[] Bytes, string ContentType)> GenerateAsync(ProviderProfile profile, string prompt, CancellationToken token)
    {
        var baseUrl = profile.BaseUrl.TrimEnd('/');
        if (baseUrl.EndsWith("/chat/completions")) baseUrl = baseUrl[..^"/chat/completions".Length];
        if (baseUrl.EndsWith("/images/generations")) baseUrl = baseUrl[..^"/images/generations".Length];
        var endpoint = baseUrl + "/images/generations";
        var endpointUri = new Uri(endpoint);
        if (endpointUri.Scheme != "https" && !(endpointUri.Scheme == "http" && endpointUri.IsLoopback)) throw new IOException("Image API must use HTTPS (or localhost for development).");
        if (!string.IsNullOrEmpty(endpointUri.UserInfo)) throw new IOException("Put image credentials in the API key field.");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (string.IsNullOrWhiteSpace(profile.Model)) throw new InvalidOperationException("Enter an image model ID in Settings → Engine → Image generation.");
        if (!string.IsNullOrWhiteSpace(profile.ApiKey)) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", profile.ApiKey);
        request.Content = System.Net.Http.Json.JsonContent.Create(new { model = profile.Model, prompt, n = 1 });
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(2) };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Image endpoint returned HTTP {(int)response.StatusCode}; this provider/model may not support image generation.");
        using var document = JsonDocument.Parse(await HttpPayload.ReadTextAsync(response, 36_000_000, token));
        var data = document.RootElement.GetProperty("data")[0];
        if (data.TryGetProperty("b64_json", out var encoded))
        {
            var bytes = Convert.FromBase64String(encoded.GetString() ?? "");
            return (ValidateImage(bytes), "image/png");
        }
        var url = data.GetProperty("url").GetString() ?? throw new InvalidDataException("Provider returned no image.");
        return await SafeImageDownloader.DownloadAsync(url, token);
    }
    private static byte[] ValidateImage(byte[] bytes)
    {
        if (bytes.Length is < 16 or > 25_000_000) throw new InvalidDataException("Image exceeds the 25 MB size limit or is empty.");
        if (!(bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71 }) || bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 }) || System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF"))
            throw new InvalidDataException("Provider did not return a PNG, JPEG, or WebP image.");
        return bytes;
    }
}
public static class SafeImageDownloader
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectCallback = async (context, token) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
            var ip = addresses.FirstOrDefault(IsPublicAddress) ?? throw new IOException("Image host must resolve to a public address.");
            var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try { await socket.ConnectAsync(ip, context.DnsEndPoint.Port, token); return new NetworkStream(socket, true); }
            catch { socket.Dispose(); throw; }
        }
    }) { Timeout = TimeSpan.FromSeconds(30) };
    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        if (IPAddress.IsLoopback(address)) return false;
        if (bytes.Length == 16) return (bytes[0] & 0xE0) == 0x20;
        return bytes[0] != 0 && bytes[0] != 10 && bytes[0] != 127 && bytes[0] < 224 &&
            !(bytes[0] == 169 && bytes[1] == 254) && !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
            !(bytes[0] == 192 && bytes[1] == 168) && !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127);
    }
    public static async Task<(byte[] Bytes, string ContentType)> DownloadAsync(string url, CancellationToken token)
    {
        var uri = new Uri(url);
        if (uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)) throw new IOException("Generated image links must use HTTPS.");
        using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var media = response.Content.Headers.ContentType?.MediaType ?? "";
        if (media is not ("image/png" or "image/jpeg" or "image/webp")) throw new InvalidDataException("Image link returned an unsupported media type.");
        if (response.Content.Headers.ContentLength > 25_000_000) throw new InvalidDataException("Image exceeds the 25 MB limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream(); var buffer = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(buffer, token)) > 0)
        { if (output.Length + read > 25_000_000) throw new InvalidDataException("Image exceeds the 25 MB limit."); await output.WriteAsync(buffer.AsMemory(0, read), token); }
        var bytes = output.ToArray();
        if (bytes.Length < 16) throw new InvalidDataException("Image download is empty.");
        return (bytes, media);
    }
}
