using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Collections;
using Polygon = Avalonia.Controls.Shapes.Polygon;

namespace JARVIS;

public partial class MainWindow
{
    private Window? _desktopPet;
    private DispatcherTimer? _petStatusTimer;
    private CheckBox? _petEnabled;
    private AssistantMark? _petMark;
    private Image? _petImage;
    private Bitmap? _petBitmap;
    private Window? _petBubble;
    private MarkdownChat? _petBubbleMarkdown;
    private DispatcherTimer? _bubbleTimer;
    private AssistantMark? _petPreview;
    private bool _bubbleHovered;
    private TextBlock? _petStreamText;

    private void ShowPetReply(string answer, bool streaming = false)
    {
        if (_desktopPet is not { IsVisible: true } pet || string.IsNullOrWhiteSpace(answer)) return;
        _petBubble?.Close();
        _petBubbleMarkdown?.Dispose();
        var styleName = _config.PetBubbleStyle;
        var light = styleName == "White";
        var glass = styleName == "Frosted glass";
        var foreground = light ? Brushes.Black : Brushes.White;
        var fill = light ? Brush.Parse("#FFF9F9F7") : glass ? Brush.Parse("#A622262C") : Brush.Parse("#FF17191D");
        var markdown = new MarkdownChat(_config.PetBubbleFontSize, foreground); _petBubbleMarkdown = markdown;
        var close = new Button { Content = "Close" };
        var deck = new Button { Content = "Open conversation" };
        var reply = new Button { Content = "Reply" };
        var stop = new Button { Content = "Stop", IsVisible = streaming };
        stop.Click += (_, _) => _generation?.Cancel();
        var copy = new Button { Content = "Copy" };
        copy.Click += async (_, _) => { try { if (TopLevel.GetTopLevel(copy)?.Clipboard is { } clipboard) { await clipboard.SetTextAsync(answer); copy.Content = "Copied"; } } catch (Exception) { copy.Content = "Unavailable"; } };
        var actions = new WrapPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { stop, reply, deck, copy, close } };
        _petStreamText = streaming ? new TextBlock { Text = answer, Foreground = foreground, TextWrapping = TextWrapping.Wrap, FontSize = Math.Clamp(_config.PetBubbleFontSize, 10, 24) } : null;
        var content = new StackPanel { Spacing = 10, Children = { new TextBlock { Text = _config.VerityMode ? "Verity" : "J.A.R.V.I.S.", Foreground = foreground, FontWeight = FontWeight.SemiBold }, new ScrollViewer { MaxHeight = Math.Clamp(_config.PetBubbleHeight, 120, 600), Content = (Control?)_petStreamText ?? markdown.Render(answer) }, actions } };
        var card = new Border { Background = fill, BorderBrush = light ? Brushes.LightGray : Brushes.Gray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(16), Child = content };
        var tail = new Polygon { Fill = fill, Width = 24, Height = 12, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Points = new AvaloniaList<Point> { new(0, 0), new(24, 0), new(12, 12) } };
        var layout = new StackPanel { Children = { card, tail } };
        var bubble = new Window { Title = "J.A.R.V.I.S. reply", Width = Math.Clamp(_config.PetBubbleWidth, 260, 700), SizeToContent = SizeToContent.Height, SystemDecorations = SystemDecorations.None, ShowInTaskbar = false, ShowActivated = false, Topmost = true, CanResize = false, Background = Brushes.Transparent, RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark, TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent }, Content = layout };
        if (glass) GlassBackdrop.Configure(bubble);
        _petBubble = bubble;
        close.Click += (_, _) => bubble.Close();
        reply.Click += (_, _) => { bubble.Close(); ToggleQuickBar(); };
        deck.Click += (_, _) => { bubble.Close(); OpenFullDeck(); NavigateConversation(); };
        bubble.Transitions = new Avalonia.Animation.Transitions { new Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(350) } };
        bubble.PointerEntered += (_, _) => { _bubbleHovered = true; bubble.Opacity = 1; };
        bubble.PointerExited += (_, _) => _bubbleHovered = false;
        _bubbleHovered = false; _bubbleTimer?.Stop();
        if (!streaming && _config.PetBubbleSeconds > 0)
        {
            var remaining = Math.Clamp(_config.PetBubbleSeconds, 5, 300);
            _bubbleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _bubbleTimer.Tick += (_, _) => { if (_bubbleHovered || bubble.IsKeyboardFocusWithin) return; remaining--; if (remaining <= 1 && !_config.PetReducedMotion) bubble.Opacity = .25; if (remaining <= 0) bubble.Close(); };
            _bubbleTimer.Start();
        }
        bubble.Closed += (_, _) => { if (ReferenceEquals(_petBubble, bubble)) { _bubbleTimer?.Stop(); _petBubble = null; _petStreamText = null; _petBubbleMarkdown?.Dispose(); _petBubbleMarkdown = null; HideTemporaryPetLater(); } };
        var screen = Screens.ScreenFromWindow(pet) ?? Screens.Primary;
        var area = screen?.WorkingArea ?? new PixelRect(0, 0, 1280, 720); var scale = screen?.Scaling ?? 1;
        bubble.Width = Math.Min(bubble.Width, Math.Max(200, area.Width / scale - 16));
        bubble.Show();
        Dispatcher.UIThread.Post(() =>
        {
            if (!bubble.IsVisible || !pet.IsVisible) return;
            var width = (int)(bubble.Bounds.Width * scale); var height = (int)(bubble.Bounds.Height * scale);
            var x = Math.Clamp(pet.Position.X + (int)(pet.Bounds.Width * scale / 2) - width / 2, area.X, Math.Max(area.X, area.Right - width));
            var above = pet.Position.Y - height - (int)(4 * scale);
            if (above < area.Y)
            {
                layout.Children.Remove(tail); tail.Points = new AvaloniaList<Point> { new(0, 12), new(24, 12), new(12, 0) }; layout.Children.Insert(0, tail);
            }
            var y = above >= area.Y ? above : pet.Position.Y + (int)(pet.Bounds.Height * scale) + (int)(4 * scale);
            bubble.Position = new PixelPoint(x, Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - height)));
        }, DispatcherPriority.Loaded);
    }

    private void InitializeDesktopPet(bool interactive)
    {
        var style = (StackPanel)((ScrollViewer)((TabItem)SettingsControls.Items[5]!).Content!).Content!;
        style.Children.Add(new TextBlock { Text = "Pet Studio", FontSize = 18, FontWeight = FontWeight.SemiBold });
        _petPreview = new AssistantMark { Width = Math.Clamp(_config.PetSize, 64, 180), Height = Math.Clamp(_config.PetSize, 64, 180), AnimationsEnabled = _config.PetAnimations && !_config.PetReducedMotion, AnimationSpeed = _config.PetAnimationSpeed, AnimationIntensity = _config.PetAnimationIntensity, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        style.Children.Add(_petPreview);
        style.Children.Add(new TextBlock { Text = "Companion" });
        var character = new ComboBox { ItemsSource = new[] { "J.A.R.V.I.S.", "Verity" }, SelectedItem = _config.VerityMode ? "Verity" : "J.A.R.V.I.S." };
        character.SelectionChanged += (_, _) =>
        {
            _config.VerityMode = (character.SelectedItem as string) == "Verity";
            if (Application.Current is not null) Application.Current.Resources["VerityMode"] = _config.VerityMode;
            ThemeManager.Apply(_config.Theme, _config.Accent, _config.Tint); ApplyAppearance(); SavePreferences();
            _petPreview?.InvalidateVisual(); _petMark?.InvalidateVisual();
        };
        style.Children.Add(character);
        _petEnabled = new CheckBox { Content = "Show desktop pet", IsChecked = _config.PetEnabled };
        _petEnabled.IsCheckedChanged += (_, _) => { _config.PetEnabled = _petEnabled.IsChecked == true; SavePreferences(); if (interactive) UpdateDesktopPet(); };
        style.Children.Add(_petEnabled);
        var size = new NumericUpDown { Minimum = 64, Maximum = 180, Increment = 8, Value = Math.Clamp(_config.PetSize, 64, 180) };
        size.ValueChanged += (_, _) => { _config.PetSize = (int)(size.Value ?? 96); SavePreferences(); if (interactive) UpdateDesktopPet(); };
        style.Children.Add(new TextBlock { Text = "Pet size" }); style.Children.Add(size);
        var sizeSlider = new Slider { Minimum = 64, Maximum = 180, Value = Math.Clamp(_config.PetSize, 64, 180), TickFrequency = 1 };
        sizeSlider.ValueChanged += (_, _) => size.Value = (decimal)Math.Round(sizeSlider.Value);
        size.ValueChanged += (_, _) => { sizeSlider.Value = (double)(size.Value ?? 96); if (_petPreview is not null) _petPreview.Width = _petPreview.Height = Math.Clamp((double)(size.Value ?? 96), 64, 180); };
        style.Children.Add(sizeSlider);
        var presets = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        foreach (var (label, value) in new[] { ("Small", 72), ("Medium", 112), ("Large", 168) }) { var button = new Button { Content = label }; button.Click += (_, _) => size.Value = value; presets.Children.Add(button); }
        style.Children.Add(presets);
        var opacity = new NumericUpDown { Minimum = .3m, Maximum = 1m, Increment = .1m, Value = (decimal)Math.Clamp(_config.PetOpacity, .3, 1) };
        opacity.ValueChanged += (_, _) => { _config.PetOpacity = (double)(opacity.Value ?? 1); SavePreferences(); if (interactive) UpdateDesktopPet(); };
        style.Children.Add(new TextBlock { Text = "Pet opacity" }); style.Children.Add(opacity);
        var personality = new ComboBox { ItemsSource = new[] { "Professional", "Calm", "Playful" }, SelectedItem = _config.PetPersonality };
        personality.SelectionChanged += (_, _) => { _config.PetPersonality = personality.SelectedItem as string ?? "Professional"; SavePreferences(); };
        style.Children.Add(new TextBlock { Text = "Companion personality" }); style.Children.Add(personality);
        var animations = new CheckBox { Content = "Animate pet", IsChecked = _config.PetAnimations };
        animations.IsCheckedChanged += (_, _) => { _config.PetAnimations = animations.IsChecked == true; SavePreferences(); UpdateDesktopPet(); };
        style.Children.Add(animations);
        var reduced = new CheckBox { Content = "Reduced motion", IsChecked = _config.PetReducedMotion };
        reduced.IsCheckedChanged += (_, _) => { _config.PetReducedMotion = reduced.IsChecked == true; SavePreferences(); if (interactive) UpdateDesktopPet(); }; style.Children.Add(reduced);
        void Number(string label, decimal min, decimal max, decimal step, decimal initial, Action<decimal> update)
        {
            style.Children.Add(new TextBlock { Text = label });
            var control = new NumericUpDown { Minimum = min, Maximum = max, Increment = step, Value = Math.Clamp(initial, min, max) };
            control.ValueChanged += (_, _) => { if (control.Value is { } value) { update(value); SavePreferences(); if (interactive) UpdateDesktopPet(); } };
            style.Children.Add(control);
        }
        Number("Animation speed", .25m, 3m, .25m, (decimal)_config.PetAnimationSpeed, v => _config.PetAnimationSpeed = (double)v);
        Number("Animation intensity", 0, 2, .25m, (decimal)_config.PetAnimationIntensity, v => _config.PetAnimationIntensity = (double)v);
        Number("Speech bubble width", 260, 700, 20, _config.PetBubbleWidth, v => _config.PetBubbleWidth = (int)v);
        Number("Speech bubble maximum height", 120, 600, 20, _config.PetBubbleHeight, v => _config.PetBubbleHeight = (int)v);
        Number("Speech bubble text size", 10, 24, 1, _config.PetBubbleFontSize, v => _config.PetBubbleFontSize = (int)v);
        Number("Bubble duration in seconds (0 keeps it open)", 0, 300, 5, _config.PetBubbleSeconds, v => _config.PetBubbleSeconds = (int)v);
        style.Children.Add(new TextBlock { Text = "Speech bubble background" });
        var bubbleStyle = new ComboBox { ItemsSource = new[] { "Frosted glass", "White", "Black" }, SelectedItem = new[] { "Frosted glass", "White", "Black" }.Contains(_config.PetBubbleStyle) ? _config.PetBubbleStyle : "Frosted glass" };
        bubbleStyle.SelectionChanged += (_, _) => { _config.PetBubbleStyle = bubbleStyle.SelectedItem as string ?? "Frosted glass"; SavePreferences(); };
        style.Children.Add(bubbleStyle);
        var snap = new CheckBox { Content = "Snap pet to nearest screen edge after dragging", IsChecked = _config.PetSnapToEdge };
        snap.IsCheckedChanged += (_, _) => { _config.PetSnapToEdge = snap.IsChecked == true; SavePreferences(); }; style.Children.Add(snap);
        var compact = new CheckBox { Content = "Compact quick bar", IsChecked = _config.CompactQuickBar };
        compact.IsCheckedChanged += (_, _) => { _config.CompactQuickBar = compact.IsChecked == true; SavePreferences(); }; style.Children.Add(compact);
        var topmost = new CheckBox { Content = "Keep pet above other windows", IsChecked = _config.PetAlwaysOnTop };
        topmost.IsCheckedChanged += (_, _) => { _config.PetAlwaysOnTop = topmost.IsChecked == true; SavePreferences(); if (interactive) UpdateDesktopPet(); }; style.Children.Add(topmost);
        var bubblePreview = new Button { Content = "Preview speech bubble" };
        bubblePreview.Click += (_, _) => { if (_desktopPet is not { IsVisible: true }) { ShowNotice("Enable the desktop pet to preview its speech bubble."); return; } ShowPetReply("**Hello!** I'm ready to help.\n\n- Ask me a question\n- Search for an app\n- Open the full conversation"); }; style.Children.Add(bubblePreview);
        var previewButton = new Button { Content = "Preview animation states" };
        previewButton.Click += async (_, _) => { previewButton.IsEnabled = false; foreach (var state in new[] { "Idle", "Listening", "Thinking", "Speaking", "Success", "Error" }) { if (_petPreview is null) break; _petPreview.Activity = state; previewButton.Content = "Preview: " + state; await Task.Delay(1300); } if (_petPreview is not null) _petPreview.Activity = "Idle"; previewButton.Content = "Preview animation states"; previewButton.IsEnabled = true; };
        style.Children.Add(previewButton);
        var imageButton = new Button { Content = "Choose pet image (PNG or JPEG)" };
        imageButton.Click += async (_, _) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choose pet image", AllowMultiple = false, FileTypeFilter = new[] { new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } } } });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (path is null) return;
            try { using var check = new Bitmap(path); _config.PetImagePath = path; SavePreferences(); UpdateDesktopPet(); }
            catch (Exception ex) { ShowNotice("Could not load pet image: " + ex.Message); }
        };
        style.Children.Add(imageButton);
        var resetImage = new Button { Content = "Use built-in pet" };
        resetImage.Click += (_, _) => { _config.PetImagePath = ""; SavePreferences(); UpdateDesktopPet(); }; style.Children.Add(resetImage);
        style.Children.Add(new TextBlock { Text = "Drag to move. Click for quick chat; right-click for controls. The pet follows the active persona.", TextWrapping = TextWrapping.Wrap, FontSize = 11 });
        if (!interactive) return;
        Opened += (_, _) => UpdateDesktopPet();
        Closed += (_, _) => { _petStatusTimer?.Stop(); _petBubble?.Close(); _desktopPet?.Close(); _petBitmap?.Dispose(); };
    }

    private void UpdateDesktopPet(bool force = false)
    {
        if (_petPreview is not null) { _petPreview.AnimationsEnabled = _config.PetAnimations && !_config.PetReducedMotion; _petPreview.AnimationSpeed = _config.PetAnimationSpeed; _petPreview.AnimationIntensity = _config.PetAnimationIntensity; }
        if (!_config.PetEnabled && !force) { _petBubble?.Close(); _desktopPet?.Hide(); return; }
        if (_desktopPet is null)
        {
            var status = new TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, FontSize = 10 };
            _petMark = new AssistantMark { Width = 68, Height = 68, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            _petImage = new Image { Width = 68, Height = 68, Stretch = Stretch.Uniform, IsVisible = false };
            var layout = new StackPanel { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Children = { _petMark, _petImage, status } };
            var pet = new Window { Title = "J.A.R.V.I.S. pet", SystemDecorations = SystemDecorations.None, Background = Brushes.Transparent, TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent }, Topmost = true, ShowInTaskbar = false, CanResize = false, Content = layout };
            _desktopPet = pet;
            var previousPosition = pet.Position;
            pet.PositionChanged += (_, _) =>
            {
                var next = pet.Position; var dx = next.X - previousPosition.X; var dy = next.Y - previousPosition.Y; previousPosition = next;
                if (_petBubble is not { IsVisible: true } current) return;
                var area = (Screens.ScreenFromWindow(pet) ?? Screens.Primary)?.WorkingArea ?? new PixelRect(0, 0, 1280, 720);
                var scale = (Screens.ScreenFromWindow(pet) ?? Screens.Primary)?.Scaling ?? 1;
                current.Position = new PixelPoint(Math.Clamp(current.Position.X + dx, area.X, Math.Max(area.X, area.Right - (int)(current.Bounds.Width * scale))), Math.Clamp(current.Position.Y + dy, area.Y, Math.Max(area.Y, area.Bottom - (int)(current.Bounds.Height * scale))));
            };
            var menu = new ContextMenu();
            void Item(string label, Action action) { var item = new MenuItem { Header = label }; item.Click += (_, _) => action(); menu.Items.Add(item); }
            Item("Quick chat", ToggleQuickBar); Item("Open full deck", OpenFullDeck);
            Item("Engine and models", () => { OpenFullDeck(); OpenSettingsTab(0); });
            Item("Pause / resume memory", () => { if (!_busy) MemoryCheck.IsChecked = !_config.MemoryEnabled; });
            Item("Stop current response", () => _generation?.Cancel());
            Item("Start a new chat", () => { if (_busy) return; NewChat_Click(this, new RoutedEventArgs()); OpenFullDeck(); });
            Item("Move pet to screen corner", () =>
            {
                var area = (Screens.ScreenFromWindow(pet) ?? Screens.Primary)?.WorkingArea ?? new PixelRect(0, 0, 1280, 720);
                var scale = (Screens.ScreenFromWindow(pet) ?? Screens.Primary)?.Scaling ?? 1;
                pet.Position = new PixelPoint(Math.Max(area.X, area.Right - (int)(pet.Width * scale) - 16), Math.Max(area.Y, area.Bottom - (int)(pet.Height * scale) - 16));
                _config.PetX = pet.Position.X; _config.PetY = pet.Position.Y; SavePreferences();
            });
            Item("Pet settings", () => { OpenFullDeck(); OpenSettingsTab(5); });
            Item("Hide pet", () => { if (_petEnabled is not null) _petEnabled.IsChecked = false; });
            pet.ContextMenu = menu;
            PixelPoint start = default, pointerStart = default; bool dragging = false;
            pet.PointerPressed += (_, e) => { if (!e.GetCurrentPoint(pet).Properties.IsLeftButtonPressed) return; start = pet.Position; pointerStart = pet.PointToScreen(e.GetPosition(pet)); dragging = true; e.Pointer.Capture(pet); e.Handled = true; };
            pet.PointerMoved += (_, e) => { if (!dragging) return; var current = pet.PointToScreen(e.GetPosition(pet)); pet.Position = new PixelPoint(start.X + current.X - pointerStart.X, start.Y + current.Y - pointerStart.Y); };
            pet.PointerReleased += (_, e) => { if (!dragging) return; dragging = false; e.Pointer.Capture(null); if (Math.Abs(pet.Position.X - start.X) < 4 && Math.Abs(pet.Position.Y - start.Y) < 4) ToggleQuickBar(); else { if (_config.PetSnapToEdge) SnapPetToEdge(pet); _config.PetX = pet.Position.X; _config.PetY = pet.Position.Y; SavePreferences(); } e.Handled = true; };
            _petStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            var wasBusy = false; var reactionUntil = DateTime.MinValue; var reaction = "Success";
            _petStatusTimer.Tick += (_, _) =>
            {
                if (!pet.IsVisible) return;
                if (!dragging && !Screens.All.Any(s => s.WorkingArea.Contains(pet.Position)))
                {
                    var primary = Screens.Primary;
                    if (primary is not null) { var area = primary.WorkingArea; pet.Position = new PixelPoint(Math.Max(area.X, area.Right - (int)(pet.Width * primary.Scaling) - 16), Math.Max(area.Y, area.Bottom - (int)(pet.Height * primary.Scaling) - 16)); _config.PetX = pet.Position.X; _config.PetY = pet.Position.Y; SavePreferences(); }
                }
                if (wasBusy && !_busy) { reaction = (StatusText.Text ?? "").Contains("error", StringComparison.OrdinalIgnoreCase) || (StatusText.Text ?? "").Contains("failed", StringComparison.OrdinalIgnoreCase) ? "Error" : "Success"; reactionUntil = DateTime.UtcNow.AddSeconds(2); }
                wasBusy = _busy;
                var activity = _busy ? "Thinking" : DateTime.UtcNow < reactionUntil ? reaction : _petBubble is { IsVisible: true } ? "Speaking" : _mini is { IsVisible: true } ? "Listening" : "Idle";
                if (_petMark is not null) _petMark.Activity = activity;
                if (_petImage is not null)
                {
                    var amount = _config.PetAnimations && !_config.PetReducedMotion ? Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.Clamp(_config.PetAnimationSpeed, .25, 3) * (activity == "Thinking" ? 4 : 2)) * 3 * Math.Clamp(_config.PetAnimationIntensity, 0, 2) : 0;
                    _petImage.RenderTransform = new TranslateTransform(0, amount);
                }
                var local = _config.ActiveProvider == "Local";
                var needsModel = local ? _engine.ActiveModelPath is null : !_config.Providers.TryGetValue(_config.ActiveProvider, out var profile) || string.IsNullOrWhiteSpace(profile.Model) || string.IsNullOrWhiteSpace(profile.BaseUrl);
                status.Text = _busy ? "Working…" : needsModel ? "Setup needed" : local ? "Model loaded" : "Configured";
                status.Foreground = _busy || needsModel ? Brushes.Gold : Brushes.LightGreen;
                ToolTip.SetTip(pet, _busy ? "J.A.R.V.I.S. is working" : needsModel ? "Open Engine and models to finish setup" : local ? $"Loaded: {Path.GetFileName(_engine.ActiveModelPath)}" : $"{_config.ActiveProvider} is configured; connectivity is checked when you send a request.");
            };
            _petStatusTimer.Start();
            var screen = Screens.Primary;
            var area = screen?.WorkingArea ?? new PixelRect(0, 0, 1280, 720);
            var candidate = _config.PetX.HasValue && _config.PetY.HasValue ? new PixelPoint(_config.PetX.Value, _config.PetY.Value) : new PixelPoint(area.Right - 130, area.Bottom - 150);
            pet.Position = Screens.All.Any(s => s.WorkingArea.Contains(candidate)) ? candidate : new PixelPoint(area.Right - 130, area.Bottom - 150);
        }
        var diameter = Math.Clamp(_config.PetSize, 64, 180);
        _desktopPet.Width = _desktopPet.Height = diameter;
        _desktopPet.Topmost = _config.PetAlwaysOnTop;
        _desktopPet.Opacity = Math.Clamp(_config.PetOpacity, .3, 1);
        if (_petMark is not null && _petImage is not null)
        {
            _petMark.Width = _petMark.Height = diameter * .68; _petMark.AnimationsEnabled = _config.PetAnimations && !_config.PetReducedMotion;
            _petMark.AnimationSpeed = _config.PetAnimationSpeed; _petMark.AnimationIntensity = _config.PetAnimationIntensity;
            _petImage.Width = _petImage.Height = diameter * .68;
            _petImage.Source = null; _petBitmap?.Dispose(); _petBitmap = null;
            if (!string.IsNullOrWhiteSpace(_config.PetImagePath)) try { _petBitmap = new Bitmap(_config.PetImagePath); _petImage.Source = _petBitmap; } catch (Exception) { }
            _petImage.IsVisible = _petBitmap is not null; _petMark.IsVisible = _petBitmap is null;
        }
        if (!_desktopPet.IsVisible) _desktopPet.Show();
    }

    private void HideTemporaryPetLater() => Dispatcher.UIThread.Post(() =>
    {
        if (!_config.PetEnabled && !_busy && _petBubble is null && _mini is not { IsVisible: true }) _desktopPet?.Hide();
    }, DispatcherPriority.Background);

    private void SnapPetToEdge(Window pet)
    {
        var screen = Screens.ScreenFromWindow(pet) ?? Screens.Primary; if (screen is null) return;
        var area = screen.WorkingArea; var width = (int)(pet.Width * screen.Scaling); var height = (int)(pet.Height * screen.Scaling);
        var x = Math.Clamp(pet.Position.X, area.X, Math.Max(area.X, area.Right - width));
        var y = Math.Clamp(pet.Position.Y, area.Y, Math.Max(area.Y, area.Bottom - height));
        var distances = new[] { x - area.X, area.Right - width - x, y - area.Y, area.Bottom - height - y };
        switch (Array.IndexOf(distances, distances.Min())) { case 0: x = area.X; break; case 1: x = area.Right - width; break; case 2: y = area.Y; break; default: y = area.Bottom - height; break; }
        pet.Position = new PixelPoint(x, y);
    }
}
