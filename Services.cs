using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace JARVIS;

public sealed class ProviderProfile
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
}
public sealed class McpServerProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Custom MCP";
    public string Transport { get; set; } = "stdio";
    public string Command { get; set; } = "";
    public List<string> Arguments { get; set; } = new();
    public string WorkingDirectory { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public bool AutoConnect { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> EnabledTools { get; set; } = new();
}
public sealed class AppConfig
{
    public string ActiveProvider { get; set; } = "Local";
    public string Theme { get; set; } = "Obsidian Glass";
    public int VisualStyleVersion { get; set; }
    public string Accent { get; set; } = "#D5E9A4";
    public string Tint { get; set; } = "#17191B";
    public string Workspace { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JARVIS", "workspace");
    public bool FileTools { get; set; }
    public bool DeliberateMode { get; set; } = true;
    public string FontFamily { get; set; } = FontCatalog.Inter;
    public int FontPickerVersion { get; set; }
    public bool KnowledgeEnabled { get; set; } = true;
    public bool WebSearchEnabled { get; set; }
    public List<string> DisabledSkills { get; set; } = new();
    public bool MemoryEnabled { get; set; } = true;
    public string UserProfile { get; set; } = "";
    public string PreferredLocalModel { get; set; } = "";
    public List<string> FavoriteAppPaths { get; set; } = new();
    public List<string> RecentAppPaths { get; set; } = new();
    public List<string> ShortcutAppPaths { get; set; } = new() { "", "", "", "", "" };
    public List<string> WorkRoutineAppPaths { get; set; } = new() { "", "", "" };
    public int WorkFocusMinutes { get; set; } = 25;
    public List<McpServerProfile> McpServers { get; set; } = new();
    public Dictionary<string, ProviderProfile> Providers { get; set; } = new()
    {
        ["Nvidia NIM"] = new() { BaseUrl = "https://integrate.api.nvidia.com/v1", Model = "" },
        ["Groq Cloud"] = new() { BaseUrl = "https://api.groq.com/openai/v1", Model = "" },
        ["OpenRouter"] = new() { BaseUrl = "https://openrouter.ai/api/v1", Model = "" },
        ["Google Gemini"] = new() { BaseUrl = "https://generativelanguage.googleapis.com/v1beta", Model = "" },
        ["Custom OpenAI"] = new() { BaseUrl = "http://localhost:1234/v1", Model = "" }
    };
}
public sealed class SecureConfigStore
{
    private readonly string _directory;
    public SecureConfigStore(string? directory = null) => _directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JARVIS");
    public AppConfig Load()
    {
        var path = Path.Combine(_directory, "config.encrypted.json");
        if (!File.Exists(path)) return new();
        var envelope = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
        var bytes = Convert.FromBase64String(envelope["payload"]);
        byte[] plain;
        if (OperatingSystem.IsWindows()) plain = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
        else
        {
            plain = new byte[bytes.Length - 28];
            using var aes = new AesGcm(Key(), 16);
            aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(28), bytes.AsSpan(12, 16), plain);
        }
        return JsonSerializer.Deserialize<AppConfig>(plain) ?? new();
    }
    private byte[] Key()
    {
        if (OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows uses DPAPI.");
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "config.key");
        if (!File.Exists(path))
        {
            using var file = new FileStream(path, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite });
            file.Write(RandomNumberGenerator.GetBytes(32));
        }
        return File.ReadAllBytes(path);
    }
    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(_directory);
        var plain = JsonSerializer.SerializeToUtf8Bytes(config);
        byte[] encrypted;
        if (OperatingSystem.IsWindows()) encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        else
        {
            encrypted = new byte[plain.Length + 28];
            RandomNumberGenerator.Fill(encrypted.AsSpan(0, 12));
            using var aes = new AesGcm(Key(), 16);
            aes.Encrypt(encrypted.AsSpan(0, 12), plain, encrypted.AsSpan(28), encrypted.AsSpan(12, 16));
        }
        var path = Path.Combine(_directory, "config.encrypted.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new { payload = Convert.ToBase64String(encrypted) }));
        File.Move(path + ".tmp", path, true);
    }
}
public interface ICloudAdapter
{
    Task<string> CompleteAsync(ProviderProfile profile, string system, string prompt, int maxTokens, CancellationToken token);
}
public class OpenAiAdapter : ICloudAdapter
{
    protected static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(2) };
    public virtual async Task<string> CompleteAsync(ProviderProfile profile, string system, string prompt, int maxTokens, CancellationToken token)
    {
        var url = profile.BaseUrl.TrimEnd('/');
        if (!url.EndsWith("/chat/completions")) url += "/chat/completions";
        Validate(url);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (!string.IsNullOrWhiteSpace(profile.ApiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", profile.ApiKey);
        request.Content = JsonContent.Create(new { model = profile.Model, messages = new[] { new { role = "system", content = system }, new { role = "user", content = prompt } }, max_tokens = maxTokens, stream = false });
        using var response = await Client.SendAsync(request, token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Provider returned HTTP {(int)response.StatusCode}. Check the endpoint, key, and model.");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
    protected static void Validate(string url)
    {
        var uri = new Uri(url);
        if (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) throw new InvalidOperationException("Use HTTPS for cloud endpoints, or HTTP for localhost.");
        if (!string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidOperationException("Put credentials in the API key field.");
    }
}
public sealed class NvidiaNimAdapter : OpenAiAdapter { }
public sealed class GroqAdapter : OpenAiAdapter { }
public sealed class OpenRouterAdapter : OpenAiAdapter { }
public sealed class GeminiAdapter : OpenAiAdapter
{
    public override async Task<string> CompleteAsync(ProviderProfile profile, string system, string prompt, int maxTokens, CancellationToken token)
    {
        var url = profile.BaseUrl.TrimEnd('/') + "/models/" + Uri.EscapeDataString(profile.Model) + ":generateContent";
        Validate(url);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-goog-api-key", profile.ApiKey);
        request.Content = JsonContent.Create(new { systemInstruction = new { parts = new[] { new { text = system } } }, contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } }, generationConfig = new { maxOutputTokens = maxTokens } });
        using var response = await Client.SendAsync(request, token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Gemini returned HTTP {(int)response.StatusCode}. Check credentials and model.");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        return string.Concat(doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts").EnumerateArray().Where(p => p.TryGetProperty("text", out _)).Select(p => p.GetProperty("text").GetString()));
    }
}
public sealed class ProviderRouter
{
    private readonly Dictionary<string, ICloudAdapter> _adapters = new() { ["Nvidia NIM"] = new NvidiaNimAdapter(), ["Groq Cloud"] = new GroqAdapter(), ["OpenRouter"] = new OpenRouterAdapter(), ["Google Gemini"] = new GeminiAdapter(), ["Custom OpenAI"] = new OpenAiAdapter() };
    public Task<string> CompleteAsync(AppConfig config, string system, string prompt, int maxTokens, CancellationToken token)
    {
        var profile = config.Providers[config.ActiveProvider];
        if (string.IsNullOrWhiteSpace(profile.Model)) throw new InvalidOperationException("Enter a cloud model ID in Settings.");
        return _adapters[config.ActiveProvider].CompleteAsync(profile, system, prompt, maxTokens, token);
    }
}
