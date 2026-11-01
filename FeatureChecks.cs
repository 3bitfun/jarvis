using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
namespace JARVIS;
internal static class FeatureChecks
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "JARVIS-features-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var config = new AppConfig { Workspace = Path.Combine(root, "workspace"), FileTools = true };
        string Action(object action) => WorkspaceFiles.Execute("```jarvis-file\n" + JsonSerializer.Serialize(action) + "\n```", config);
        Action(new { path = "demo.txt", operation = "write", content = "hello" });
        Action(new { path = "demo.txt", operation = "replace", oldText = "hello", content = "world" });
        if (File.ReadAllText(Path.Combine(config.Workspace, "demo.txt")) != "world") throw new Exception("File edit failed");
        var rejection = Action(new { path = "../escape.txt", operation = "write", content = "bad" });
        if (!rejection.Contains("rejected") || File.Exists(Path.Combine(root, "escape.txt"))) throw new Exception("Sandbox escape");
        var store = new SecureConfigStore(Path.Combine(root, "config"));
        if (!config.DeliberateMode) throw new Exception("Deliberate mode must default on");
        config.DeliberateMode = false;
        config.FontFamily = "Arial";
        config.Providers["Groq Cloud"].ApiKey = "test-secret-do-not-use";
        store.Save(config);
        var restored = store.Load();
        if (restored.DeliberateMode || restored.FontFamily != "Arial") throw new Exception("Preferences did not persist");
        if (File.ReadAllText(Path.Combine(root, "config", "config.encrypted.json")).Contains("test-secret") || store.Load().Providers["Groq Cloud"].ApiKey != "test-secret-do-not-use") throw new Exception("Encrypted config roundtrip failed");
        var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://localhost:{port}/"); listener.Start();
        var server = Task.Run(async () =>
        {
            for (var i = 0; i < 5; i++)
            {
                var context = await listener.GetContextAsync();
                using var doc = JsonDocument.Parse(await new StreamReader(context.Request.InputStream).ReadToEndAsync());
                var gemini = context.Request.RawUrl!.Contains(":generateContent");
                if (gemini ? !doc.RootElement.TryGetProperty("contents", out _) : !doc.RootElement.TryGetProperty("messages", out _)) throw new Exception("Invalid provider payload");
                var bytes = Encoding.UTF8.GetBytes(gemini ? "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"ok\"}]}}]}" : "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}");
                context.Response.ContentType = "application/json"; context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes); context.Response.Close();
            }
        });
        foreach (ICloudAdapter adapter in new ICloudAdapter[] { new NvidiaNimAdapter(), new GroqAdapter(), new OpenRouterAdapter(), new GeminiAdapter(), new OpenAiAdapter() })
        {
            var answer = await adapter.CompleteAsync(new() { BaseUrl = $"http://localhost:{port}/v1", ApiKey = "test", Model = "test" }, "system", "hello", 16, CancellationToken.None);
            if (answer != "ok") throw new Exception("Provider parse failed");
        }
        await server.WaitAsync(TimeSpan.FromSeconds(10));
        Console.WriteLine("Passed: encrypted settings, workspace write/replace/traversal rejection, and five provider adapter payload/response checks against a local mock.");
    }
}
