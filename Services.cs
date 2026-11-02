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
    public string Workspace { get; set; } = Path.Combine(AppPaths.DataDirectory, "workspace");
    public bool FileTools { get; set; }
    public bool DeliberateMode { get; set; } = true;
    public int ResponseTokens { get; set; } = 1024;
    public bool GlassEnabled { get; set; } = true;
    public bool SetupCompleted { get; set; }
    public bool SetupDismissed { get; set; }
    public bool VerityMode { get; set; }
    public bool PetEnabled { get; set; }
    public int PetSize { get; set; } = 96;
    public double PetOpacity { get; set; } = 0.95;
    public int? PetX { get; set; }
    public int? PetY { get; set; }
    public string PetPersonality { get; set; } = "Professional";
    public bool PetAnimations { get; set; } = true;
    public string PetImagePath { get; set; } = "";
    public List<string> DashboardWidgets { get; set; } = new() { "Core", "System", "Storage", "Network", "Clock", "Suggestions" };
    public Dictionary<string, bool> WideWidgets { get; set; } = new();
    public bool WeatherEnabled { get; set; }
    public bool NewsEnabled { get; set; }
    public string WeatherCity { get; set; } = "Sydney";
    public string DisplayName { get; set; } = "";
    public string QuickShortcut { get; set; } = "Alt+Space";
    public string DeckShortcut { get; set; } = "Alt+J";
    public string ResponseMode { get; set; } = "Quick";
    public Dictionary<string, string> ChatModes { get; set; } = new();
    public double Temperature { get; set; } = 0.6;
    public ProviderProfile ImageProvider { get; set; } = new();
    public Dictionary<string, string> Drafts { get; set; } = new();
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
    public void Normalize()
    {
        var defaults = new AppConfig();
        Providers ??= new();
        foreach (var (name, profile) in defaults.Providers)
        {
            if (!Providers.TryGetValue(name, out var stored) || stored is null) Providers[name] = profile;
            else { stored.BaseUrl ??= profile.BaseUrl; stored.ApiKey ??= ""; stored.Model ??= ""; }
        }
        if (ActiveProvider != "Local" && !defaults.Providers.ContainsKey(ActiveProvider ?? "")) ActiveProvider = "Local";
        ChatModes ??= new();
        DashboardWidgets ??= new() { "Core", "System", "Storage", "Network", "Clock", "Suggestions" }; WideWidgets ??= new();
        try { GlobalHotkey.Parse(QuickShortcut); GlobalHotkey.Parse(DeckShortcut); } catch { QuickShortcut = "Alt+Space"; DeckShortcut = "Alt+J"; }
        DisabledSkills ??= new(); Drafts ??= new(); McpServers ??= new();
        ImageProvider ??= new(); ImageProvider.BaseUrl ??= ""; ImageProvider.ApiKey ??= ""; ImageProvider.Model ??= "";
        FavoriteAppPaths ??= new(); RecentAppPaths ??= new(); ShortcutAppPaths ??= new(); WorkRoutineAppPaths ??= new();
        UserProfile ??= ""; PreferredLocalModel ??= "";
        if (string.IsNullOrWhiteSpace(FontFamily)) FontFamily = FontCatalog.Inter;
        McpServers = McpServers.Where(server => server is not null).ToList();
        ResponseTokens = Math.Clamp(ResponseTokens, 128, 4096);
        Temperature = double.IsFinite(Temperature) ? Math.Clamp(Temperature, 0, 1.5) : .6;
        WorkFocusMinutes = Math.Clamp(WorkFocusMinutes, 1, 240);
        if (string.IsNullOrWhiteSpace(Workspace)) Workspace = defaults.Workspace;
        if (!Avalonia.Media.Color.TryParse(Accent, out _)) Accent = defaults.Accent;
        if (!Avalonia.Media.Color.TryParse(Tint, out _)) Tint = defaults.Tint;
    }
}
public sealed class SecureConfigStore
{
    public string? RecoveryNotice { get; private set; }
    private readonly string _directory;
    public SecureConfigStore(string? directory = null) => _directory = directory ?? AppPaths.DataDirectory;
    public AppConfig Load()
    {
        var path = Path.Combine(_directory, "config.encrypted.json");
        var result = AtomicStorage.Read(path, Decode, () => new AppConfig(), out var recovery);
        result.Normalize();
        RecoveryNotice = recovery;
        return result;
    }
    private AppConfig Decode(string text)
    {
        var envelope = JsonSerializer.Deserialize<Dictionary<string, string>>(text) ?? throw new InvalidDataException("Empty settings.");
        if (!envelope.TryGetValue("payload", out var payload) || string.IsNullOrWhiteSpace(payload)) throw new InvalidDataException("Settings payload is missing.");
        var bytes = Convert.FromBase64String(payload);
        byte[] plain;
        if (OperatingSystem.IsWindows()) plain = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
        else
        {
            if (bytes.Length < 28) throw new InvalidDataException("Settings payload is incomplete.");
            plain = new byte[bytes.Length - 28];
            using var aes = new AesGcm(Key(), 16);
            aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(28), bytes.AsSpan(12, 16), plain);
        }
        return JsonSerializer.Deserialize<AppConfig>(plain) ?? throw new InvalidDataException("Settings content is empty.");
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
    public void ForgetPreviousVersion()
    {
        var path = Path.Combine(_directory, "config.encrypted.json");
        AtomicStorage.PurgeHistory(path);
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
        AtomicStorage.Write(path, JsonSerializer.Serialize(new { payload = Convert.ToBase64String(encrypted) }));
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
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Provider returned HTTP {(int)response.StatusCode}. Check the endpoint, key, and model.");
        using var document = JsonDocument.Parse(await HttpPayload.ReadTextAsync(response, 4_000_000, token));
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
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Gemini returned HTTP {(int)response.StatusCode}. Check credentials and model.");
        using var doc = JsonDocument.Parse(await HttpPayload.ReadTextAsync(response, 4_000_000, token));
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
