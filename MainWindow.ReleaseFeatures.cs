using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace JARVIS;

public partial class MainWindow
{
    private ComboBox? _responseChoice;
    private void InitializeReleaseFeatures(bool interactive)
    {
        PromptBox.AddHandler(KeyDownEvent, PromptBox_KeyDown, RoutingStrategies.Tunnel);
        var style = (StackPanel)((ScrollViewer)((TabItem)SettingsControls.Items[5]!).Content!).Content!;
        var glass = new CheckBox { Content = "Desktop glass / blur", IsChecked = _config.GlassEnabled };
        glass.IsCheckedChanged += (_, _) => { _config.GlassEnabled = glass.IsChecked == true; ApplyAppearance(); SavePreferences(); };
        style.Children.Insert(0, glass);
        var quick = new TextBox { Text = _config.QuickShortcut, Watermark = "Quick bar shortcut" };
        var deck = new TextBox { Text = _config.DeckShortcut, Watermark = "Full deck shortcut" };
        var saveKeys = new Button { Content = "Save shortcuts" };
        saveKeys.Click += (_, _) =>
        {
            try
            {
                var q = GlobalHotkey.Parse(quick.Text ?? ""); var d = GlobalHotkey.Parse(deck.Text ?? "");
                if (q == d) throw new ArgumentException("Choose different shortcuts for the two windows.");
                _config.QuickShortcut = quick.Text!.Trim(); _config.DeckShortcut = deck.Text!.Trim();
                _hotkeys?.Dispose();
                if (OperatingSystem.IsWindows()) _hotkeys = new GlobalHotkey(ToggleQuickBar, ToggleFullDeck, HotkeyError, _config.QuickShortcut, _config.DeckShortcut);
                SavePreferences(); ShowNotice("Shortcuts saved. Registration conflicts appear in the status bar.");
            }
            catch (Exception ex) { ShowNotice(ex.Message); }
        };
        style.Children.Add(new TextBlock { Text = "Global shortcuts", FontWeight = FontWeight.SemiBold });
        style.Children.Add(quick); style.Children.Add(deck); style.Children.Add(saveKeys);
        _responseChoice = new ComboBox { ItemsSource = new[] { "Instant", "Quick", "Think more" }, SelectedItem = _config.ChatModes.GetValueOrDefault(_engine.ActiveChatId, _config.ResponseMode), MinWidth = 130 };
        _responseChoice.SelectionChanged += (_, _) => { if (_responseChoice.SelectedItem is string mode) { _config.ChatModes[_engine.ActiveChatId] = mode; _config.ResponseMode = mode; SavePreferences(); } };
        var composer = (Grid)PromptBox.Parent!;
        composer.RowDefinitions.Insert(0, new RowDefinition(GridLength.Auto));
        foreach (var child in composer.Children) Grid.SetRow(child, Grid.GetRow(child) + 1);
        composer.Children.Add(_responseChoice);
        var about = (StackPanel)((ScrollViewer)((TabItem)SettingsControls.Items[6]!).Content!).Content!;
        about.Children.Add(new TextBlock { Text = "Created by 3bitfun / tasqlab. Built with Avalonia, LLamaSharp, Markdig and the Model Context Protocol SDK. Font licenses ship with the app.", FontSize = 11, TextWrapping = TextWrapping.Wrap });
        var tutorial = new Button { Content = "Open welcome tutorial" };
        tutorial.Click += (_, _) => ShowWelcome(); about.Children.Add(tutorial);
        var knowledge = (StackPanel)((ScrollViewer)((TabItem)SettingsControls.Items[1]!).Content!).Content!;
        var summary = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 140, MaxLength = 4000, Watermark = "No compact summary for this chat yet." };
        var inspect = new Button { Content = "Read active chat summary" };
        inspect.Click += (_, _) => summary.Text = _engine.ContextSummary;
        var saveSummary = new Button { Content = "Save summary (empty resets compaction)" };
        saveSummary.Click += (_, _) => { if (_busy) { ShowNotice("Stop the current reply before editing context."); return; } _engine.EditSummary(summary.Text ?? ""); ShowNotice("Context summary updated."); };
        knowledge.Children.Add(new Expander { Header = "Active chat context summary", Content = new StackPanel { Spacing = 8, Children = { inspect, summary, saveSummary } } });
        ApplyAppearance();
        if (interactive) Opened += async (_, _) =>
        {
            if (!_config.SetupCompleted) { OpenFullDeck(); ShowWelcome(); }
            try
            {
                _availableUpdate = await _updater.CheckAsync(typeof(App).Assembly.GetName().Version!);
                if (_availableUpdate is not null)
                {
                    var banner = new Button { Content = $"Update {_availableUpdate.Version} available - open About", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Margin = new Thickness(0, 8) };
                    banner.Click += (_, _) => { OpenSettingsTab(6); CheckUpdate_Click(this, new RoutedEventArgs()); ((Grid)Content!).Children.Remove(banner); };
                    ((Grid)Content!).Children.Add(banner);
                }
            }
            catch (Exception) { /* Offline startup remains usable. */ }
        };
    }

    private void ApplyAppearance()
    {
        var solid = !_config.GlassEnabled || _config.Theme is "Minimalist" or "Terminal" or "GitHub Style" or "ChatGPT Style";
        var tint = (Color)Application.Current!.Resources["GlassTintColor"]!;
        var surface = new SolidColorBrush(tint);
        Background = solid ? surface : Brushes.Transparent;
        TransparencyLevelHint = solid ? new[] { WindowTransparencyLevel.None } : new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Transparent };
        var r = Application.Current.Resources;
        if (solid)
        {
            r["GlassMaterial"] = new ExperimentalAcrylicMaterial { TintColor = tint, TintOpacity = 1, MaterialOpacity = 1, FallbackColor = tint };
            r["GlassBrush"] = surface;
        }
        else ThemeManager.Apply(_config.Theme, _config.Accent, _config.Tint);
        var terminal = _config.Theme == "Terminal";
        DeckLayout.ColumnDefinitions[0].Width = new GridLength(_config.Theme switch { "Minimalist" => 190, "Terminal" => 180, "ChatGPT Style" => 250, "GitHub Style" => 260, _ => 240 });
        ChatLog.MaxWidth = _config.Theme switch { "ChatGPT Style" => 740, "Terminal" => 1100, "Minimalist" => 760, _ => 900 };
        ChatLog.Spacing = terminal ? 12 : _config.Theme == "ChatGPT Style" ? 32 : 22;
        r["RadiusMd"] = new CornerRadius(terminal || _config.Theme == "GitHub Style" ? 5 : _config.Theme == "Minimalist" ? 8 : 18);
        r["ButtonPadding"] = new Thickness(terminal ? 9 : 14, terminal ? 6 : 10);
        ThemeManager.ApplyFont(terminal ? "Cascadia Mono,Consolas" : _config.FontFamily);
        if (solid) r["PanelBrush"] = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));
    }

    private void ShowWelcome()
    {
        var body = new StackPanel { Spacing = 14, Margin = new Thickness(24) };
        var window = new Window { Title = "Welcome to J.A.R.V.I.S.", Width = 560, Height = 620, Content = new ScrollViewer { Content = body }, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        body.Children.Add(new TextBlock { Text = "Welcome to J.A.R.V.I.S.", FontSize = 26, FontWeight = FontWeight.SemiBold });
        var name = new TextBox { Watermark = "What should I call you? (optional)", Text = _config.DisplayName };
        body.Children.Add(name);
        window.Closing += (_, _) => { _config.DisplayName = name.Text?.Trim() ?? ""; SavePreferences(); };
        var greeting = new TextBlock { Text = "A little help, always nearby. You can skip setup and return from About.", TextWrapping = TextWrapping.Wrap };
        name.TextChanged += (_, _) => greeting.Text = "Welcome" + (string.IsNullOrWhiteSpace(name.Text) ? "!" : ", " + name.Text!.Trim() + "!");
        body.Children.Add(greeting);
        body.Children.Add(new TextBlock { Text = $"{_engine.LocalModels.Count} local models found. Local CPU inference needs RAM for model weights plus context; a 1-1.5B Q4 model is a useful starting point on an 8 GB machine. Larger models need more memory. Cloud requests send selected context to your provider.", TextWrapping = TextWrapping.Wrap });
        void Action(string text, Action action) { var button = new Button { Content = text, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch }; button.Click += (_, _) => { action(); }; body.Children.Add(button); }
        var modelChoice = new ComboBox { ItemsSource = ModelDownloadService.Curated, SelectedIndex = 0 };
        body.Children.Add(modelChoice);
        Action("Download selected GGUF", () => { CuratedModelChoice.SelectedItem = modelChoice.SelectedItem; window.Close(); OpenSettingsTab(0); DownloadGguf_Click(this, new RoutedEventArgs()); });
        Action("Download or import a GGUF / configure cloud", () => { window.Close(); OpenSettingsTab(0); });
        body.Children.Add(new TextBlock { Text = "Try J.A.R.V.I.S.", FontSize = 18 });
        Action("Launch an app", () => { window.Close(); ToggleQuickBar(); });
        Action("Start a focus timer", () => { window.Close(); _ = StartWorkRoutineAsync(); });
        Action("Draft a desktop note", () => { window.Close(); NewChat_Click(this, new RoutedEventArgs()); PromptBox.Text = "Help me draft a short note. Ask what it should say and where I want to save it before making a file."; PromptBox.Focus(); });
        Action("Customize shortcuts and appearance", () => { window.Close(); OpenSettingsTab(5); });
        var memory = new CheckBox { Content = "Save conversation memory", IsChecked = _config.MemoryEnabled }; body.Children.Add(memory);
        void Finish() { _config.DisplayName = name.Text?.Trim() ?? ""; _config.MemoryEnabled = memory.IsChecked == true; MemoryCheck.IsChecked = _config.MemoryEnabled; _config.SetupCompleted = true; SavePreferences(); window.Close(); }
        Action("Ready - save setup", Finish);
        Action("Skip setup", () => { _config.SetupCompleted = true; SavePreferences(); window.Close(); });
        window.Show(this);
    }
}
