using System.Net;

namespace JARVIS;

public sealed record ModelDownloadChoice(string Name, string FileName, string Source, string Note)
{
    public override string ToString() => Name + " · " + Note;
}

public sealed record DownloadProgress(long Received, long? Total)
{
    public int Percent => Total is > 0 ? (int)Math.Clamp(Received * 100 / Total.Value, 0, 100) : 0;
    public string Label => Total is > 0
        ? $"{Received / 1_000_000.0:0.0} / {Total.Value / 1_000_000.0:0.0} MB ({Percent}%)"
        : $"{Received / 1_000_000.0:0.0} MB downloaded";
}

public sealed class ModelDownloadService
{
    private const long MaximumBytes = 16L * 1024 * 1024 * 1024;
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public static IReadOnlyList<ModelDownloadChoice> Curated { get; } =
    [
        new("Qwen 2.5 1.5B Instruct", "Qwen2.5-1.5B-Instruct-Q4_K_M.gguf",
            "https://huggingface.co/bartowski/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/Qwen2.5-1.5B-Instruct-Q4_K_M.gguf",
            "Q4_K_M · about 1 GB · general chat"),
        new("Llama 3.2 1B Instruct", "Llama-3.2-1B-Instruct-Q4_K_M.gguf",
            "https://huggingface.co/bartowski/Llama-3.2-1B-Instruct-GGUF/resolve/main/Llama-3.2-1B-Instruct-Q4_K_M.gguf",
            "Q4_K_M · about 0.8 GB · compact")
    ];

    public async Task<string> DownloadAsync(string sourceUrl, string modelsDirectory,
        IProgress<DownloadProgress>? progress = null, CancellationToken token = default)
    {
        var uri = ValidateUrl(sourceUrl);
        var name = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
        if (name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !name.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) || name.Length > 180)
            throw new InvalidDataException("Use a direct URL to a single .gguf file.");
        Directory.CreateDirectory(modelsDirectory);
        var destination = Path.Combine(modelsDirectory, name);
        if (File.Exists(destination)) throw new IOException($"{name} is already in the models folder.");
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".download";
        try
        {
            using var response = await GetWithRedirectsAsync(uri, token);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            if (total > MaximumBytes) throw new InvalidDataException("This model exceeds the 16 GB download limit.");
            await using var input = await response.Content.ReadAsStreamAsync(token);
            await using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[128 * 1024];
            long received = 0;
            while (true)
            {
                var count = await input.ReadAsync(buffer, token);
                if (count == 0) break;
                received += count;
                if (received > MaximumBytes) throw new InvalidDataException("This model exceeds the 16 GB download limit.");
                await output.WriteAsync(buffer.AsMemory(0, count), token);
                progress?.Report(new DownloadProgress(received, total));
            }
            await output.FlushAsync(token);
            if (received < 1024 || (total.HasValue && received != total.Value))
                throw new InvalidDataException("The model download is incomplete.");
            output.Close();
            await using (var check = File.OpenRead(temporary))
            {
                var header = new byte[4];
                if (await check.ReadAsync(header, token) != 4 || !header.AsSpan().SequenceEqual("GGUF"u8))
                    throw new InvalidDataException("The downloaded file is not a GGUF model. Check the URL and model access.");
            }
            File.Move(temporary, destination);
            return destination;
        }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } }
    }

    private static Uri ValidateUrl(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
            !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Use an HTTPS GGUF URL, or HTTP on localhost.");
        return uri;
    }

    private static async Task<HttpResponseMessage> GetWithRedirectsAsync(Uri uri, CancellationToken token)
    {
        for (var i = 0; i < 6; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or
                HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                var next = response.Headers.Location;
                response.Dispose();
                if (next is null) throw new HttpRequestException("The model source returned a redirect without a destination.");
                uri = ValidateUrl(new Uri(uri, next).AbsoluteUri);
                continue;
            }
            return response;
        }
        throw new HttpRequestException("Too many model download redirects.");
    }
}
