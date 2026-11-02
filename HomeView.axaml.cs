using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
namespace JARVIS;
public partial class HomeView : UserControl
{
    private AppConfig? _config;
    private DashboardView? _dashboard;
    public Func<string, string>? WidgetData { get; set; }
    public Action<string>? ShortcutAction { get; set; }
    public event EventHandler<RoutedEventArgs>? NewChatRequested;
    public event EventHandler<RoutedEventArgs>? QuickBarRequested;
    public event EventHandler<RoutedEventArgs>? RoutineRequested;
    public event EventHandler<RoutedEventArgs>? RoutineSettingsRequested;
    public event EventHandler<RoutedEventArgs>? EngineRequested;
    public event EventHandler<RoutedEventArgs>? KnowledgeRequested;
    public event EventHandler<RoutedEventArgs>? StyleRequested;
    public Action<string>? OpenChat { get; set; }
    public Action<string>? StartPrompt { get; set; }
    public HomeView() => InitializeComponent();
    public void ConfigureDashboard(AppConfig config, Action save, Action welcome)
    {
        _config = config;
        var body = (StackPanel)((ScrollViewer)Content!).Content!;
        var dashboard = new DashboardView { WidgetData = id => WidgetData?.Invoke(id) ?? "Unavailable" }; _dashboard = dashboard; dashboard.Configure(config, save, welcome);
        body.Children.Insert(Math.Min(6, body.Children.Count), dashboard);
        config.HomeShortcuts ??= new();
        var shortcuts = new WrapPanel();
        void RenderShortcuts()
        {
            shortcuts.Children.Clear();
            foreach (var name in config.HomeShortcuts.Distinct().Where(n => new[] { "New chat", "Quick search", "Focus timer", "New note", "Pinned apps" }.Contains(n)))
            { var button = new Button { Content = name, Margin = new Avalonia.Thickness(0, 0, 8, 8) }; button.Click += (_, _) => ShortcutAction?.Invoke(name); shortcuts.Children.Add(button); }
        }
        var choices = new WrapPanel();
        foreach (var name in new[] { "New chat", "Quick search", "Focus timer", "New note", "Pinned apps" })
        { var check = new CheckBox { Content = name, IsChecked = config.HomeShortcuts.Contains(name), Margin = new Avalonia.Thickness(0, 0, 10, 8) }; check.IsCheckedChanged += (_, _) => { if (check.IsChecked == true) { if (!config.HomeShortcuts.Contains(name)) config.HomeShortcuts.Add(name); } else config.HomeShortcuts.Remove(name); save(); RenderShortcuts(); }; choices.Children.Add(check); }
        var shortcutPanel = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = "QUICK ACTIONS", Classes = { "label" } }, shortcuts, new Expander { Header = "Customize quick actions", Content = choices } } };
        body.Children.Insert(2, shortcutPanel); RenderShortcuts();
    }
    public void Refresh(IReadOnlyList<ChatInfo> chats, string engine, bool ready, int documentCount)
    {
        Greeting.Text = DateTime.Now.Hour switch { < 12 => "Good morning.", < 18 => "Good afternoon.", _ => "Good evening." };
        if (!string.IsNullOrWhiteSpace(_config?.DisplayName)) Greeting.Text = Greeting.Text.TrimEnd('.') + ", " + _config.DisplayName.Trim() + ".";
        EngineText.Text = engine;
        CommandEngine.Text = engine + (ready ? " · ready" : " · setup needed");
        EngineStatus.Text = ready ? "Ready for your next idea." : "Configure your engine in Settings to begin.";
        SetupCard.IsVisible = !ready;
        LibrarySummary.Text = $"{chats.Count} conversations · {chats.Count(chat => chat.IsPinned)} pinned · {documentCount} reference documents";
        KnowledgeText.Text = $"{documentCount} documents indexed";
        RecentChats.Children.Clear();
        foreach (var chat in chats.Where(c => c.Title != "New chat").Take(5))
        {
            var row = new Button { Content = AppIcon.Label("chat", chat.Title), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
            row.Click += (_, _) => OpenChat?.Invoke(chat.Id);
            RecentChats.Children.Add(row);
        }
        if (RecentChats.Children.Count == 0)
        {
            var note = new TextBlock { Text = "Your conversations will appear here. Start one whenever you're ready." };
            note.Classes.Add("muted"); RecentChats.Children.Add(note);
        }
    }
    public void SetRoutineStatus(string text) => RoutineStatus.Text = text;
    public void ApplyLayout(string layout)
    {
        Greeting.FontSize = layout == "Command Center" ? 28 : layout == "Modern" ? 32 : 36;
        Greeting.FontWeight = layout == "Command Center" ? Avalonia.Media.FontWeight.SemiBold : Avalonia.Media.FontWeight.Normal;
        HomeHero.Spacing = layout == "Command Center" ? 8 : 12;
        CommandStatus.IsVisible = layout == "Command Center";
        CommandStatus.Transitions = new Avalonia.Animation.Transitions { new Avalonia.Animation.DoubleTransition { Property = Avalonia.Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(220) } };
        if (CommandStatus.IsVisible && (_config?.HudAnimationIntensity ?? 1) > 0) { CommandStatus.Opacity = 0; Avalonia.Threading.Dispatcher.UIThread.Post(() => CommandStatus.Opacity = 1); } else CommandStatus.Opacity = 1;
        CommandCore.AnimationIntensity = Math.Clamp(_config?.HudAnimationIntensity ?? 1, 0, 2);
        CommandCore.AnimationsEnabled = CommandCore.AnimationIntensity > 0;
    }
    private void Starter_Click(object? sender, RoutedEventArgs e) { if (sender is Button { Tag: string prompt }) StartPrompt?.Invoke(prompt); }
    private void NewChat_Click(object? sender, RoutedEventArgs e) => NewChatRequested?.Invoke(this, e);
    private void QuickBar_Click(object? sender, RoutedEventArgs e) => QuickBarRequested?.Invoke(this, e);
    private void Routine_Click(object? sender, RoutedEventArgs e) => RoutineRequested?.Invoke(this, e);
    private void RoutineSettings_Click(object? sender, RoutedEventArgs e) => RoutineSettingsRequested?.Invoke(this, e);
    private void Engine_Click(object? sender, RoutedEventArgs e) => EngineRequested?.Invoke(this, e);
    private void Knowledge_Click(object? sender, RoutedEventArgs e) => KnowledgeRequested?.Invoke(this, e);
    private void Style_Click(object? sender, RoutedEventArgs e) => StyleRequested?.Invoke(this, e);
}
