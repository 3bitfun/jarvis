using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
namespace JARVIS;
public enum PetPillState { Idle, Input, Reply }
public sealed record LauncherResult(LaunchableApp App, int Number)
{
    public bool HasShortcut => Number is >= 1 and <= 5;
    public string Shortcut => HasShortcut ? "Alt " + Number : "";
    public bool IsFavorite { get; set; }
    public string FavoriteGlyph => IsFavorite ? "★" : "☆";
    public string FavoriteIcon => IsFavorite ? "star-filled" : "star";
}
public partial class MiniLauncherWindow : Window
{
    private readonly AppLauncher _launcher;
    private readonly AppIconCache _icons = new();
    private readonly Func<string, Task<string>> _ask;
    private readonly Action _fullDeck;
    private readonly Func<string> _petStatus;
    private PetPillState _state = PetPillState.Idle;
    private PixelRect _workArea = new(0, 0, 1920, 1080);
    private double _scale = 1;
    private Window? _petAnchor;
    public bool CompactLayout { get; set; }
    private bool _movingInput, _replyReady;
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _busy, _closed, _ready;
    private Task? _indexing;
    private readonly DispatcherTimer _morph = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private EventHandler? _morphFrame;
    private LauncherResult[] _visible = Array.Empty<LauncherResult>();
    private MarkdownChat? _markdown;
    public MiniLauncherWindow() : this(_ => Task.FromResult("Open the quick bar from the main application."), () => { }) { }
    public MiniLauncherWindow(Func<string, Task<string>> ask, Action fullDeck, Func<string>? petStatus = null, AppLauncher? launcher = null)
    {
        _launcher = launcher ?? new(); _ask = ask; _fullDeck = fullDeck; _petStatus = petStatus ?? (() => "J.A.R.V.I.S. · Ready"); InitializeComponent(); _ready = true;
        ToolTip.SetTip(QueryBox, "Try: open Notepad · start a 25 minute timer · timer status · stop timer · make a note: Buy milk");
        GlassBackdrop.Configure(this);
        _statusTimer.Tick += (_, _) => { if (IsVisible) PetStatus.Text = _busy ? "Thinking…" : _petStatus(); };
        _statusTimer.Start();
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        Opened += async (_, _) =>
        {
            if (_indexing is not null) return;
            Status.Text = "Finding your apps…";
            try { _indexing = _launcher.RefreshAsync(); await _indexing; if (!_closed && !_busy) UpdateResults(); }
            catch (Exception) { if (!_closed) Status.Text = "App search could not initialize. You can still ask J.A.R.V.I.S."; }
        };
        Closed += (_, _) => { _closed = true; _morph.Stop(); _statusTimer.Stop(); _markdown?.Dispose(); _icons.Dispose(); };
    }
    public void Toggle(Window main, bool anchorToPet = false)
    {
        if (IsVisible)
        {
            Hide();
            return;
        }
        _petAnchor = anchorToPet ? main : null;
        var screen = main.Screens.ScreenFromWindow(main) ?? main.Screens.Primary;
        if (screen is not null) { _workArea = screen.WorkingArea; _scale = screen.Scaling; }
        if (_petAnchor is not null) { Show(); Activate(); ExpandInput(); return; }
        SetPillState(PetPillState.Idle);
        Show(); Activate(); FocusIdle();
        _morph.Stop();
        var started = System.Diagnostics.Stopwatch.StartNew();
        Width = Height = 96;
        OpeningMark.IsVisible = true; PillShell.Opacity = 0;
        EventHandler? frame = null;
        frame = (_, _) =>
        {
            if (!IsVisible || _closed) { _morph.Stop(); _morph.Tick -= frame; return; }
            var t = Math.Clamp((started.Elapsed.TotalMilliseconds - 140) / 420, 0, 1);
            var eased = 1 - Math.Pow(1 - t, 3);
            OpeningMark.IsVisible = eased < .35;
            OpeningMark.Opacity = 1 - eased;
            PillShell.Opacity = eased;
            Width = 96 + (Math.Min(560, _workArea.Width / _scale - 24) - 96) * eased;
            Height = 96 + 204 * eased;
            Position = new PixelPoint(_workArea.X + (_workArea.Width - (int)(Width * _scale)) / 2, _workArea.Y + (int)(16 * _scale));
            PillShell.CornerRadius = new CornerRadius(48 - 28 * eased);
            if (t >= 1) { _morph.Stop(); _morph.Tick -= frame; ExpandInput(); }
        };
        _morphFrame = frame; _morph.Tick += frame; _morph.Start();
    }
    private void SetPillState(PetPillState state)
    {
        _morph.Stop();
        if (_morphFrame is not null) { _morph.Tick -= _morphFrame; _morphFrame = null; }
        OpeningMark.IsVisible = false; PillShell.Opacity = 1;
        _state = state;
        var idle = state == PetPillState.Idle;
        var areaWidth = _workArea.Width / _scale;
        var areaHeight = _workArea.Height / _scale;
        Width = Math.Min(idle ? 344 : CompactLayout ? 440 : 560, areaWidth - 24);
        var desiredHeight = idle ? 72 : state == PetPillState.Reply ? (CompactLayout ? 320 : 400) : Math.Clamp((CompactLayout ? 178 : 218) + _visible.Length * 46, 260, CompactLayout ? 390 : 470);
        Height = Math.Min(desiredHeight, areaHeight - 48);
        if (_petAnchor is not null)
        {
            var width = (int)(Width * _scale); var height = (int)(Height * _scale);
            var x = Math.Clamp(_petAnchor.Position.X + (int)(_petAnchor.Width * _scale / 2) - width / 2, _workArea.X, Math.Max(_workArea.X, _workArea.Right - width));
            var above = _petAnchor.Position.Y - height - (int)(8 * _scale);
            var y = above >= _workArea.Y ? above : Math.Min(_petAnchor.Position.Y + (int)(_petAnchor.Height * _scale) + (int)(8 * _scale), _workArea.Bottom - height);
            Position = new PixelPoint(x, Math.Max(_workArea.Y, y));
        }
        else Position = new PixelPoint(_workArea.X + (_workArea.Width - (int)(Width * _scale)) / 2, _workArea.Y + (int)(16 * _scale));
        PillShell.CornerRadius = new CornerRadius(idle ? 28 : 20);
        PetBody.IsVisible = !idle;
        PetStatus.Text = _busy ? "Thinking…" : _petStatus();
    }
    private void FocusIdle() => Dispatcher.UIThread.Post(() => { if (IsVisible && _state == PetPillState.Idle) { Activate(); IdleInput.Focus(); } });
    private void ExpandInput()
    {
        SetPillState(_busy || _replyReady ? PetPillState.Reply : PetPillState.Input);
        if (!_busy) Dispatcher.UIThread.Post(() => { QueryBox.Focus(); QueryBox.SelectAll(); });
    }
    private void IdleInput_Changed(object? sender, TextChangedEventArgs e)
    {
        if (!_ready || _movingInput || string.IsNullOrEmpty(IdleInput.Text) || _busy) return;
        _movingInput = true;
        var text = IdleInput.Text; IdleInput.Text = "";
        SetPillState(PetPillState.Input); QueryBox.Text = text;
        QueryBox.Focus(); QueryBox.CaretIndex = text.Length;
        _movingInput = false;
    }
    private void PillHeader_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control c && (c is Button || c.FindAncestorOfType<Button>() is not null)) return;
        ExpandInput(); e.Handled = true;
    }
    private void Query_Changed(object? sender, TextChangedEventArgs e) { if (_ready && !_busy) UpdateResults(); }
    private void UpdateResults()
    {
        _replyReady = false;
        _markdown?.Dispose(); _markdown = null; ReplyContent.Content = null;
        ReplyScroll.IsVisible = false; ThinkingProgress.IsVisible = false;
        var query = QueryBox.Text?.Trim() ?? "";
        _visible = _launcher.Search(query).Take(5).Select((app, i) => new LauncherResult(app, _launcher.VisibleShortcut(app, i)) { IsFavorite = _launcher.IsFavorite(app) }).ToArray();
        Results.ItemsSource = _visible; Results.SelectedIndex = _visible.Length > 0 ? 0 : -1;
        Results.IsVisible = _visible.Length > 0; EmptyState.IsVisible = _visible.Length == 0;
        ResultCount.Text = _visible.Length == 0 ? "" : _visible.Length + " results";
        SectionLabel.Text = query.Length == 0 ? "Applications" : "Search results";
        ClearButton.IsVisible = query.Length > 0;
        Status.Text = _visible.Length == 0 ? "No app match. Press Enter to ask J.A.R.V.I.S." : "Press Enter to ask, or Alt+1–5 to launch an app.";
        foreach (var result in _visible) _ = LoadIconAsync(result.App);
        if (_state != PetPillState.Idle || query.Length > 0) SetPillState(PetPillState.Input);
    }
    private async Task LoadIconAsync(LaunchableApp app)
    {
        if (app.HasIcon) return;
        var icon = await _icons.GetAsync(app.Path);
        if (!_closed) app.Icon = icon;
    }
    internal static int ShortcutIndex(Key key) => key switch
    {
        Key.D1 or Key.NumPad1 => 0, Key.D2 or Key.NumPad2 => 1, Key.D3 or Key.NumPad3 => 2,
        Key.D4 or Key.NumPad4 => 3, Key.D5 or Key.NumPad5 => 4, _ => -1
    };
    private async void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_state == PetPillState.Idle) Hide(); else { SetPillState(PetPillState.Idle); FocusIdle(); }
            e.Handled = true; return;
        }
        if (_state == PetPillState.Idle)
        { if (e.Key == Key.Enter) { ExpandInput(); e.Handled = true; } return; }
        var index = ShortcutIndex(e.Key);
        if (e.KeyModifiers == KeyModifiers.Alt && index >= 0)
        {
            e.Handled = true;
            if (!_busy && _state == PetPillState.Input)
            {
                if (_launcher.HasShortcutAssignment(index))
                {
                    if (_launcher.GetShortcut(index) is { } pinnedApp) Launch(pinnedApp);
                    else Status.Text = $"Alt+{index + 1} is assigned to an app that is no longer installed. Update it in Settings → Apps.";
                }
                else if (Results.IsVisible && index < _visible.Length) Launch(_visible[index].App);
            }
            return;
        }
        if (_busy) return;
        var navigatingResults = QueryBox.IsFocused || Results.IsKeyboardFocusWithin;
        if ((e.Key == Key.Down || e.Key == Key.Up) && Results.IsVisible && navigatingResults)
        {
            e.Handled = true;
            Results.SelectedIndex = Math.Clamp(Results.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, Math.Max(0, _visible.Length - 1));
            if (Results.SelectedItem is not null) Results.ScrollIntoView(Results.SelectedItem);
        }
        else if (e.Key == Key.Enter && navigatingResults)
        {
            e.Handled = true;
            if (Results.IsKeyboardFocusWithin && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && Results.IsVisible && Results.SelectedItem is LauncherResult result) Launch(result.App);
            else await AskAsync();
        }
    }
    private void Launch(LaunchableApp app)
    {
        try { _launcher.Launch(app); Hide(); }
        catch (Exception) { Status.Text = "Couldn't open " + app.Name + ". Try another result or refresh the app list next launch."; }
    }
    private void Results_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    { if (_ready && Results.SelectedItem is LauncherResult result) Status.Text = "Enter to ask · Alt+1–5 to launch " + result.App.Name; }
    private void Results_DoubleTapped(object? sender, TappedEventArgs e)
    { if (!_busy && Results.SelectedItem is LauncherResult result) Launch(result.App); }
    private void Favorite_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: LauncherResult result }) return;
        _launcher.ToggleFavorite(result.App);
        result.IsFavorite = _launcher.IsFavorite(result.App);
        UpdateResults();
    }
    private async void Ask_Click(object? sender, RoutedEventArgs e) => await AskAsync();
    private async Task AskAsync()
    {
        var query = QueryBox.Text?.Trim(); if (_busy || string.IsNullOrWhiteSpace(query)) { if (!_busy) QueryBox.Focus(); return; }
        _busy = true; _replyReady = false; SetPillState(PetPillState.Reply); AskButton.IsEnabled = false; QueryBox.IsEnabled = false; ClearButton.IsEnabled = false;
        Results.IsVisible = EmptyState.IsVisible = false; ReplyScroll.IsVisible = true; ThinkingProgress.IsVisible = true;
        _markdown?.Dispose(); _markdown = null;
        SectionLabel.Text = "Jarvis"; ResultCount.Text = "";
        ReplyContent.Content = new TextBlock { Text = "Working on your question…", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        Status.Text = "Using your current conversation and saved context.";
        try
        {
            var answer = await _ask(query); if (_closed) return;
            _replyReady = true; _markdown = new MarkdownChat(); ReplyContent.Content = _markdown.Render(answer);
            Status.Text = "Continue in the full deck with Alt+J, or type a new search above.";
        }
        catch (Exception) { if (!_closed) ReplyContent.Content = new TextBlock { Text = "Couldn't complete the request. Try again or check the engine in the full deck.", TextWrapping = Avalonia.Media.TextWrapping.Wrap }; }
        finally
        {
            _busy = false;
            if (!_closed) { AskButton.IsEnabled = QueryBox.IsEnabled = ClearButton.IsEnabled = true; ThinkingProgress.IsVisible = false; if (IsVisible) { if (_state == PetPillState.Idle) FocusIdle(); else { QueryBox.Focus(); QueryBox.SelectAll(); } } PetStatus.Text = _petStatus(); }
        }
    }
    private void Clear_Click(object? sender, RoutedEventArgs e) { QueryBox.Text = ""; QueryBox.Focus(); }
    private void CloseBar_Click(object? sender, RoutedEventArgs e) => Hide();
    private void FullDeck_Click(object? sender, RoutedEventArgs e) { Hide(); _fullDeck(); }
}
