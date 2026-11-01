using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
namespace JARVIS;
public partial class HomeView : UserControl
{
    public event EventHandler<RoutedEventArgs>? NewChatRequested;
    public event EventHandler<RoutedEventArgs>? QuickBarRequested;
    public event EventHandler<RoutedEventArgs>? RoutineRequested;
    public event EventHandler<RoutedEventArgs>? RoutineSettingsRequested;
    public event EventHandler<RoutedEventArgs>? EngineRequested;
    public event EventHandler<RoutedEventArgs>? KnowledgeRequested;
    public event EventHandler<RoutedEventArgs>? StyleRequested;
    public Action<string>? OpenChat { get; set; }
    public HomeView() => InitializeComponent();
    public void Refresh(IReadOnlyList<ChatInfo> chats, string engine, bool ready, int documentCount)
    {
        Greeting.Text = DateTime.Now.Hour switch { < 12 => "Good morning.", < 18 => "Good afternoon.", _ => "Good evening." };
        EngineText.Text = engine;
        EngineStatus.Text = ready ? "Ready for your next idea." : "Configure your engine in Settings to begin.";
        KnowledgeText.Text = $"{documentCount} documents indexed";
        RecentChats.Children.Clear();
        foreach (var chat in chats.Where(c => c.Title != "New chat").Take(5))
        {
            var row = new Button { Content = chat.Title + "   ↗", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
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
    private void NewChat_Click(object? sender, RoutedEventArgs e) => NewChatRequested?.Invoke(this, e);
    private void QuickBar_Click(object? sender, RoutedEventArgs e) => QuickBarRequested?.Invoke(this, e);
    private void Routine_Click(object? sender, RoutedEventArgs e) => RoutineRequested?.Invoke(this, e);
    private void RoutineSettings_Click(object? sender, RoutedEventArgs e) => RoutineSettingsRequested?.Invoke(this, e);
    private void Engine_Click(object? sender, RoutedEventArgs e) => EngineRequested?.Invoke(this, e);
    private void Knowledge_Click(object? sender, RoutedEventArgs e) => KnowledgeRequested?.Invoke(this, e);
    private void Style_Click(object? sender, RoutedEventArgs e) => StyleRequested?.Invoke(this, e);
}
