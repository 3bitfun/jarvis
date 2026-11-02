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
        await CheckConversationAsync(root);
        await CheckStreamingAsync(root);
        await CheckPrivateCompactionAsync(root);
        await CheckProviderEndpointsAsync();
        Console.WriteLine("Passed: encrypted settings, workspace write/replace/traversal rejection, and five provider adapter payload/response checks against a local mock.");
    }

    private static async Task CheckProviderEndpointsAsync()
    {
        var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://localhost:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = Task.Run(async () =>
        {
            for (var i = 0; i < 2; i++)
            {
                var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
                var url = context.Request.Url!;
                if (i == 0 && (url.AbsolutePath != "/v1/chat/completions" || url.Query != "?tenant=example")) throw new Exception("Custom chat endpoint or query was changed");
                if (i == 1 && url.AbsolutePath != "/v1/models/mock:generateContent") throw new Exception("Gemini model prefix was not normalized");
                var json = i == 0 ? "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}" : "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"ok\"}]}}]}";
                var bytes = Encoding.UTF8.GetBytes(json); context.Response.ContentType = "application/json"; context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, timeout.Token); context.Response.Close();
            }
        }, timeout.Token);
        var openAi = new ProviderProfile { BaseUrl = $"http://localhost:{port}/v1/chat/completions?tenant=example", Model = "mock" };
        var gemini = new ProviderProfile { BaseUrl = $"http://localhost:{port}/v1", Model = "models/mock" };
        if (await new OpenAiAdapter().CompleteAsync(openAi, "system", "hi", 64, timeout.Token) != "ok" ||
            await new GeminiAdapter().CompleteAsync(gemini, "system", "hi", 64, timeout.Token) != "ok") throw new Exception("Provider endpoint request failed");
        await server.WaitAsync(timeout.Token);
    }

    private static async Task CheckPrivateCompactionAsync(string root)
    {
        var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://localhost:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var bodies = new List<string>();
        var server = Task.Run(async () =>
        {
            for (var i = 0; i < 5; i++)
            {
                var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
                var body = await new StreamReader(context.Request.InputStream).ReadToEndAsync(); bodies.Add(body);
                var answer = body.Contains("Update a bounded conversation summary") ? "Private summary: favorite color teal." : "Short answer.";
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = answer } } } }));
                context.Response.ContentType = "application/json"; context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, timeout.Token); context.Response.Close();
            }
        }, timeout.Token);
        using var engine = new LLamaEngine(Path.Combine(root, "private-compaction"));
        var config = new AppConfig { ActiveProvider = "Custom OpenAI", MemoryEnabled = false, KnowledgeEnabled = false };
        config.Providers["Custom OpenAI"] = new ProviderProfile { BaseUrl = $"http://localhost:{port}/v1", Model = "mock" }; engine.Config = config;
        for (var i = 0; i < 4; i++) await engine.GenerateAsync($"Private turn {i}: " + new string('x', 10000), timeout.Token);
        await server.WaitAsync(timeout.Token);
        if (engine.Conversation.Count != 4 || !bodies.Any(b => b.Contains("Update a bounded conversation summary")) || !bodies[^1].Contains("Private summary: favorite color teal"))
            throw new Exception("Private conversation did not compact into session context");
        if (File.Exists(engine.MemoryPath) && File.ReadAllText(engine.MemoryPath).Contains("Private")) throw new Exception("Private compaction persisted sensitive content");
    }

    private static async Task CheckStreamingAsync(string root)
    {
        var emitted = new StringBuilder(); var filter = new SafeStreamingText(part => emitted.Append(part));
        foreach (var part in new[] { "Hello ", "`", "`", "`jar", "vis-file\n{secret}", "```" }) filter.Append(part);
        if (emitted.ToString() != "Hello ") throw new Exception("Structured tool content leaked into streamed text");
        emitted.Clear(); filter = new SafeStreamingText(part => emitted.Append(part));
        foreach (var part in new[] { "Try ", "```", "c", "s\nint x;", "```", " done" }) filter.Append(part);
        if (emitted.ToString() != "Try ```cs\nint x;``` done") throw new Exception("Ordinary Markdown streaming was damaged");
        using (var interrupted = new HttpResponseMessage { Content = new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}\n\n") })
        {
            interrupted.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            var partial = new StringBuilder();
            try { await CloudStreaming.ReadAsync(interrupted, false, part => partial.Append(part), CancellationToken.None); throw new Exception("Incomplete stream was accepted"); }
            catch (IOException) { }
            if (partial.ToString() != "partial") throw new Exception("Interrupted stream lost its partial reply");
        }
        var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://localhost:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = Task.Run(async () =>
        {
            for (var index = 0; index < 5; index++)
            {
                var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
                using var request = JsonDocument.Parse(await new StreamReader(context.Request.InputStream).ReadToEndAsync());
                var gemini = context.Request.RawUrl!.Contains(":streamGenerateContent");
                if (index == 0 && (!request.RootElement.GetProperty("stream").GetBoolean() || gemini)) throw new Exception("OpenAI streaming request missing stream flag");
                if (index == 1 && !gemini) throw new Exception("Gemini streaming endpoint missing");
                if (index == 2)
                {
                    context.Response.StatusCode = 400;
                    var rejected = Encoding.UTF8.GetBytes("{\"error\":\"streaming is not supported\"}");
                    context.Response.ContentLength64 = rejected.Length; await context.Response.OutputStream.WriteAsync(rejected, timeout.Token); context.Response.Close(); continue;
                }
                if (index == 3 && request.RootElement.GetProperty("stream").GetBoolean()) throw new Exception("Non-streaming fallback was not used");
                var body = index == 3 ? "{\"choices\":[{\"message\":{\"content\":\"fallback\"}}]}" : gemini
                    ? "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Hello\"}]}}]}\n\ndata: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\" world\"}]},\"finishReason\":\"STOP\"}]}\n\n"
                    : "data: {\"choices\":[{\"delta\":{\"content\":\"Hello\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\" world\"}}]}\n\ndata: [DONE]\n\n";
                var bytes = Encoding.UTF8.GetBytes(body); context.Response.ContentType = index == 3 ? "application/json" : "text/event-stream";
                context.Response.ContentLength64 = bytes.Length;
                // Split the wire stream at arbitrary boundaries, including within JSON and UTF-8 reads.
                for (var offset = 0; offset < bytes.Length; offset += 7) await context.Response.OutputStream.WriteAsync(bytes.AsMemory(offset, Math.Min(7, bytes.Length - offset)), timeout.Token);
                context.Response.Close();
            }
        }, timeout.Token);
        var profile = new ProviderProfile { BaseUrl = $"http://localhost:{port}/v1", ApiKey = "test", Model = "mock" };
        foreach (ICloudAdapter adapter in new ICloudAdapter[] { new OpenAiAdapter(), new GeminiAdapter() })
        {
            var parts = new StringBuilder();
            var answer = await adapter.CompleteChatAsync(profile, "system", Array.Empty<ChatTurn>(), "hi", 64, timeout.Token, part => parts.Append(part));
            if (answer != "Hello world" || parts.ToString() != answer) throw new Exception("Cloud streaming response failed");
        }
        var fallback = await new OpenAiAdapter().CompleteChatAsync(profile, "system", Array.Empty<ChatTurn>(), "hi", 64, timeout.Token, _ => { });
        if (fallback != "fallback") throw new Exception("Cloud non-streaming fallback failed");
        using var engine = new LLamaEngine(Path.Combine(root, "streaming-integration"));
        var config = new AppConfig { ActiveProvider = "Custom OpenAI", MemoryEnabled = false, KnowledgeEnabled = false };
        config.Providers["Custom OpenAI"] = profile; engine.Config = config;
        var streamed = new StringBuilder();
        var actual = await engine.GenerateAsync("stream through the engine", timeout.Token, progress: part => streamed.Append(part));
        if (actual != "Hello world" || streamed.ToString() != actual || engine.Conversation.Count != 1) throw new Exception("Engine streaming or saved response failed");
        await server.WaitAsync(timeout.Token);
    }

    private static async Task CheckConversationAsync(string root)
    {
        var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://localhost:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requests = new List<string>();
        var server = Task.Run(async () =>
        {
            foreach (var reply in new[] { "first answer", "second answer", "", "saved answer", "last answer" })
            {
                var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
                using var reader = new StreamReader(context.Request.InputStream);
                requests.Add(await reader.ReadToEndAsync());
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = reply } } } }));
                context.Response.ContentType = "application/json"; context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, timeout.Token); context.Response.Close();
            }
        }, timeout.Token);
        using var engine = new LLamaEngine(Path.Combine(root, "chat-integration"));
        var config = new AppConfig { ActiveProvider = "Custom OpenAI", MemoryEnabled = false, KnowledgeEnabled = false };
        config.Providers["Custom OpenAI"] = new ProviderProfile { BaseUrl = $"http://localhost:{port}/v1", Model = "mock" };
        engine.Config = config;
        if (await engine.GenerateAsync("hello", timeout.Token) != "first answer") throw new Exception("First chat reply failed");
        if (await engine.GenerateAsync("follow up", timeout.Token) != "second answer") throw new Exception("Follow-up chat reply failed");
        try { await engine.GenerateAsync("empty answer", timeout.Token); throw new Exception("Empty provider reply was accepted"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("empty response")) { }
        using var second = JsonDocument.Parse(requests[1]);
        var messages = second.RootElement.GetProperty("messages");
        if (messages.GetArrayLength() != 4 || messages[1].GetProperty("role").GetString() != "user" || messages[1].GetProperty("content").GetString() != "hello" || messages[2].GetProperty("role").GetString() != "assistant" || messages[2].GetProperty("content").GetString() != "first answer" || messages[3].GetProperty("content").GetString() != "follow up") throw new Exception("Follow-up request omitted structured conversation context");
        if (engine.Conversation.Count != 2 || (File.Exists(engine.MemoryPath) && File.ReadAllText(engine.MemoryPath).Contains("hello"))) throw new Exception("Empty reply or private messages were saved incorrectly");
        using (var stopped = new CancellationTokenSource())
        {
            stopped.Cancel();
            try { await engine.GenerateAsync("cancelled message", stopped.Token); throw new Exception("Cancelled chat succeeded"); }
            catch (OperationCanceledException) { }
        }
        config.MemoryEnabled = true;
        if (await engine.GenerateAsync("attachment excerpt", timeout.Token, displayUserMessage: "visible question [Attached: example.txt]") != "saved answer") throw new Exception("Saved attachment chat failed");
        if (await engine.GenerateAsync("what was in the file?", timeout.Token) != "last answer") throw new Exception("Post-cancellation follow-up failed");
        await server.WaitAsync(timeout.Token);
        using var final = JsonDocument.Parse(requests[4]);
        var finalMessages = final.RootElement.GetProperty("messages");
        if (!finalMessages.EnumerateArray().Any(message => message.GetProperty("role").GetString() == "user" && message.GetProperty("content").GetString() == "attachment excerpt")) throw new Exception("Attachment excerpt was lost from follow-up context");
        var disk = File.ReadAllText(engine.MemoryPath);
        if (disk.Contains("first answer") || disk.Contains("cancelled message") || !disk.Contains("visible question")) throw new Exception("Private or cancelled chat persisted unexpectedly");
        var restored = new ConversationMemory(Path.Combine(root, "chat-integration"));
        if (restored.Turns.Count != 2 || restored.Turns[0].User != "visible question [Attached: example.txt]" || restored.Turns[0].ModelUserContent != "attachment excerpt") throw new Exception("Attachment chat was not restored correctly");
    }
}
