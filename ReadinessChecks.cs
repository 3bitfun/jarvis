using System.Net;
using System.Net.Sockets;
using System.Text;

namespace JARVIS;

internal static class ReadinessChecks
{
    public static async Task RunAsync()
    {
        CheckInstance();
        var root = Path.Combine(Path.GetTempPath(), "JARVIS-readiness-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var memory = new ConversationMemory(Path.Combine(root, "memory"));
        memory.Add("first", "reply"); memory.Add("second", "reply");
        File.WriteAllText(memory.FilePath, "{damaged");
        var recovered = new ConversationMemory(Path.Combine(root, "memory"));
        Require(recovered.RecoveryNotice is not null && recovered.Turns.Count == 1, "Chat recovery from rolling backup failed");
        recovered.Add("session private", "private reply", persist: false);
        Require(!File.ReadAllText(recovered.FilePath).Contains("session private"), "Paused-memory message was persisted");
        Require(recovered.SessionTurns.Count == 1, "Private-session context missing");
        recovered.ClearAllDetails();
        Require(!File.ReadAllText(recovered.FilePath + ".bak").Contains("\"User\": \"first\""), "Cleared messages remained in backup");
        Require(!Directory.EnumerateFiles(Path.GetDirectoryName(recovered.FilePath)!, "*.corrupt-*").Any(), "Cleared messages remained in recovery snapshots");
        recovered.Add("delete me", "secret"); var oldId = recovered.ActiveId;
        recovered.DeleteChat(oldId);
        Require(recovered.Chats.Count == 1 && recovered.ActiveId != oldId && !File.ReadAllText(recovered.FilePath + ".bak").Contains("delete me"), "Chat deletion or backup purge failed");
        var configDirectory = Path.Combine(root, "config"); var store = new SecureConfigStore(configDirectory);
        var config = new AppConfig { UserProfile = "original" }; store.Save(config); config.UserProfile = "updated"; store.Save(config);
        File.WriteAllText(Path.Combine(configDirectory, "config.encrypted.json"), "bad encrypted envelope");
        Require(store.Load().UserProfile == "original" && store.RecoveryNotice is not null, "Encrypted config recovery failed");
        var broken = new AppConfig { Providers = null!, ActiveProvider = "missing", Drafts = null!, ResponseTokens = -1, Temperature = double.NaN, Accent = "bad" };
        broken.Normalize(); Require(broken.Providers.Count == 5 && broken.ActiveProvider == "Local" && broken.ResponseTokens == 128 && double.IsFinite(broken.Temperature), "Settings normalization failed");
        var skillDirectory = Path.Combine(root, "skills"); Directory.CreateDirectory(Path.Combine(skillDirectory, "large")); Directory.CreateDirectory(Path.Combine(skillDirectory, "small"));
        File.WriteAllText(Path.Combine(skillDirectory, "large", "SKILL.md"), "name: A large\n" + new string('x', 10000));
        File.WriteAllText(Path.Combine(skillDirectory, "small", "SKILL.md"), "name: B small\nUseful short guidance.");
        var skills = new SkillsLibrary(skillDirectory);
        Require(skills.BuildPrompt(500).Contains("Useful short guidance"), "Oversized skill hid usable later skills");
        foreach (var skill in skills.Skills) skill.Enabled = false;
        Require(skills.BuildPrompt() == "", "Disabled skills still added prompt content");
        using var oversized = new HttpResponseMessage { Content = new StringContent(new string('x', 200)) };
        try { await HttpPayload.ReadTextAsync(oversized, 100, CancellationToken.None); throw new Exception("Oversized HTTP payload accepted"); }
        catch (InvalidDataException) { }
        await CheckModelFetchAsync();
        Console.WriteLine("Passed: single-instance activation, chat/settings recovery, private sessions, deletion and backup purge, settings normalization, skill budgeting, HTTP response bounds, and model lists for all five providers including Gemini pagination and auth errors.");
    }
    private static void CheckInstance()
    {
        using var first = new DesktopInstance();
        if (!first.IsPrimary) throw new InvalidOperationException("Close J.A.R.V.I.S. before running the single-instance check.");
        using var activated = new ManualResetEventSlim(); first.Attach(activated.Set);
        using var second = new DesktopInstance(); Require(!second.IsPrimary, "Second instance acquired primary ownership");
        second.ActivateExistingAsync().GetAwaiter().GetResult();
        Require(activated.Wait(TimeSpan.FromSeconds(3)), "Second launch did not activate the first instance");
    }
    private static async Task CheckModelFetchAsync()
    {
        var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start(); var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://localhost:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var server = Task.Run(async () =>
        {
            for (var i = 0; i < 7; i++)
            {
                var context = await listener.GetContextAsync().WaitAsync(timeout.Token); var request = context.Request;
                string json;
                if (request.RawUrl!.Contains("unauthorized")) { context.Response.StatusCode = 401; json = "{}"; }
                else if (request.RawUrl.StartsWith("/gemini/"))
                {
                    Require(request.Headers["x-goog-api-key"] == "fake-key", "Gemini key header missing");
                    json = request.QueryString["pageToken"] is null
                        ? "{\"models\":[{\"name\":\"models/chat-one\",\"supportedGenerationMethods\":[\"generateContent\"]},{\"name\":\"models/embedding\",\"supportedGenerationMethods\":[\"embedContent\"]}],\"nextPageToken\":\"next page\"}"
                        : "{\"models\":[{\"name\":\"models/chat-two\",\"supportedGenerationMethods\":[\"generateContent\"]}]}";
                }
                else
                {
                    Require(request.Url!.AbsolutePath.EndsWith("/models") && request.Headers["Authorization"] == "Bearer fake-key", "OpenAI-compatible model request invalid");
                    json = "{\"data\":[{\"id\":\"zeta\"},{\"id\":\"alpha\"},{\"id\":\"alpha\"}]}";
                }
                var bytes = Encoding.UTF8.GetBytes(json); context.Response.ContentType = "application/json"; context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close();
            }
        }, timeout.Token);
        foreach (var provider in new[] { "Nvidia NIM", "Groq Cloud", "OpenRouter", "Custom OpenAI" })
        {
            var ids = await ProviderModels.FetchAsync(provider, $"http://localhost:{port}/v1/chat/completions", "fake-key", timeout.Token);
            Require(ids.SequenceEqual(new[] { "alpha", "zeta" }), provider + " model normalization failed");
        }
        var gemini = await ProviderModels.FetchAsync("Google Gemini", $"http://localhost:{port}/gemini", "fake-key", timeout.Token);
        Require(gemini.SequenceEqual(new[] { "chat-one", "chat-two" }), "Gemini pagination or filtering failed");
        try { await ProviderModels.FetchAsync("Custom OpenAI", $"http://localhost:{port}/unauthorized", "bad-key", timeout.Token); throw new Exception("Unauthorized model fetch accepted"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("401")) { }
        await server.WaitAsync(timeout.Token);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
