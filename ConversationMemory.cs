using System.Text.Json;

namespace JARVIS;

public sealed record ChatTurn(string User, string Assistant, List<string>? Images = null, string? ModelUserContent = null);
public sealed record ChatInfo(string Id, string Title, bool IsPinned, string SearchText)
{
    public string PinGlyph => IsPinned ? "★" : "☆";
    public string PinIcon => IsPinned ? "pin-filled" : "pin";
    public override string ToString() => Title;
}

/// <summary>Local conversation archive and compact working memory shared by all models.</summary>
public sealed class ConversationMemory
{
    private sealed class SavedState
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "New chat";
        public bool IsPinned { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string Summary { get; set; } = "";
        public int CompactedCount { get; set; }
        public List<ChatTurn> Turns { get; set; } = new();
    }

    private sealed class Library
    {
        public string ActiveId { get; set; } = "";
        public List<SavedState> Chats { get; set; } = new();
    }

    private SavedState _state;
    private sealed record TransientTurn(int AfterSavedCount, ChatTurn Turn);
    private readonly Dictionary<string, List<TransientTurn>> _transientTurns = new();
    private readonly Dictionary<string, (int Count, string Summary)> _transientSummaries = new();
    private readonly Library _library;
    public IReadOnlyList<ChatInfo> Chats => _library.Chats
        .OrderByDescending(c => c.IsPinned).ThenByDescending(c => c.UpdatedAt)
        .Select(c => new ChatInfo(c.Id, DisplayTitle(c), c.IsPinned,
            DisplayTitle(c) + "\n" + string.Join("\n", OrderedTurns(c, 0).SelectMany(t => new[] { t.User, t.Assistant }))))
        .ToArray();
    public string ActiveId => _state.Id;
    public string FilePath { get; }
    public string? RecoveryNotice { get; }
    public IReadOnlyList<ChatTurn> SessionTurns => _transientTurns.TryGetValue(_state.Id, out var turns) ? turns.Skip(_transientSummaries.GetValueOrDefault(_state.Id).Count).Select(t => t.Turn).ToArray() : Array.Empty<ChatTurn>();
    public string SessionSummary => _transientSummaries.TryGetValue(_state.Id, out var value) ? value.Summary : "";
    public string Summary => _state.Summary;
    public int CompactedCount => _state.CompactedCount;
    public IReadOnlyList<ChatTurn> Turns => OrderedTurns(_state, 0).ToArray();
    public IReadOnlyList<ChatTurn> ContextTurns => OrderedTurns(_state, _state.CompactedCount, _transientSummaries.GetValueOrDefault(_state.Id).Count).ToArray();

    private string DisplayTitle(SavedState state)
    {
        if (state.Title != "New chat" || !_transientTurns.TryGetValue(state.Id, out var turns) || turns.Count == 0) return state.Title;
        var title = turns[0].Turn.User.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return title.Length > 32 ? title[..32] + "…" : title.Length == 0 ? "New chat" : title;
    }

    private IEnumerable<ChatTurn> OrderedTurns(SavedState state, int skipSaved, int skipTransient = 0)
    {
        _transientTurns.TryGetValue(state.Id, out var transient);
        var position = 0;
        for (var i = 0; i <= state.Turns.Count; i++)
        {
            while (transient is not null && position < transient.Count && transient[position].AfterSavedCount == i)
            { if (position >= skipTransient) yield return transient[position].Turn; position++; }
            if (i < state.Turns.Count && i >= skipSaved) yield return state.Turns[i];
        }
    }

    public ConversationMemory(string? directory = null)
    {
        directory ??= AppPaths.DataDirectory;
        Directory.CreateDirectory(directory);
        FilePath = Path.Combine(directory, "memory.json");
        _library = AtomicStorage.Read(FilePath, json =>
        {
            var library = JsonSerializer.Deserialize<Library>(json) ?? throw new InvalidDataException("Empty chat archive.");
            library.Chats ??= new();
            if (library.Chats.Count == 0 && !json.Contains("\"Chats\"")) library.Chats.Add(JsonSerializer.Deserialize<SavedState>(json) ?? new SavedState());
            return library;
        }, () => new Library(), out var recovery);
        RecoveryNotice = recovery;
        if (_library.Chats.Count == 0)
            _library.Chats.Add(new SavedState());
        _library.Chats = _library.Chats.Where(chat => chat is not null).ToList();
        if (_library.Chats.Count == 0) _library.Chats.Add(new SavedState());
        foreach (var chat in _library.Chats)
        {
            chat.Title ??= "New chat"; chat.Summary ??= ""; chat.Turns ??= new List<ChatTurn>();
            chat.Turns = chat.Turns.Where(turn => turn is not null).ToList();
            chat.CompactedCount = Math.Clamp(chat.CompactedCount, 0, chat.Turns.Count);
            if (string.IsNullOrWhiteSpace(chat.Id)) chat.Id = Guid.NewGuid().ToString("N");
        }
        _state = _library.Chats.FirstOrDefault(c => c.Id == _library.ActiveId) ?? _library.Chats[0];
    }

