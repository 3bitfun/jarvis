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
        HomePage.ConfigureDashboard(_config, SavePreferences, ShowWelcome);
        HomePage.WidgetData = id => id switch
        {
            "Focus timer" => _focusEndsAt is { } end && end > DateTime.Now ? $"Remaining: {(end - DateTime.Now):hh\\:mm\\:ss}" : "No timer running",
            "Recent chats" => string.Join("\n", _engine.Chats.Where(c => c.Title != "New chat").Take(4).Select(c => c.Title).DefaultIfEmpty("No recent conversations")),
            "Pinned apps" => string.Join("\n", _launcher.Apps.Where(_launcher.IsFavorite).Take(5).Select(a => a.Name).DefaultIfEmpty("No pinned apps · favorite apps in quick search")),
            "Model status" => _config.ActiveProvider == "Local" ? (_engine.ActiveModelPath is null ? "No model loaded" : "Loaded · " + Path.GetFileName(_engine.ActiveModelPath)) : _config.Providers.TryGetValue(_config.ActiveProvider, out var profile) && !string.IsNullOrWhiteSpace(profile.Model) ? _config.ActiveProvider + " · " + profile.Model + " (connection unchecked)" : _config.ActiveProvider + " · choose a model",
            "Notes" => NoteSummary(),
            _ => "Unavailable"
        };
        HomePage.ShortcutAction = name =>
        {
            switch (name)
            {
                case "New chat": NewChat_Click(this, new RoutedEventArgs()); break;
                case "Quick search": case "Pinned apps": ToggleQuickBar(); break;
                case "Focus timer": ShowNotice(TryPetAction("start a 25 minute timer") ?? "Couldn't start timer."); break;
                case "New note": ShowNoteEditor(); break;
            }
        };
        Application.Current!.Resources["VerityMode"] = _config.VerityMode;
        PromptBox.AddHandler(KeyDownEvent, PromptBox_KeyDown, RoutingStrategies.Tunnel);
        var style = (StackPanel)((ScrollViewer)((TabItem)SettingsControls.Items[5]!).Content!).Content!;
        var layoutOptions = new[] { "Original", "Modern", "Focus", "Command Center" };
        var layoutChoice = new ComboBox { ItemsSource = layoutOptions, SelectedItem = layoutOptions.Contains(_config.Layout) ? _config.Layout : "Original" };
        layoutChoice.SelectionChanged += (_, _) => { _config.Layout = layoutChoice.SelectedItem as string ?? "Original"; ApplyAppearance(); ShowConversation(); SavePreferences(); };
        var stark = new Button { Content = "Apply Stark-inspired J.A.R.V.I.S. preset" };
        stark.Click += (_, _) => { _config.Theme = "Stark HUD"; ThemeChoice.SelectedItem = "Stark HUD"; layoutChoice.SelectedItem = "Command Center"; ApplyTheme(); SavePreferences(); };
        style.Children.Insert(0, new TextBlock { Text = "Layout and appearance", FontSize = 16, FontWeight = FontWeight.SemiBold });
        style.Children.Insert(1, new TextBlock { Text = "Layout changes the arrangement. Choose a separate color theme below.", TextWrapping = TextWrapping.Wrap, FontSize = 11 });
        style.Children.Insert(2, layoutChoice); style.Children.Insert(3, stark);
        var previews = new WrapPanel();
        foreach (var name in layoutOptions)
        {
            var preview = new Button { Content = name + "\n▌  ▬▬▬\n▌  ▬ ▬", Width = 122, Height = 82, Margin = new Thickness(0, 0, 8, 8) };
            preview.Click += (_, _) => layoutChoice.SelectedItem = name; previews.Children.Add(preview);
        }
        style.Children.Insert(4, previews);
        var dimensions = new StackPanel { Spacing = 8 };
        void SizeControl(string label, int min, int max, int current, Action<int> set)
        {
            dimensions.Children.Add(new TextBlock { Text = label });
            var value = new NumericUpDown { Minimum = min, Maximum = max, Increment = 10, Value = Math.Clamp(current, min, max) };
            value.ValueChanged += (_, _) => { set((int)(value.Value ?? min)); ApplyAppearance(); SavePreferences(); }; dimensions.Children.Add(value);
        }
        SizeControl("Sidebar width", 180, 400, _config.SidebarWidth, v => _config.SidebarWidth = v);
        SizeControl("Chat width", 500, 1400, _config.ChatWidth, v => _config.ChatWidth = v);
        SizeControl("Message spacing", 0, 60, _config.MessageSpacing, v => _config.MessageSpacing = v);
        dimensions.Children.Add(new TextBlock { Text = "HUD animation intensity" });
        var intensity = new NumericUpDown { Minimum = 0, Maximum = 2, Increment = .25m, Value = (decimal)Math.Clamp(_config.HudAnimationIntensity, 0, 2) };
        intensity.ValueChanged += (_, _) => { _config.HudAnimationIntensity = (double)(intensity.Value ?? 1); ApplyAppearance(); SavePreferences(); }; dimensions.Children.Add(intensity);
        var hideSidebar = new CheckBox { Content = "Hide sidebar (open settings from the top bar)", IsChecked = _config.HideSidebar };
        hideSidebar.IsCheckedChanged += (_, _) => { _config.HideSidebar = hideSidebar.IsChecked == true; ApplyAppearance(); SavePreferences(); }; dimensions.Children.Add(hideSidebar);
        style.Children.Insert(5, new Expander { Header = "Layout dimensions", Content = dimensions });
        var glass = new CheckBox { Content = "Desktop glass / blur", IsChecked = _config.GlassEnabled };
        glass.IsCheckedChanged += (_, _) => { _config.GlassEnabled = glass.IsChecked == true; ApplyAppearance(); SavePreferences(); };
        style.Children.Insert(6, glass);
        var square = new CheckBox { Content = "Square corners in full screen", IsChecked = _config.SquareFullscreen };
        square.IsCheckedChanged += (_, _) => { _config.SquareFullscreen = square.IsChecked == true; ApplyWindowCorners(); SavePreferences(); };
        style.Children.Insert(7, square);
        var colors = new WrapPanel();
        foreach (var (label, color) in new[] { ("Cyan", "#43DCEE"), ("Blue", "#75BFFF"), ("Violet", "#C8A3FF"), ("Amber", "#F3BC66"), ("Rose", "#EF8EA8"), ("Lime", "#D5E9A4") })
        {
            var preset = new Button { Content = label, Margin = new Thickness(0, 0, 6, 6) };
            preset.Click += (_, _) => { ThemeChoice.SelectedItem = "Custom"; AccentPicker.Color = Color.Parse(color); }; colors.Children.Add(preset);
        }
        var contrast = new CheckBox { Content = "High text contrast", IsChecked = _config.HighTextContrast };
        contrast.IsCheckedChanged += (_, _) => { _config.HighTextContrast = contrast.IsChecked == true; ApplyTheme(); SavePreferences(); };
        style.Children.Insert(7, new TextBlock { Text = "Accent presets (custom background below)" }); style.Children.Insert(8, colors); style.Children.Insert(9, contrast);
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
            if (!_config.SetupCompleted && !_config.SetupDismissed) { OpenFullDeck(); ShowWelcome(); }
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

    private string NoteSummary()
    {
        try { var folder = Path.Combine(_config.Workspace, "notes"); return !Directory.Exists(folder) ? "No notes yet" : string.Join("\n", Directory.EnumerateFiles(folder, "*.txt").OrderByDescending(File.GetLastWriteTimeUtc).Take(4).Select(Path.GetFileName).DefaultIfEmpty("No notes yet")); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return "Notes unavailable"; }
    }
    private void ShowNoteEditor()
    {
        var input = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 140, Watermark = "Write your note…" };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var save = new Button { Content = "Save note" };
        var window = new Window { Title = "New note", Width = 460, Height = 300, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new StackPanel { Spacing = 12, Margin = new Thickness(20), Children = { input, save, status } } };
        save.Click += (_, _) => { if (string.IsNullOrWhiteSpace(input.Text)) { status.Text = "Write a note first."; return; } var result = TryPetAction("make a note: " + input.Text); if (result?.StartsWith("Saved your note", StringComparison.Ordinal) == true) { window.Close(); ShowNotice(result); } else status.Text = result ?? "Could not save the note."; };
        window.Show(this); input.Focus();
    }

    private bool SolidAppearance => !_config.GlassEnabled || _config.Theme is "Minimalist" or "Terminal" or "GitHub Style" or "ChatGPT Style";
    private void ApplyAppearance()
    {
        if (_config.VerityMode) ThemeManager.Apply("Custom", "#FFD447", "#201C10");
        var solid = SolidAppearance;
        if (solid) GlassStatus.Text = "Solid surface · blur and Mica disabled.";
        _mini?.SetGlass(_config.GlassEnabled);
        if (_petBubble is not null && _config.PetBubbleStyle == "Frosted glass")
        {
            _petBubble.TransparencyLevelHint = _config.GlassEnabled ? new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Transparent } : new[] { WindowTransparencyLevel.Transparent };
            if (_petBubble.Content is StackPanel bubble)
            {
                var fill = Brush.Parse(_config.GlassEnabled ? "#A622262C" : "#FF17191D");
                foreach (var card in bubble.Children.OfType<Border>()) card.Background = fill;
                foreach (var tail in bubble.Children.OfType<Avalonia.Controls.Shapes.Polygon>()) tail.Fill = fill;
            }
        }
        var tint = (Color)Application.Current!.Resources["GlassTintColor"]!;
        var surface = new SolidColorBrush(tint);
        Background = solid ? surface : Brushes.Transparent;
        DeckAcrylic.IsVisible = DeckGlassHighlight.IsVisible = !solid;
        TransparencyLevelHint = solid ? new[] { WindowTransparencyLevel.None } : new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Transparent };
        var r = Application.Current.Resources;
        if (solid)
        {
            r["GlassMaterial"] = new ExperimentalAcrylicMaterial { TintColor = tint, TintOpacity = 1, MaterialOpacity = 1, FallbackColor = tint };
            r["GlassBrush"] = surface;
        }
        else ThemeManager.Apply(_config.VerityMode ? "Custom" : _config.Theme, _config.VerityMode ? "#FFD447" : _config.Accent, _config.VerityMode ? "#201C10" : _config.Tint);
        var terminal = _config.Theme == "Terminal";
        DeckLayout.ColumnDefinitions[0].Width = new GridLength(_config.Theme switch { "Minimalist" => 190, "Terminal" => 180, "ChatGPT Style" => 250, "GitHub Style" => 260, _ => 240 });
        ChatLog.MaxWidth = _config.Theme switch { "ChatGPT Style" => 740, "Terminal" => 1100, "Minimalist" => 760, _ => 900 };
        ChatLog.Spacing = terminal ? 12 : _config.Theme == "ChatGPT Style" ? 32 : 22;
        r["RadiusMd"] = new CornerRadius(terminal || _config.Theme == "GitHub Style" ? 5 : _config.Theme == "Minimalist" ? 8 : 18);
        r["ButtonPadding"] = new Thickness(terminal ? 9 : 14, terminal ? 6 : 10);
        ThemeManager.ApplyFont(terminal ? "Cascadia Mono,Consolas" : _config.FontFamily);
        if (solid) r["PanelBrush"] = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));
        if (_config.HighTextContrast) { r["TextBrush"] = Brushes.White; r["MutedBrush"] = new SolidColorBrush(Color.Parse("#E0E5E7")); r["SecondaryBrush"] = r["MutedBrush"]; }
        ApplyDeckLayout();
        ApplyWindowCorners();
    }

    private void ApplyWindowCorners()
    {
        var radius = WindowState == WindowState.FullScreen && _config.SquareFullscreen ? 0 : _config.Layout == "Command Center" ? 8 : _config.Layout == "Original" ? 24 : 14;
        ExpandedView.CornerRadius = DeckAcrylic.CornerRadius = DeckGlassHighlight.CornerRadius = new CornerRadius(radius);
    }

    private void ApplyDeckLayout()
    {
        var layout = _config.Layout;
        HomePage.ApplyLayout(layout);
        DeckSidebar.IsVisible = !_config.HideSidebar;
        if (layout == "Original") { ChatComposer.MaxWidth = 900; ChatArea.Margin = new Thickness(26, 0); PromptDropZone.CornerRadius = new CornerRadius(14); DeckHeader.Padding = new Thickness(24, 10); ExpandedView.CornerRadius = new CornerRadius(24); DeckLayout.ColumnDefinitions[0].Width = new GridLength(_config.HideSidebar ? 0 : Math.Clamp(_config.SidebarWidth, 180, 400)); ChatLog.MaxWidth = ChatComposer.MaxWidth = Math.Clamp(_config.ChatWidth, 500, 1400); ChatLog.Spacing = Math.Clamp(_config.MessageSpacing, 0, 60); ApplyTerminalLayout(); return; }
        DeckLayout.ColumnDefinitions[0].Width = new GridLength(layout switch { "Modern" => 280, "Focus" => 210, "Command Center" => 256, _ => 240 });
        ChatLog.MaxWidth = layout switch { "Modern" => 800, "Focus" => 680, "Command Center" => 1040, _ => 900 };
        ChatComposer.MaxWidth = ChatLog.MaxWidth;
        ChatLog.Spacing = layout == "Command Center" ? 12 : 28;
        ChatArea.Margin = new Thickness(layout == "Focus" ? 44 : 26, 0);
        DeckHeader.Padding = new Thickness(layout == "Modern" ? 24 : 18, 8);
        ExpandedView.CornerRadius = new CornerRadius(layout == "Command Center" ? 8 : 14);
        PromptDropZone.CornerRadius = new CornerRadius(layout == "Modern" ? 22 : layout == "Command Center" ? 6 : 14);
        DeckLayout.ColumnDefinitions[0].Width = new GridLength(_config.HideSidebar ? 0 : Math.Clamp(_config.SidebarWidth, 180, 400));
        ChatLog.MaxWidth = ChatComposer.MaxWidth = Math.Clamp(_config.ChatWidth, 500, 1400);
        ChatLog.Spacing = Math.Clamp(_config.MessageSpacing, 0, 60);
        ApplyTerminalLayout();
    }

    private void ApplyTerminalLayout()
    {
        var terminal = _config.Theme == "Terminal";
        Grid.SetColumn(DeckSidebar, terminal ? 1 : 0); Grid.SetColumn(DeckContent, terminal ? 0 : 1);
        DeckLayout.ColumnDefinitions[0].Width = terminal ? new GridLength(1, GridUnitType.Star) : new GridLength(_config.HideSidebar ? 0 : Math.Clamp(_config.SidebarWidth, 180, 400));
        DeckLayout.ColumnDefinitions[1].Width = terminal ? new GridLength(_config.HideSidebar ? 0 : Math.Clamp(_config.SidebarWidth, 180, 400)) : new GridLength(1, GridUnitType.Star);
        DeckSidebar.BorderThickness = terminal ? new Thickness(1, 0, 0, 0) : new Thickness(0, 0, 1, 0);
        if (terminal) { ChatArea.Margin = new Thickness(18, 0); ChatLog.Spacing = 12; PromptDropZone.CornerRadius = new CornerRadius(2); }
    }

    internal void CheckLayoutSizing()
    {
        foreach (var layout in new[] { "Original", "Modern", "Focus", "Command Center" })
        foreach (var glass in new[] { false, true })
        foreach (var size in new[] { new Size(800, 600), new Size(1280, 800), new Size(1920, 1080) })
        {
            _config.Layout = layout; _config.GlassEnabled = glass; ApplyAppearance();
            HomePage.Measure(size); HomePage.Arrange(new Rect(size));
            if (!double.IsFinite(HomePage.Bounds.Width) || HomePage.Bounds.Width <= 0) throw new InvalidOperationException("Home layout failed for " + layout);
        }
        _config.Theme = "Obsidian Glass"; _config.GlassEnabled = false; ApplyAppearance();
        if (DeckAcrylic.IsVisible || DeckGlassHighlight.IsVisible || TransparencyLevelHint[0] != WindowTransparencyLevel.None) throw new InvalidOperationException("Solid mode left an acrylic layer enabled.");
        _config.Theme = "Terminal"; ApplyAppearance();
        if (Grid.GetColumn(DeckSidebar) != 1 || Grid.GetColumn(DeckContent) != 0) throw new InvalidOperationException("Terminal layout did not move the sidebar.");
        _config.SquareFullscreen = true; WindowState = WindowState.FullScreen; ApplyWindowCorners();
        if (ExpandedView.CornerRadius.TopLeft != 0 || DeckAcrylic.CornerRadius.TopLeft != 0) throw new InvalidOperationException("Full screen corners are not square.");
        WindowState = WindowState.Normal;
    }

}
