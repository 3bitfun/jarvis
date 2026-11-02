using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace JARVIS;

public partial class MainWindow
{
    private TrayIcon? _tray;
    private readonly DispatcherTimer _draftSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly HashSet<string> _privateDraftIds = new();
    private bool _closing;
    private void InitializeShell(bool interactive)
    {
        VersionLabel.Text = "J.A.R.V.I.S. " + typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0];
        TemperatureInput.Value = (decimal)_config.Temperature; ResponseLengthInput.Value = _config.ResponseTokens;
        ImageEndpointInput.Text = _config.ImageProvider.BaseUrl; ImageKeyInput.Text = _config.ImageProvider.ApiKey; ImageModelInput.Text = _config.ImageProvider.Model;
        if (_config.MemoryEnabled) foreach (var pair in _config.Drafts) _drafts[pair.Key] = pair.Value;
        _draftSaveTimer.Tick += (_, _) => { _draftSaveTimer.Stop(); SaveDraft(); };
        PromptBox.TextChanged += (_, _) => { if (_ready && !_busy && _config.MemoryEnabled) { _draftSaveTimer.Stop(); _draftSaveTimer.Start(); } };
        AddHandler(KeyDownEvent, Shell_KeyDown, RoutingStrategies.Tunnel);
        var notice = _store.RecoveryNotice ?? _engine.MemoryRecoveryNotice;
        if (notice is not null) StatusText.Text = notice;
        if (!interactive) return;
        DesktopInstance.Current?.Attach(() => Dispatcher.UIThread.Post(OpenFullDeck));
        try
        {
            using var iconStream = AssetLoader.Open(new Uri("avares://JARVIS/Assets/Jarvis.ico"));
            Icon = new WindowIcon(iconStream);
            var menu = new NativeMenu();
            void Add(string title, Action action)
            {
                var item = new NativeMenuItem(title); item.Click += (_, _) => Dispatcher.UIThread.Post(action); menu.Items.Add(item);
            }
            Add("Open J.A.R.V.I.S.", OpenFullDeck);
            Add("Quick chat and apps", ToggleQuickBar);
            Add("New conversation", () => { OpenFullDeck(); NewChat_Click(this, new RoutedEventArgs()); });
            menu.Items.Add(new NativeMenuItemSeparator());
            Add("Pause / resume memory", () => { if (_busy) ShowNotice("Stop the current request before changing memory."); else MemoryCheck.IsChecked = !(_config.MemoryEnabled); });
            Add("Settings", () => { OpenFullDeck(); OpenSettingsTab(0); });
            menu.Items.Add(new NativeMenuItemSeparator());
            Add("Quit", Close);
            _tray = new TrayIcon { Icon = Icon, ToolTipText = "J.A.R.V.I.S. · Alt+Space quick bar · Alt+J full deck", Menu = menu, IsVisible = true };
            _tray.Clicked += (_, _) => OpenFullDeck();
        }
        catch (Exception ex) { AppDiagnostics.Write(ex); }
        Closing += (_, _) => { _closing = true; _generation?.Cancel(); SaveDraft(); _draftSaveTimer.Stop(); _tray?.Dispose(); };
    }
    private void SaveDraft()
    {
        var text = PromptBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(text)) { _drafts.Remove(_engine.ActiveChatId); _privateDraftIds.Remove(_engine.ActiveChatId); }
        else { _drafts[_engine.ActiveChatId] = text[..Math.Min(text.Length, 20000)]; if (!_config.MemoryEnabled) _privateDraftIds.Add(_engine.ActiveChatId); }
        if (!_config.MemoryEnabled) return;
        var validIds = _engine.Chats.Select(chat => chat.Id).ToHashSet();
        _config.Drafts = _drafts.Where(pair => validIds.Contains(pair.Key) && !_privateDraftIds.Contains(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value)).TakeLast(20).ToDictionary(pair => pair.Key, pair => pair.Value);
        SavePreferences();
    }
    internal void CheckPrivateDrafts()
    {
        MemoryCheck.IsChecked = false;
        var first = _engine.ActiveChatId;
        PromptBox.Text = "private draft should not be saved"; SaveDraft();
        _engine.NewChat(); PromptBox.Text = "another private draft"; SaveDraft();
        MemoryCheck.IsChecked = true;
        _engine.SelectChat(first); PromptBox.Text = _drafts.GetValueOrDefault(first, ""); SaveDraft();
        if (PromptBox.Text != "private draft should not be saved" || _store.Load().Drafts.Values.Any(text => text.Contains("private draft")))
            throw new InvalidOperationException("Private drafts were lost or persisted after memory resumed.");
        _draftSaveTimer.Stop();
    }
    internal void CheckComposerKeys()
    {
        PromptBox.Text = "";
        var enter = new KeyEventArgs { RoutedEvent = KeyDownEvent, Key = Key.Enter };
        PromptBox.RaiseEvent(enter);
        if (!enter.Handled || !string.IsNullOrEmpty(PromptBox.Text)) throw new InvalidOperationException("Enter did not trigger the composer send handler.");
        var shifted = new KeyEventArgs { RoutedEvent = KeyDownEvent, Key = Key.Enter, KeyModifiers = KeyModifiers.Shift };
        PromptBox_KeyDown(PromptBox, shifted);
        if (shifted.Handled) throw new InvalidOperationException("Shift+Enter was blocked by the send handler.");
    }
    private void Shell_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (CommandSuggestions.IsVisible) CommandSuggestions.IsVisible = false;
            else if (SettingsSplitView.IsPaneOpen) SettingsSplitView.IsPaneOpen = false;
            else if (_busy) _generation?.Cancel();
            else Hide();
            e.Handled = true; return;
        }
        if (e.Key == Key.Tab && CommandSuggestions.IsVisible && CommandSuggestions.ItemsSource is IEnumerable<string> commands)
        {
            var command = commands.FirstOrDefault();
            if (command is not null) { PromptBox.Text = command.Split(' ')[0] + " "; PromptBox.CaretIndex = PromptBox.Text.Length; CommandSuggestions.IsVisible = false; e.Handled = true; }
            return;
        }
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        switch (e.Key)
        {
            case Key.N when !_busy: NewChat_Click(this, new RoutedEventArgs()); break;
            case Key.K: ChatSearch.Focus(); ChatSearch.SelectAll(); break;
            case Key.OemComma: OpenSettingsTab(0); break;
            case Key.L: PromptBox.Focus(); break;
            case Key.E when e.KeyModifiers.HasFlag(KeyModifiers.Shift): ExportChat_Click(this, new RoutedEventArgs()); break;
            default: return;
        }
        e.Handled = true;
    }
    private async Task<bool> ConfirmAsync(string title, string message, string action)
    {
        var dialog = new Window { Title = title, Width = 450, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
        var cancel = new Button { Content = "Cancel" }; var confirm = new Button { Content = action, Classes = { "primary" } };
        cancel.Click += (_, _) => dialog.Close(false); confirm.Click += (_, _) => dialog.Close(true);
        dialog.Content = new StackPanel { Margin = new Thickness(24), Spacing = 18, Children = {
            new TextBlock { Text = title, FontSize = 21, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 10, Children = { cancel, confirm } } } };
        return await dialog.ShowDialog<bool>(this);
    }
    private async void DeleteChat_Click(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var id = _engine.ActiveChatId;
        var title = _engine.Chats.First(chat => chat.Id == id).Title;
        if (!await ConfirmAsync("Delete this conversation?", $"Delete ‘{title}’ and its messages? Export it first if you want to keep a copy. Generated image files stay in your workspace.", "Delete conversation")) return;
        try
        {
            _engine.DeleteChat(id); _drafts.Remove(id); _privateDraftIds.Remove(id); _config.Drafts.Remove(id); _attachments.Clear(); UpdateAttachmentStatus();
            PromptBox.Text = _drafts.GetValueOrDefault(_engine.ActiveChatId, ""); SavePreferences(); _store.ForgetPreviousVersion();
            RefreshChats(); ShowConversation(); RefreshHome(); StatusText.Text = "Conversation deleted";
        }
        catch (Exception ex) { ShowNotice("Could not delete conversation: " + ex.Message); }
    }
    private void EditLastMessage_Click(object? sender, RoutedEventArgs e)
    {
        if (_busy || _engine.Conversation.LastOrDefault() is not { } last) return;
        PromptBox.Text = last.User; PromptBox.Focus(); PromptBox.CaretIndex = PromptBox.Text.Length;
        StatusText.Text = "Last message copied to the composer. Edit and send as a new message.";
    }
    private async void AttachFiles_Click(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Attach text or source files", AllowMultiple = true });
        await AttachFilesAsync(files);
    }
    private async void CopyDiagnostics_Click(object? sender, RoutedEventArgs e)
    {
        var summary = new { version = typeof(App).Assembly.GetName().Version?.ToString(), os = Environment.OSVersion.VersionString,
            architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), provider = _config.ActiveProvider,
            localModelLoaded = _engine.ActiveModelPath is not null, modelCount = _engine.LocalModels.Count, chatCount = _engine.Chats.Count,
            memory = _config.MemoryEnabled, webSearch = _config.WebSearchEnabled, mcpTools = _engine.Mcp.GetEnabledTools().Count, skills = _engine.Skills.Skills.Count,
            transparency = ActualTransparencyLevel.ToString() };
        try { if (Clipboard is not null) await Clipboard.SetTextAsync(JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true })); StatusText.Text = "Diagnostics copied. No keys, messages, or personal profile included."; }
        catch (Exception ex) { ShowNotice("Could not copy diagnostics: " + ex.Message); }
    }
    private void OpenDataFolder_Click(object? sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(AppDiagnostics.DataDirectory); Process.Start(new ProcessStartInfo(AppDiagnostics.DataDirectory) { UseShellExecute = true }); }
        catch (Exception ex) { ShowNotice(ex.Message); }
    }
}