    public void Add(string user, string assistant, IReadOnlyList<string>? images = null, bool persist = true, string? modelUserContent = null)
    {
        if (!persist)
        {
            if (!_transientTurns.TryGetValue(_state.Id, out var transient)) _transientTurns[_state.Id] = transient = new();
            transient.Add(new TransientTurn(_state.Turns.Count, new ChatTurn(user, assistant, images?.ToList(), modelUserContent)));
            return;
        }
        if (_state.Turns.Count == 0 && _state.Title == "New chat") _state.Title = user.Length > 32 ? user[..32] + "…" : user;
        _state.Turns.Add(new ChatTurn(user, assistant, images?.ToList(), modelUserContent));
        _state.UpdatedAt = DateTime.UtcNow;
        Save();
    }

    public void NewChat()
    {
        _state = new SavedState();
        _library.Chats.Insert(0, _state);
        Save();
    }

    public void SelectChat(string id)
    {
        _state = _library.Chats.First(c => c.Id == id);
        _state.UpdatedAt = DateTime.UtcNow;
        Save();
    }
    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        _state.Title = title.Trim()[..Math.Min(title.Trim().Length, 100)];
        _state.UpdatedAt = DateTime.UtcNow;
        Save();
    }

    public bool TogglePinned(string id)
    {
        var chat = _library.Chats.First(c => c.Id == id);
        chat.IsPinned = !chat.IsPinned;
        chat.UpdatedAt = DateTime.UtcNow;
        Save();
        return chat.IsPinned;
    }

    public void DeleteChat(string id)
    {
        var chat = _library.Chats.First(c => c.Id == id);
        _library.Chats.Remove(chat);
        _transientTurns.Remove(id);
        _transientSummaries.Remove(id);
        if (_state.Id == id)
        {
            if (_library.Chats.Count == 0) _library.Chats.Add(new SavedState());
            _state = _library.Chats[0];
        }
        Save();
        // Replace the rotating backup too, so a deleted chat cannot reappear after recovery.
        AtomicStorage.PurgeHistory(FilePath);
    }

    public string ExportMarkdown(string id)
    {
        var chat = _library.Chats.First(c => c.Id == id);
        var output = new System.Text.StringBuilder("# ").AppendLine(DisplayTitle(chat)).AppendLine();
        foreach (var turn in OrderedTurns(chat, 0))
        {
            output.AppendLine("## You").AppendLine().AppendLine(turn.User).AppendLine();
            output.AppendLine("## J.A.R.V.I.S.").AppendLine().AppendLine(turn.Assistant).AppendLine();
            if (turn.Images is { Count: > 0 })
                foreach (var image in turn.Images) output.Append("![Generated image](").Append(image.Replace("\\", "/")).AppendLine(")");
            output.AppendLine();
        }
        return output.ToString();
    }

    public int ClearAllDetails()
    {
        var count = _library.Chats.Sum(chat => chat.Turns.Count) + _transientTurns.Values.Sum(turns => turns.Count);
        _transientTurns.Clear();
        _transientSummaries.Clear();
        foreach (var chat in _library.Chats)
        {
            chat.Turns.Clear();
            chat.Summary = "";
            chat.CompactedCount = 0;
            chat.UpdatedAt = DateTime.UtcNow;
        }
        Save();
        AtomicStorage.PurgeHistory(FilePath);
        return count;
    }

    public void EditSummary(string summary)
    {
        _state.Summary = summary.Trim();
        if (string.IsNullOrWhiteSpace(summary)) _state.CompactedCount = 0;
        Save();
    }

    public void Compact(int count, string summary)
    {
        if (count <= 0 || count > _state.Turns.Count - _state.CompactedCount)
            throw new ArgumentOutOfRangeException(nameof(count));
        _state.CompactedCount += count;
        _state.Summary = summary.Trim();
        Save();
    }

    public void CompactSession(int count, string summary)
    {
        if (count <= 0 || count > SessionTurns.Count) throw new ArgumentOutOfRangeException(nameof(count));
        _transientSummaries[_state.Id] = (_transientSummaries.GetValueOrDefault(_state.Id).Count + count, summary.Trim());
    }

    public IReadOnlyList<ChatTurn> ActiveTurns => _state.Turns.Skip(_state.CompactedCount).ToArray();

    private void Save()
    {
        _library.ActiveId = _state.Id;
        AtomicStorage.Write(FilePath, JsonSerializer.Serialize(_library, new JsonSerializerOptions { WriteIndented = true }));
    }
}
