using System.Net.Http.Headers;
using System.Text.Json;

namespace JARVIS;

public static class ProviderModels
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    public static async Task<IReadOnlyList<string>> FetchAsync(string provider, string baseUrl, string key, CancellationToken token = default)
    {
        var root = baseUrl.Trim().TrimEnd('/');
        if (root.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) root = root[..^17];
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Use an HTTPS base URL, or HTTP on localhost.");
        var gemini = provider == "Google Gemini";
        var ids = new HashSet<string>(StringComparer.Ordinal);
        string? page = null;
        for (var i = 0; i < 20; i++)
        {
            var endpoint = root + "/models" + (gemini ? "?pageSize=1000" + (page is null ? "" : "&pageToken=" + Uri.EscapeDataString(page)) : "");
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            if (!string.IsNullOrWhiteSpace(key))
            {
                if (gemini) request.Headers.Add("x-goog-api-key", key);
                else request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            }
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Model list returned HTTP {(int)response.StatusCode}. Check the API key and base URL, or choose Custom model ID.");
            using var document = JsonDocument.Parse(await HttpPayload.ReadTextAsync(response, 4_000_000, token));
            if (!document.RootElement.TryGetProperty(gemini ? "models" : "data", out var items) || items.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("This endpoint did not return a model list. You can still enter a custom model ID.");
            foreach (var item in items.EnumerateArray())
            {
                if (gemini && item.TryGetProperty("supportedGenerationMethods", out var methods) && !methods.EnumerateArray().Any(m => m.GetString() == "generateContent")) continue;
                if (item.TryGetProperty(gemini ? "name" : "id", out var id) && id.GetString() is { Length: > 0 } value)
                    ids.Add(gemini && value.StartsWith("models/") ? value[7..] : value);
            }
            page = gemini && document.RootElement.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
            if (string.IsNullOrEmpty(page)) break;
        }
        return ids.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
