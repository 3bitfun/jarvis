using System.Security.Cryptography;
using System.Text.Json;

namespace JARVIS;

internal static class AtomicStorage
{
    public static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.Write(text); writer.Flush(); stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static T Read<T>(string path, Func<string, T> parse, Func<T> empty, out string? recovery)
    {
        recovery = null;
        if (!File.Exists(path))
        {
            if (File.Exists(path + ".bak"))
            {
                try { var recovered = parse(File.ReadAllText(path + ".bak")); File.Copy(path + ".bak", path); recovery = "Restored missing data from its backup."; return recovered; }
                catch (Exception ex) when (IsCorrupt(ex)) { }
            }
            return empty();
        }
        try { return parse(File.ReadAllText(path)); }
        catch (Exception ex) when (IsCorrupt(ex))
        {
            File.Move(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
            if (File.Exists(path + ".bak"))
            {
                try { var restored = parse(File.ReadAllText(path + ".bak")); File.Copy(path + ".bak", path); recovery = "Recovered saved data from backup. The damaged file was kept for recovery."; return restored; }
                catch (Exception backupError) when (IsCorrupt(backupError)) { }
            }
            recovery = "A damaged data file was preserved. Started with an empty copy because no usable backup was available.";
            return empty();
        }
    }
    private static bool IsCorrupt(Exception ex) => ex is JsonException or FormatException or CryptographicException or InvalidDataException or KeyNotFoundException;
    public static void PurgeHistory(string path)
    {
        if (File.Exists(path)) File.Copy(path, path + ".bak", true);
        foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".corrupt-*")) File.Delete(file);
    }
}
