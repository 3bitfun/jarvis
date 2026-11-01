using System.Text.Json;

namespace JARVIS;

public sealed record ChatTurn(string User, string Assistant, List<string>? Images = null);
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
    private readonly List<ChatTurn> _transientTurns = new();
    private readonly Library _library;
    public IReadOnlyList<ChatInfo> Chats => _library.Chats
        .OrderByDescending(c => c.IsPinned).ThenByDescending(c => c.UpdatedAt)
        .Select(c => new ChatInfo(c.Id, c.Title, c.IsPinned,
            c.Title + "\n" + string.Join("\n", c.Turns.SelectMany(t => new[] { t.User, t.Assistant })) +
            (c.Id == _state.Id ? "\n" + string.Join("\n", _transientTurns.SelectMany(t => new[] { t.User, t.Assistant })) : "")))
        .ToArray();
    public string ActiveId => _state.Id;
    public string FilePath { get; }
    public string? RecoveryNotice { get; }
    public IReadOnlyList<ChatTurn> SessionTurns => _transientTurns.ToArray();
    public string Summary => _state.Summary;
    public int CompactedCount => _state.CompactedCount;
    public IReadOnlyList<ChatTurn> Turns => _state.Turns.Concat(_transientTurns).ToArray();

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

    public void Add(string user, string assistant, IReadOnlyList<string>? images = null, bool persist = true)
    {
        if (!persist) { _transientTurns.Add(new ChatTurn(user, assistant, images?.ToList())); return; }
        if (_state.Turns.Count == 0 && _state.Title == "New chat") _state.Title = user.Length > 32 ? user[..32] + "…" : user;
        _state.Turns.Add(new ChatTurn(user, assistant, images?.ToList()));
        _state.UpdatedAt = DateTime.UtcNow;
        Save();
    }

    public void NewChat()
    {
        _transientTurns.Clear();
        _state = new SavedState();
        _library.Chats.Insert(0, _state);
        Save();
    }

    public void SelectChat(string id)
    {
        _transientTurns.Clear();
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
        if (_state.Id == id)
        {
            _transientTurns.Clear();
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
        var output = new System.Text.StringBuilder("# ").AppendLine(chat.Title).AppendLine();
        foreach (var turn in chat.Turns.Concat(chat.Id == _state.Id ? _transientTurns.AsEnumerable() : Array.Empty<ChatTurn>()))
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
        var count = _library.Chats.Sum(chat => chat.Turns.Count) + _transientTurns.Count;
        _transientTurns.Clear();
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

    public void Compact(int count, string summary)
    {
        if (count <= 0 || count > _state.Turns.Count - _state.CompactedCount)
            throw new ArgumentOutOfRangeException(nameof(count));
        _state.CompactedCount += count;
        _state.Summary = summary.Trim();
        Save();
    }

    public IReadOnlyList<ChatTurn> ActiveTurns => _state.Turns.Skip(_state.CompactedCount).ToArray();

    private void Save()
    {
        _library.ActiveId = _state.Id;
        AtomicStorage.Write(FilePath, JsonSerializer.Serialize(_library, new JsonSerializerOptions { WriteIndented = true }));
    }
}
