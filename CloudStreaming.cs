using System.Text;
using System.Text.Json;

namespace JARVIS;

internal static class CloudStreaming
{
    private const int MaximumCharacters = 4_000_000;

    public static async Task<bool> UnsupportedAsync(HttpResponseMessage response, CancellationToken token)
    {
        if ((int)response.StatusCode is not (400 or 422 or 501)) return false;
        try
        {
            var text = await HttpPayload.ReadTextAsync(response, 8192, token);
            return text.Contains("stream", StringComparison.OrdinalIgnoreCase) &&
                (text.Contains("not support", StringComparison.OrdinalIgnoreCase) || text.Contains("unsupported", StringComparison.OrdinalIgnoreCase) ||
                 text.Contains("not available", StringComparison.OrdinalIgnoreCase) || text.Contains("not implemented", StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidDataException) { return false; }
    }

    public static string Extract(JsonElement root, bool gemini, bool delta)
    {
        if (root.ValueKind != JsonValueKind.Object) return "";
        if (gemini)
        {
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0) return "";
            if (candidates[0].ValueKind != JsonValueKind.Object || !candidates[0].TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object || !content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array) return "";
            return string.Concat(parts.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.Object && p.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String).Select(p => p.GetProperty("text").GetString()));
        }
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0) return "";
        if (choices[0].ValueKind != JsonValueKind.Object || !choices[0].TryGetProperty(delta ? "delta" : "message", out var message) || message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("content", out var value)) return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    }

    public static async Task<string> ReadAsync(HttpResponseMessage response, bool gemini, Action<string> progress, CancellationToken token)
    {
        if (response.Content.Headers.ContentType?.MediaType?.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase) != true)
        {
            using var json = JsonDocument.Parse(await HttpPayload.ReadTextAsync(response, MaximumCharacters, token));
            return Extract(json.RootElement, gemini, false);
        }
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(token), Encoding.UTF8);
        var buffer = new char[4096]; var line = new StringBuilder(); var eventData = new StringBuilder(); var result = new StringBuilder();
        var total = 0; var done = false; var finished = false;
        void Event()
        {
            if (eventData.Length == 0) return;
            var payload = eventData.ToString(); eventData.Clear();
            if (payload.Trim() == "[DONE]") { done = true; finished = true; return; }
            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("The provider returned an invalid streaming event.");
            if (json.RootElement.TryGetProperty("error", out _)) throw new InvalidOperationException("The provider reported an error while streaming the response.");
            var collectionName = gemini ? "candidates" : "choices";
            var finishName = gemini ? "finishReason" : "finish_reason";
            if (json.RootElement.TryGetProperty(collectionName, out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0 &&
                choices[0].ValueKind == JsonValueKind.Object && choices[0].TryGetProperty(finishName, out var finish) && finish.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(finish.GetString())) finished = true;
            var part = Extract(json.RootElement, gemini, !gemini);
            if (part.Length == 0) return;
            result.Append(part); progress(part);
        }
        void Line()
        {
            var value = line.ToString().TrimEnd('\r'); line.Clear();
            if (value.Length == 0) { Event(); return; }
            if (!value.StartsWith("data:", StringComparison.Ordinal)) return;
            if (eventData.Length > 0) eventData.Append('\n');
            eventData.Append(value.AsSpan(5).TrimStart());
            if (eventData.Length > 512_000) throw new InvalidDataException("Provider stream event exceeded the size limit.");
        }
        while (!done)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), token);
            if (count == 0) break;
            total += count; if (total > MaximumCharacters) throw new InvalidDataException("Provider response exceeded the size limit.");
            for (var i = 0; i < count && !done; i++)
            {
                if (buffer[i] == '\n') Line();
                else { line.Append(buffer[i]); if (line.Length > 512_000) throw new InvalidDataException("Provider stream line exceeded the size limit."); }
            }
        }
        if (!done) { if (line.Length > 0) Line(); Event(); }
        if (!finished) throw new IOException("The provider stream ended before the response was complete. Try again.");
        return result.ToString();
    }
}
