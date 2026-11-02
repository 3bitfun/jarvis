using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace JARVIS;

internal static class ImageChecks
{
    public static async Task RunAsync(string root)
    {
        var archivePath = Path.Combine(root, "image-runtime-check.zip"); var stage = Path.Combine(root, "image-runtime-check"); Directory.CreateDirectory(stage);
        using (var archive = System.IO.Compression.ZipFile.Open(archivePath, System.IO.Compression.ZipArchiveMode.Create))
        { var safe = archive.CreateEntry("sd-cli.exe"); using var writer = new StreamWriter(safe.Open()); writer.Write("test"); }
        LocalImageService.ExtractRuntime(archivePath, stage, CancellationToken.None);
        if (!File.Exists(Path.Combine(stage, "sd-cli.exe"))) throw new Exception("Local runtime extraction failed.");
        var badZip = Path.Combine(root, "image-runtime-bad.zip");
        using (var archive = System.IO.Compression.ZipFile.Open(badZip, System.IO.Compression.ZipArchiveMode.Create))
        { var bad = archive.CreateEntry("../escape.exe"); using var writer = new StreamWriter(bad.Open()); writer.Write("test"); }
        try { LocalImageService.ExtractRuntime(badZip, stage, CancellationToken.None); throw new Exception("Unsafe runtime archive was accepted."); }
        catch (InvalidDataException) { }
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://localhost:{port}/"); listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var server = Task.Run(async () =>
        {
            for (var i = 0; i < 4; i++)
            {
                var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
                string json;
                if (i == 0)
                {
                    if (context.Request.Url!.AbsolutePath != "/api/v1/images/models") throw new Exception("OpenRouter image model endpoint is incorrect.");
                    json = "{\"data\":[{\"id\":\"vendor/image-model\"}]}";
                }
                else
                {
                    using var doc = JsonDocument.Parse(await new StreamReader(context.Request.InputStream).ReadToEndAsync());
                    if (i is 1 or 3)
                    {
                        if (context.Request.Url!.AbsolutePath != "/api/v1/images" || context.Request.Headers["Authorization"] != "Bearer test-key" || doc.RootElement.GetProperty("model").GetString() != "vendor/image-model") throw new Exception("OpenRouter image request did not reuse the chat endpoint, key and image model.");
                        json = JsonSerializer.Serialize(new { data = new[] { new { b64_json = Convert.ToBase64String(png) } } });
                    }
                    else
                    {
                        if (context.Request.Url!.AbsolutePath != "/v1beta/models/gemini-image:generateContent" || context.Request.Headers["x-goog-api-key"] != "test-key") throw new Exception("Gemini image request was incorrect.");
                        json = JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { inlineData = new { mimeType = "image/png", data = Convert.ToBase64String(png) } } } } } } });
                    }
                }
                var bytes = Encoding.UTF8.GetBytes(json); context.Response.ContentType = "application/json"; context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, timeout.Token); context.Response.Close();
            }
        }, timeout.Token);
        var openRouter = new ProviderProfile { BaseUrl = $"http://localhost:{port}/api/v1/chat/completions", ApiKey = "test-key", Model = "vendor/image-model" };
        var ids = await ProviderModels.FetchImageModelsAsync("OpenRouter", openRouter.BaseUrl, openRouter.ApiKey, timeout.Token);
        if (!ids.SequenceEqual(new[] { "vendor/image-model" })) throw new Exception("Image model discovery failed.");
        var encoded = "```jarvis-image\n{\"tool\":\"generate_image\",\"prompt\":\"a cat\"}\n```";
        var workspace = Path.Combine(root, "image-checks");
        var generated = await ImageToolParser.ProcessAsync(encoded, openRouter, workspace, timeout.Token, "OpenRouter");
        if (generated.Paths.Count != 1 || !File.ReadAllBytes(generated.Paths[0]).SequenceEqual(png)) throw new Exception("OpenRouter image did not save.");
        var gemini = new ProviderProfile { BaseUrl = $"http://localhost:{port}/v1beta", ApiKey = "test-key", Model = "models/gemini-image" };
        var geminiResult = await ImageToolParser.ProcessAsync(encoded, gemini, workspace, timeout.Token, "Google Gemini");
        if (geminiResult.Paths.Count != 1 || !File.ReadAllBytes(geminiResult.Paths[0]).SequenceEqual(png)) throw new Exception("Gemini image did not save.");
        using var engine = new LLamaEngine(Path.Combine(root, "image-chat"));
        var config = new AppConfig { ActiveProvider = "OpenRouter", MemoryEnabled = false, Workspace = workspace, ImageBackend = "Chat provider", ImageChatModel = "vendor/image-model" };
        config.Providers["OpenRouter"] = new ProviderProfile { BaseUrl = openRouter.BaseUrl, ApiKey = openRouter.ApiKey, Model = "vendor/chat-model" }; engine.Config = config;
        await engine.GenerateImageAsync("a cat", timeout.Token);
        if (engine.Conversation.Count != 1 || engine.Conversation[0].Images?.Count != 1 || !File.Exists(engine.Conversation[0].Images![0])) throw new Exception("Image was not added to the current chat.");
        var localPath = Path.Combine(workspace, "local-test.png");
        var local = await ImageToolParser.ProcessAsync(encoded, new(), workspace, timeout.Token, localGenerator: async (_, _, token) => { await File.WriteAllBytesAsync(localPath, png, token); return localPath; });
        if (local.Paths.Single() != localPath) throw new Exception("Local image routing failed.");
        await server.WaitAsync(timeout.Token);
        Console.WriteLine("Passed image model discovery, provider routing, inline chat persistence, and local backend routing.");
    }
}
