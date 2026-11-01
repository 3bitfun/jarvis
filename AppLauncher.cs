using System.Diagnostics;
using System.ComponentModel;
using Avalonia.Media.Imaging;
namespace JARVIS;
public sealed record LaunchableApp(string Name, string Path) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private Bitmap? _icon;
    public Bitmap? Icon { get => _icon; set { _icon = value; PropertyChanged?.Invoke(this, new(nameof(Icon))); PropertyChanged?.Invoke(this, new(nameof(HasIcon))); } }
    public bool HasIcon => Icon is not null;
    public string Initial => Name.Length == 0 ? "↗" : Name[..1].ToUpperInvariant();
    public void Launch() => Process.Start(new ProcessStartInfo(Path) { UseShellExecute = true });
}
public sealed class AppLauncher
{
    private LaunchableApp[] _apps = Array.Empty<LaunchableApp>();
    private AppConfig? _config;
    private Action? _saveConfig;
    public IReadOnlyList<LaunchableApp> Apps => _apps;
    public void Configure(AppConfig config, Action saveConfig)
    {
        _config = config; _saveConfig = saveConfig;
        config.FavoriteAppPaths ??= new(); config.RecentAppPaths ??= new();
        config.ShortcutAppPaths ??= new(); config.WorkRoutineAppPaths ??= new();
        while (config.ShortcutAppPaths.Count < 5) config.ShortcutAppPaths.Add("");
        while (config.WorkRoutineAppPaths.Count < 3) config.WorkRoutineAppPaths.Add("");
    }
    public async Task RefreshAsync()
    {
        _apps = await Task.Run(() =>
        {
            var apps = new List<LaunchableApp>();
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), desktop, commonDesktop }.Distinct())
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = root != desktop && root != commonDesktop, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                        if (System.IO.Path.GetExtension(file).ToLowerInvariant() is ".lnk" or ".appref-ms" or ".exe") apps.Add(new(System.IO.Path.GetFileNameWithoutExtension(file), file));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return apps.DistinctBy(a => a.Name, StringComparer.OrdinalIgnoreCase).OrderBy(a => a.Name).ToArray();
        });
    }
    public IReadOnlyList<LaunchableApp> Search(string query)
    {
        query = query.Trim();
        if (query.Length == 0)
            return _apps.OrderByDescending(IsFavorite).ThenByDescending(RecentRank).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase).Take(7).ToArray();
        return _apps.Select(app => (App: app, Match: MatchScore(app.Name, query)))
            .Where(x => x.Match > 0)
            .Select(x => (x.App, Score: x.Match + (IsFavorite(x.App) ? 120 : 0) + RecentRank(x.App) * 12))
            .OrderByDescending(x => x.Score).ThenBy(x => x.App.Name).Take(7).Select(x => x.App).ToArray();
    }
    public bool IsFavorite(LaunchableApp app) => _config?.FavoriteAppPaths.Contains(app.Path, StringComparer.OrdinalIgnoreCase) == true;
    public void ToggleFavorite(LaunchableApp app)
    {
        if (_config is null) return;
        var existing = _config.FavoriteAppPaths.FindIndex(path => path.Equals(app.Path, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0) _config.FavoriteAppPaths.RemoveAt(existing); else _config.FavoriteAppPaths.Add(app.Path);
        _saveConfig?.Invoke();
    }
    public LaunchableApp? GetShortcut(int index)
    {
        if (index is < 0 or > 4 || _config is null || index >= _config.ShortcutAppPaths.Count) return null;
        var path = _config.ShortcutAppPaths[index];
        return string.IsNullOrWhiteSpace(path) ? null : _apps.FirstOrDefault(a => a.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
    }
    public bool HasShortcutAssignment(int index) => _config is not null && index is >= 0 and < 5 &&
        index < _config.ShortcutAppPaths.Count && !string.IsNullOrWhiteSpace(_config.ShortcutAppPaths[index]);
    public int VisibleShortcut(LaunchableApp app, int resultIndex)
    {
        if (_config is null) return resultIndex + 1;
        for (var i = 0; i < Math.Min(5, _config.ShortcutAppPaths.Count); i++)
            if (!string.IsNullOrWhiteSpace(_config.ShortcutAppPaths[i]))
            {
                if (_config.ShortcutAppPaths[i].Equals(app.Path, StringComparison.OrdinalIgnoreCase)) return i + 1;
                continue;
            }
        return resultIndex < 5 && string.IsNullOrWhiteSpace(_config.ShortcutAppPaths[resultIndex]) ? resultIndex + 1 : -1;
    }
    public void Launch(LaunchableApp app)
    {
        app.Launch();
        if (_config is null) return;
        _config.RecentAppPaths.RemoveAll(path => path.Equals(app.Path, StringComparison.OrdinalIgnoreCase));
        _config.RecentAppPaths.Insert(0, app.Path);
        if (_config.RecentAppPaths.Count > 12) _config.RecentAppPaths.RemoveRange(12, _config.RecentAppPaths.Count - 12);
        _saveConfig?.Invoke();
    }
    private int RecentRank(LaunchableApp app)
    {
        if (_config is null) return 0;
        var index = _config.RecentAppPaths.FindIndex(path => path.Equals(app.Path, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? 0 : _config.RecentAppPaths.Count - index;
    }
    internal static int MatchScore(string name, string query)
    {
        if (name.Equals(query, StringComparison.OrdinalIgnoreCase)) return 1000;
        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 800;
        if (name.Contains(query, StringComparison.OrdinalIgnoreCase)) return 600;
        var index = 0;
        foreach (var ch in name) if (index < query.Length && char.ToUpperInvariant(ch) == char.ToUpperInvariant(query[index])) index++;
        return index == query.Length ? Math.Max(1, 200 - name.Length) : 0;
    }
}
