using System.Text.RegularExpressions;

namespace JARVIS;

public sealed class LocalSkill
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Path { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

/// <summary>Loads user-authored SKILL.md files separately from MCP tool servers.</summary>
public sealed class SkillsLibrary
{
    private readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JARVIS", "skills");
    public List<LocalSkill> Skills { get; } = new();
    public SkillsLibrary() => Refresh();
    public void Refresh()
    {
        Directory.CreateDirectory(_root); Skills.Clear();
        foreach (var path in Directory.EnumerateFiles(_root, "SKILL.md", SearchOption.AllDirectories).Take(100))
        {
            try
            {
                var text = File.ReadAllText(path); if (text.Length > 32_000) continue;
                var name = Regex.Match(text, "(?m)^\\s*name:\\s*['\\\"]?([^'\\\"\\r\\n]+)").Groups[1].Value.Trim();
                var description = Regex.Match(text, "(?m)^\\s*description:\\s*['\\\"]?([^'\\\"\\r\\n]+)").Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(name)) name = Directory.GetParent(path)?.Name ?? "Skill";
                Skills.Add(new LocalSkill { Name = name, Description = description, Path = path });
            }
            catch (IOException) { }
        }
        Skills.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }
    public void Import(string sourcePath)
    {
        if (!File.Exists(sourcePath) || !Path.GetFileName(sourcePath).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a file named SKILL.md.");
        var info = new FileInfo(sourcePath);
        if (info.Length is 0 or > 32_000) throw new InvalidDataException("A skill file must be smaller than 32 KB.");
        var folder = Path.Combine(_root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        File.Copy(sourcePath, Path.Combine(folder, "SKILL.md")); Refresh();
    }
    public string BuildPrompt(int maxChars = 3500)
    {
        var output = new System.Text.StringBuilder("Relevant user-installed skills (guidance only; user request and system safety rules take priority):\n");
        foreach (var skill in Skills.Where(s => s.Enabled))
        {
            var text = File.ReadAllText(skill.Path);
            var block = $"\n## {skill.Name}\n{skill.Description}\n{text}\n";
            if (output.Length + block.Length > maxChars) break;
            output.Append(block);
        }
        return output.Length == 0 ? "" : output.ToString();
    }
}
