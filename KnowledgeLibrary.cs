using System.Text.RegularExpressions;
namespace JARVIS;
public sealed record KnowledgeHit(string Source, string Text, double Score);
public sealed class KnowledgeLibrary
{
    private readonly List<(string Source, string Text, HashSet<string> Words)> _chunks = new();
    public string DirectoryPath { get; }
    public int DocumentCount { get; private set; }
    private static readonly HashSet<string> StopWords = new("the a an is are was were and or to of in on for with it this that my me you your how what can do please about tell".Split(' '));
    public KnowledgeLibrary(string? directory = null)
    {
        DirectoryPath = directory ?? Path.Combine(AppPaths.DataDirectory, "knowledge");
        Directory.CreateDirectory(DirectoryPath);
        var oldGuide = Path.Combine(DirectoryPath, "jarvis-guide.md");
        if (File.Exists(oldGuide) && File.ReadAllText(oldGuide).Contains("pressing it twice expands the deck"))
            File.Move(oldGuide, oldGuide + ".legacy-" + Guid.NewGuid().ToString("N"));
        Seed("jarvis-guide.md", """
            # J.A.R.V.I.S. guide
            J.A.R.V.I.S. is a desktop assistant with local GGUF inference and optional cloud providers. Alt+J shows or hides the full deck. Alt+Space opens the floating pill for quick chat and app search. Type / for commands; /model opens engine settings, /status shows engine status, /reasoning controls deliberate mode. The system tray also offers quick access. The deck supports saved chats, editable titles, pinning, deletion, export, Markdown, themes, and curated fonts.

            # Memory and knowledge
            Chats and compact summaries are saved locally. About you notes are shared across chats. Imported Markdown and text documents are searched for relevant passages before each answer. Sources are reference material, not instructions. The knowledge library does not browse the web or retrain model weights. Cloud providers receive the prompt including selected memory and relevant document excerpts.

            # Capabilities and limits
            Deliberate mode guides useful answers but does not increase the underlying model's intelligence. Web search supplies current source excerpts only when enabled. MCP calls require connected, enabled tools and per-call approval. Skills are local guidance, separate from executable tools. Text and code files can be attached as bounded excerpts. File edits run only in the workspace when explicitly enabled. Image generation needs a compatible image endpoint and model. The assistant cannot see the screen, run shell commands, or inspect arbitrary files without a real configured tool that provides that capability. Never claim actions happened without their results.
            """);
        Seed("problem-solving.md", """
            # Debugging software and errors
            Start with the exact error, expected behavior, actual behavior, and smallest reproduction. Check recent changes, input formats, file paths, package versions, and logs. Fix one cause at a time. A successful build checks compilation; it does not prove runtime behavior. Never claim a test passed without a real result. Back up important files before changes.

            # Planning tasks and projects
            Define the outcome, constraints, and next concrete action. Break large goals into small deliverables. Prioritize dependencies and the most uncertain step. A useful plan states what to do first, what success looks like, and what could block progress. Choose a reasonable default when details are optional.

            # Explaining and learning
            Match the explanation to the learner. Define unfamiliar terms, use a small concrete example, then check edge cases. Distinguish evidence from assumptions. If a fact is uncertain or time-sensitive, say so and identify what would verify it. Do not fabricate sources or current facts.

            # Writing and communication
            Put the main point first. Prefer specific verbs and simple words. Give each paragraph one purpose. For an email, include a clear request and a useful next step. For a rewrite, preserve the author's intent and adapt tone to the audience. For brainstorming, offer varied feasible options with short tradeoffs.
            """);
        Seed("coding-reference.md", """
            # C# and .NET reference
            async Task methods represent asynchronous operations; await waits without blocking the caller thread. async void is normally reserved for UI event handlers. Use CancellationToken to cancel long operations. Use using or await using to dispose owned resources. A disposed native handle cannot be reused. Serialize access to shared model resources with a semaphore, including inference and model replacement. UI controls must be updated on the UI thread.

            # Python reference
            Lists preserve order and can contain duplicates. Dictionaries map unique hashable keys to values. Sets contain unique hashable elements. Use with open(path, encoding='utf-8') as file to close files reliably. Never use eval on untrusted input. Catch specific exceptions and preserve useful error context. A mutable default argument is shared across function calls; use None and initialize inside the function.

            # JavaScript reference
            const prevents reassignment of a binding but does not freeze objects. let has block scope. Use === for comparison without coercion. map transforms each element into a new array; filter selects matching elements; reduce accumulates a result. await only works inside an async function or a module supporting top-level await. Promise.all rejects when any input rejects; Promise.allSettled reports every outcome.

            # Git version control reference
            git status shows changes; git diff shows unstaged content differences; git log shows commit history. A branch names a line of development. A commit records a snapshot. Resolve merge conflicts by understanding both changes, editing the conflicted file, staging it, and completing the merge. Destructive commands such as reset --hard can discard work. Inspect status before changing branches or discarding changes.
            """);
        Seed("math-and-data.md", """
            # Percentages and arithmetic
            p percent of x equals x multiplied by p divided by 100. Percentage change from old to new is (new - old) divided by old, multiplied by 100; old must not be zero. A 20 percent increase followed by a 20 percent decrease does not return to the original value: 1.2 times 0.8 equals 0.96. Check units and estimate the order of magnitude before trusting a numeric answer.

            # Statistics basics
            The mean is sum divided by count. The median is the middle sorted value, or the average of the two middle values for an even count. An outlier affects the mean more than the median. Correlation describes association and does not establish causation. A small or biased sample can mislead. Separate raw observations from interpretations.

            # Units and rates
            Distance equals speed multiplied by time when speed is constant. Keep units consistent. One kilometer equals 1000 meters. One hour equals 3600 seconds. One byte contains 8 bits. Decimal kilo means 1000; binary kibi means 1024. For a rate, state both numerator and denominator, such as requests per second. Distinguish a duration from a calendar timestamp and include a timezone when it matters.
            """);
        Refresh();
    }
    private void Seed(string name, string text)
    { var path = Path.Combine(DirectoryPath, name); if (!File.Exists(path)) File.WriteAllText(path, text); }
    public void Import(string path)
    {
        if (!new[] { ".md", ".txt" }.Contains(Path.GetExtension(path).ToLowerInvariant())) throw new IOException("Choose a Markdown (.md) or text (.txt) file.");
        if (new FileInfo(path).Length > 1_000_000) throw new IOException("Documents must be smaller than 1 MB.");
        var name = Path.GetFileNameWithoutExtension(path);
        var destination = Path.Combine(DirectoryPath, name + "-" + Guid.NewGuid().ToString("N")[..6] + Path.GetExtension(path));
        File.Copy(path, destination); Refresh();
    }
    public void Refresh()
    {
        _chunks.Clear(); DocumentCount = 0;
        foreach (var file in Directory.EnumerateFiles(DirectoryPath).Where(f => Path.GetExtension(f) is ".md" or ".txt").Take(100))
        {
            try
            {
                if (new FileInfo(file).Length > 1_000_000 || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                var text = File.ReadAllText(file); DocumentCount++;
                foreach (var paragraph in Regex.Split(text, @"\r?\n\s*\r?\n"))
                    for (var offset = 0; offset < paragraph.Length; offset += 700)
                    {
                        var chunk = paragraph.Substring(offset, Math.Min(800, paragraph.Length - offset));
                        _chunks.Add((Path.GetFileName(file), chunk, Words(chunk + " " + Path.GetFileNameWithoutExtension(file))));
                    }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
    private static HashSet<string> Words(string text) => Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]{2,}").Select(m => m.Value).Where(w => !StopWords.Contains(w)).ToHashSet();
    public IReadOnlyList<KnowledgeHit> Search(string query, int limit = 3)
    {
        var terms = Words(query); if (terms.Count == 0) return Array.Empty<KnowledgeHit>();
        return _chunks.Select(chunk => new KnowledgeHit(chunk.Source, chunk.Text,
            terms.Where(chunk.Words.Contains).Sum(term => 1 + Math.Log(1 + (double)_chunks.Count / (1 + _chunks.Count(c => c.Words.Contains(term)))))))
            .Where(hit => hit.Score > 1.5).OrderByDescending(hit => hit.Score).Take(limit).ToArray();
    }
}
