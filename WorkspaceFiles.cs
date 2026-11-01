using System.Text.Json;
using System.Text.RegularExpressions;
namespace JARVIS;
public sealed class WorkspaceFiles
{
    public const string Protocol = "For requested file edits, emit a fenced jarvis-file JSON object with path (relative to workspace), operation (write, append, or replace), content, and oldText (required for replace). Only emit file actions when the user requests edits.";
    public static string Execute(string response, AppConfig config)
    {
        return Regex.Replace(response, @"```jarvis-file\s*([\s\S]*?)```", match =>
        {
            if (!config.FileTools) return "[File action skipped: enable workspace edits in Settings.]";
            try
            {
                using var doc = JsonDocument.Parse(match.Groups[1].Value);
                var action = doc.RootElement;
                var relative = action.GetProperty("path").GetString() ?? "";
                if (Path.IsPathRooted(relative) || relative.Contains(':') || relative.Split('/', '\\').Any(p => p is ".." or "." || p.EndsWith('.') || p.EndsWith(' '))) throw new IOException("Use a relative workspace path without traversal.");
                var root = Path.GetFullPath(config.Workspace).TrimEnd(Path.DirectorySeparatorChar);
                Directory.CreateDirectory(root);
                var path = Path.GetFullPath(Path.Combine(root, relative));
                if (!path.StartsWith(root + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new IOException("Path escapes workspace.");
                for (var part = path; part is not null; part = Path.GetDirectoryName(part))
                    if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new IOException("Symbolic links are not allowed in workspace paths.");
                var content = action.GetProperty("content").GetString() ?? "";
                if (content.Length > 1_000_000 || (File.Exists(path) && new FileInfo(path).Length > 2_000_000)) throw new IOException("File edit exceeds size limit.");
                var original = File.Exists(path) ? File.ReadAllText(path) : "";
                var updated = action.GetProperty("operation").GetString() switch
                {
                    "write" => content,
                    "append" => original + content,
                    "replace" => ReplaceUnique(original, action.GetProperty("oldText").GetString() ?? "", content),
                    _ => throw new IOException("Unknown file operation.")
                };
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temporary = path + ".jarvis-" + Guid.NewGuid().ToString("N");
                if (File.Exists(path)) File.Copy(path, temporary + ".backup");
                File.WriteAllText(temporary, updated);
                File.Move(temporary, path, true);
                return $"✓ Saved `{relative}`";
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException or ArgumentException)
            { return "File action rejected: " + ex.Message; }
        }, RegexOptions.None, TimeSpan.FromSeconds(2));
    }
    private static string ReplaceUnique(string source, string old, string content)
    {
        var index = source.IndexOf(old, StringComparison.Ordinal);
        if (old.Length == 0 || index < 0 || source.IndexOf(old, index + old.Length, StringComparison.Ordinal) >= 0) throw new IOException("Replacement must match exactly one block.");
        return source[..index] + content + source[(index + old.Length)..];
    }
}
