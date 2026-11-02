using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace JARVIS;

/// <summary>Opt-in, checksum-pinned CPU runtime and SD 1.5 checkpoint.</summary>
public static class LocalImageService
{
    private const string RuntimeUrl = "https://github.com/leejet/stable-diffusion.cpp/releases/download/master-1039-290148d/sd-master-290148d-bin-win-cpu-x64.zip";
    private const string RuntimeHash = "DB87261C0FF56FF5F24D83B2C5926F3792192E08FDC9E32ED36AF628E35D519E";
    private const string ModelUrl = "https://huggingface.co/sd-research/stable-diffusion-v1-5/resolve/88cd7be8ec302193dd6294d4ede4b28ff831fa24/v1-5-pruned-emaonly.safetensors";
    private const string ModelHash = "6CE0161689B3853ACAA03779EC93EAFE75A02F4CED659BEE03F50797806FA2FA";
    private const long ModelBytes = 4265146304;
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
    private static string Root => Path.Combine(AppPaths.DataDirectory, "image-models");
    public static string RuntimePath => Path.Combine(Root, "sd-cpp-290148d", "sd-cli.exe");
    public static string ModelPath => Path.Combine(Root, "sd15-v1-5-pruned-emaonly.safetensors");
    public static bool Supported => OperatingSystem.IsWindows() && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64;
    public static bool IsInstalled => Supported && File.Exists(RuntimePath) && File.Exists(ModelPath) && new FileInfo(ModelPath).Length == ModelBytes;

    public static async Task InstallAsync(IProgress<string>? progress, CancellationToken token)
    {
        if (!Supported)
            throw new PlatformNotSupportedException("Automatic local image setup currently supports Windows x64. Other platforms can connect a local OpenAI-compatible image server.");
        Directory.CreateDirectory(Root);
        if (!File.Exists(ModelPath))
        {
            try { var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(Root))!); if (drive.IsReady && drive.AvailableFreeSpace < ModelBytes + 250_000_000) throw new IOException("Not enough free space for the 4.27 GB image model and runtime."); }
            catch (ArgumentException) { /* Free-space information is unavailable on this filesystem. */ }
        }
        var runtimeDirectory = Path.GetDirectoryName(RuntimePath)!;
        if (!File.Exists(RuntimePath))
        {
            if (Directory.Exists(runtimeDirectory)) throw new IOException("The local image runtime folder is incomplete. Remove that folder or choose a local image server.");
            var zip = Path.Combine(Root, Guid.NewGuid().ToString("N") + ".zip.download");
            var stage = Path.Combine(Root, "runtime-staging-" + Guid.NewGuid().ToString("N"));
            try
            {
                await DownloadAsync(RuntimeUrl, zip, RuntimeHash, 18067882, "Image runtime", progress, token);
                Directory.CreateDirectory(stage);
                ExtractRuntime(zip, stage, token);
                if (!File.Exists(Path.Combine(stage, "sd-cli.exe"))) throw new InvalidDataException("Image runtime is missing sd-cli.exe.");
                Directory.Move(stage, runtimeDirectory);
            }
            finally
            {
                if (File.Exists(zip)) File.Delete(zip);
                if (Directory.Exists(stage))
                {
                    var parent = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (!Path.GetFullPath(stage).StartsWith(parent, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new IOException("Invalid runtime staging path.");
                    Directory.Delete(stage, true);
                }
            }
        }
        if (File.Exists(ModelPath))
        {
            progress?.Report("Checking existing local image model…");
            await using var existing = File.OpenRead(ModelPath);
            if (existing.Length != ModelBytes || !Convert.ToHexString(await SHA256.HashDataAsync(existing, token)).Equals(ModelHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The existing image model failed verification. Move or remove it before reinstalling.");
            return;
        }
        var temporary = Path.Combine(Root, Guid.NewGuid().ToString("N") + ".model.download");
        try { await DownloadAsync(ModelUrl, temporary, ModelHash, ModelBytes, "Image model", progress, token); File.Move(temporary, ModelPath); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    internal static void ExtractRuntime(string zip, string stage, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(zip); long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            if (entry.FullName.EndsWith('/')) continue;
            if (entry.FullName != Path.GetFileName(entry.FullName) || entry.Length < 0 || (expanded += entry.Length) > 200_000_000) throw new InvalidDataException("Invalid image runtime archive.");
            if (!entry.FullName.Equals("sd-cli.exe", StringComparison.OrdinalIgnoreCase) && !entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && !entry.FullName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) continue;
            entry.ExtractToFile(Path.Combine(stage, entry.FullName));
        }
    }

    private static async Task DownloadAsync(string source, string destination, string hash, long expected, string label, IProgress<string>? progress, CancellationToken token)
    {
        var uri = new Uri(source);
        HttpResponseMessage? response = null;
        try
        {
            for (var i = 0; i < 8; i++)
            {
                if (uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)) throw new IOException("Image download redirected to an invalid URL.");
                response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location) { uri = location.IsAbsoluteUri ? location : new Uri(uri, location); response.Dispose(); response = null; continue; }
                break;
            }
            if (response is null) throw new IOException("Too many image download redirects.");
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is { } length && length != expected) throw new InvalidDataException("Download size did not match the published file.");
            await using var input = await response.Content.ReadAsStreamAsync(token);
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[131072]; long total = 0; var last = Stopwatch.StartNew();
            while (true)
            {
                var count = await input.ReadAsync(buffer, token); if (count == 0) break;
                total += count; if (total > expected) throw new InvalidDataException("Download exceeded the expected size.");
                digest.AppendData(buffer, 0, count); await output.WriteAsync(buffer.AsMemory(0, count), token);
                if (last.ElapsedMilliseconds >= 300) { progress?.Report($"{label}: {total / 1_000_000.0:0} / {expected / 1_000_000.0:0} MB"); last.Restart(); }
            }
            if (total != expected || !Convert.ToHexString(digest.GetHashAndReset()).Equals(hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Image download failed checksum verification.");
        }
        finally { response?.Dispose(); }
    }

    public static async Task<string> GenerateAsync(string prompt, string workspace, CancellationToken token)
    {
        if (!IsInstalled) throw new InvalidOperationException("Install the local image runtime and model in Settings → Engine → Image generation first.");
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 2000) throw new InvalidDataException("Image prompts must contain 1–2,000 characters.");
        var directory = Path.Combine(Path.GetFullPath(workspace), "images"); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "jarvis-local-" + Guid.NewGuid().ToString("N") + ".png");
        var start = new ProcessStartInfo(RuntimePath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(RuntimePath)! };
        foreach (var arg in new[] { "-m", ModelPath, "-p", prompt, "-o", path, "-W", "512", "-H", "512" }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Could not start local image generation.");
        var stdout = process.StandardOutput.ReadToEndAsync(token); var stderr = process.StandardError.ReadToEndAsync(token);
        try
        {
            await process.WaitForExitAsync(token); await Task.WhenAll(stdout, stderr);
            if (process.ExitCode != 0 || !File.Exists(path)) throw new InvalidOperationException("Local image generation failed. Check available RAM and the local runtime installation.");
            await using var file = File.OpenRead(path); var signature = new byte[8];
            if (await file.ReadAsync(signature, token) != 8 || !signature.AsSpan().SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw new InvalidDataException("The local runtime did not produce a valid PNG.");
            return path;
        }
        catch
        {
            try { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } } catch (InvalidOperationException) { }
            if (File.Exists(path)) File.Delete(path); throw;
        }
    }
}
