using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace JARVIS;
public partial class MainWindow
{
    private ContentControl? _welcomeHost;
    private int _welcomeStep;
    private void ShowWelcome()
    {
        OpenFullDeck(); SettingsSplitView.IsPaneOpen = false;
        HomePage.IsVisible = false; ChatArea.IsVisible = false;
        if (_welcomeHost is null) { _welcomeHost = new ContentControl(); ((Grid)HomePage.Parent!).Children.Add(_welcomeHost); }
        _welcomeHost.IsVisible = true;
        var body = new StackPanel { Spacing = 18, Margin = new Thickness(32), MaxWidth = 760 };
        var steps = new[] { "Welcome", "Model", "Try it", "Shortcuts", "Ready" };
        body.Children.Add(new TextBlock { Text = string.Join("  →  ", steps.Select((s, i) => i == _welcomeStep ? "[ " + s + " ]" : s)), FontSize = 13 });
        body.Children.Add(new AssistantMark { Width = 100, Height = 100 });
        body.Children.Add(new TextBlock { Text = steps[_welcomeStep], FontSize = 32, FontWeight = FontWeight.SemiBold });
        void Button(string text, Action action) { var b = new Button { Content = text }; b.Click += (_, _) => action(); body.Children.Add(b); }
        if (_welcomeStep == 0)
        {
            body.Children.Add(new TextBlock { Text = "Your command center, ready when you are. What should I call you?", TextWrapping = TextWrapping.Wrap });
            var name = new TextBox { Text = _config.DisplayName, Watermark = "Your name (optional)", MaxLength = 80 };
            name.TextChanged += (_, _) => _config.DisplayName = name.Text?.Trim() ?? ""; body.Children.Add(name);
            var memory = new CheckBox { Content = "Save conversation memory on this device", IsChecked = _config.MemoryEnabled };
            memory.IsCheckedChanged += (_, _) => MemoryCheck.IsChecked = memory.IsChecked;
            body.Children.Add(memory);
        }
        if (_welcomeStep == 1)
        {
            body.Children.Add(new TextBlock { Text = "Local inference runs on your CPU. RAM estimates include working context and vary by model. Cloud providers receive the context you send. Downloads are optional.", TextWrapping = TextWrapping.Wrap });
            foreach (var model in ModelDownloadService.Curated)
            {
                var chosen = model;
                Button(model.Name + " · " + model.Note + (model.Name.StartsWith("Llama") ? " · ~2–3 GB RAM" : " · ~3–4 GB RAM"), () => { CuratedModelChoice.SelectedItem = chosen; DownloadGguf_Click(this, new RoutedEventArgs()); });
            }
            body.Children.Add(new TextBlock { Text = "Downloads continue in Settings → Engine. Review publisher licenses before use. " + _engine.LocalModels.Count + " models installed." });
            foreach (var model in _engine.LocalModels) { var name = model; Button("Use installed: " + name, () => { ProviderChoice.SelectedItem = "Local"; ModelComboBox.SelectedItem = name; }); }
            Button("Import my own GGUF", () => ImportGguf_Click(this, new RoutedEventArgs()));
            Button("Use a cloud provider / view download progress", () => OpenSettingsTab(0));
        }
        if (_welcomeStep == 2)
        {
            Button("Open the app launcher", ToggleQuickBar);
            Button("Start a focus timer", () => _ = StartWorkRoutineAsync());
            Button("Draft a desktop note", () => { NavigateConversation(); PromptBox.Text = "Help me draft a desktop note. Ask me what it should say before saving anything."; });
            Button("Test my model", () => { NavigateConversation(); PromptBox.Text = "Say hello and tell me one useful thing you can help with."; });
        }
        if (_welcomeStep == 3)
        {
            body.Children.Add(new TextBlock { Text = _config.QuickShortcut + "  quick launcher\n" + _config.DeckShortcut + "  full deck\nEnter sends · Shift+Enter adds a line", LineHeight = 30 });
            Button("Edit shortcuts and appearance", () => OpenSettingsTab(5));
        }
        if (_welcomeStep == 4) body.Children.Add(new TextBlock { Text = "Ready, " + (_config.DisplayName.Length > 0 ? _config.DisplayName : "friend") + ".\nEngine: " + _config.ActiveProvider + "\nMemory: " + (_config.MemoryEnabled ? "on" : "paused") + "\nYou can reopen this guide in About.", LineHeight = 28 });
        if (_welcomeStep > 0) Button("Back", () => { _welcomeStep--; ShowWelcome(); });
        Button(_welcomeStep == 4 ? "Finish — go to dashboard" : "Next", () => { SavePreferences(); if (_welcomeStep < 4) { _welcomeStep++; ShowWelcome(); } else { _config.SetupCompleted = true; SavePreferences(); NavigateHome(); } });
        Button("Skip setup", () => { _config.SetupDismissed = true; SavePreferences(); NavigateHome(); });
        _welcomeHost.Content = new ScrollViewer { Content = body };
        HomeTitle.Text = "Welcome"; Breadcrumb.Text = "Workspace / Setup";
    }
}
