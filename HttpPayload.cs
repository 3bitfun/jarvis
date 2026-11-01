using System.Text;

namespace JARVIS;

internal static class HttpPayload
{
    public static async Task<string> ReadTextAsync(HttpResponseMessage response, int maximumBytes, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > maximumBytes) throw new InvalidDataException("The service response exceeds the supported size limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192]; int count;
        while ((count = await stream.ReadAsync(chunk, token)) != 0)
        {
            if (buffer.Length + count > maximumBytes) throw new InvalidDataException("The service response exceeds the supported size limit.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), token);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
