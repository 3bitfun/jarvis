using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;

namespace JARVIS;

public partial class MainWindow
{
    private Window? _desktopPet;
    private DispatcherTimer? _petStatusTimer;
    private CheckBox? _petEnabled;
    private AssistantMark? _petMark;
    private Image? _petImage;
    private Bitmap? _petBitmap;

    private void InitializeDesktopPet(bool interactive)
    {
        var style = (StackPanel)((ScrollViewer)((TabItem)SettingsControls.Items[5]!).Content!).Content!;
        style.Children.Add(new TextBlock { Text = "Desktop pet", FontSize = 16, FontWeight = FontWeight.SemiBold });
        _petEnabled = new CheckBox { Content = "Show desktop pet", IsChecked = _config.PetEnabled };
        _petEnabled.IsCheckedChanged += (_, _) => { _config.PetEnabled = _petEnabled.IsChecked == true; SavePreferences(); if (interactive) UpdateDesktopPet(); };
        style.Children.Add(_petEnabled);
        var size = new NumericUpDown { Minimum = 64, Maximum = 180, Increment = 8, Value = Math.Clamp(_config.PetSize, 64, 180) };
        size.ValueChanged += (_, _) => { _config.PetSize = (int)(size.Value ?? 96); SavePreferences(); if (interactive) UpdateDesktopPet(); };
        style.Children.Add(new TextBlock { Text = "Pet size" }); style.Children.Add(size);
        var opacity = new NumericUpDown { Minimum = .3m, Maximum = 1m, Increment = .1m, Value = (decimal)Math.Clamp(_config.PetOpacity, .3, 1) };
        opacity.ValueChanged += (_, _) => { _config.PetOpacity = (double)(opacity.Value ?? 1); SavePreferences(); if (interactive) UpdateDesktopPet(); };
        style.Children.Add(new TextBlock { Text = "Pet opacity" }); style.Children.Add(opacity);
        var personality = new ComboBox { ItemsSource = new[] { "Professional", "Calm", "Playful" }, SelectedItem = _config.PetPersonality };
        personality.SelectionChanged += (_, _) => { _config.PetPersonality = personality.SelectedItem as string ?? "Professional"; SavePreferences(); };
        style.Children.Add(new TextBlock { Text = "Companion personality" }); style.Children.Add(personality);
        var animations = new CheckBox { Content = "Animate pet", IsChecked = _config.PetAnimations };
        animations.IsCheckedChanged += (_, _) => { _config.PetAnimations = animations.IsChecked == true; SavePreferences(); UpdateDesktopPet(); };
        style.Children.Add(animations);
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
        Closed += (_, _) => { _petStatusTimer?.Stop(); _desktopPet?.Close(); _petBitmap?.Dispose(); };
    }

    private void UpdateDesktopPet()
    {
        if (!_config.PetEnabled) { _desktopPet?.Hide(); return; }
        if (_desktopPet is null)
        {
            var status = new TextBlock { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, FontSize = 10 };
            _petMark = new AssistantMark { Width = 68, Height = 68, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            _petImage = new Image { Width = 68, Height = 68, Stretch = Stretch.Uniform, IsVisible = false };
            var layout = new StackPanel { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Children = { _petMark, _petImage, status } };
            var pet = new Window { Title = "J.A.R.V.I.S. pet", SystemDecorations = SystemDecorations.None, Background = Brushes.Transparent, TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent }, Topmost = true, ShowInTaskbar = false, CanResize = false, Content = layout };
            _desktopPet = pet;
            var menu = new ContextMenu();
            void Item(string label, Action action) { var item = new MenuItem { Header = label }; item.Click += (_, _) => action(); menu.Items.Add(item); }
            Item("Quick chat", ToggleQuickBar); Item("Open full deck", OpenFullDeck);
            Item("Pet settings", () => { OpenFullDeck(); OpenSettingsTab(5); });
            Item("Hide pet", () => { if (_petEnabled is not null) _petEnabled.IsChecked = false; });
            pet.ContextMenu = menu;
            PixelPoint start = default, pointerStart = default; bool dragging = false;
            pet.PointerPressed += (_, e) => { if (!e.GetCurrentPoint(pet).Properties.IsLeftButtonPressed) return; start = pet.Position; pointerStart = pet.PointToScreen(e.GetPosition(pet)); dragging = true; e.Pointer.Capture(pet); e.Handled = true; };
            pet.PointerMoved += (_, e) => { if (!dragging) return; var current = pet.PointToScreen(e.GetPosition(pet)); pet.Position = new PixelPoint(start.X + current.X - pointerStart.X, start.Y + current.Y - pointerStart.Y); };
            pet.PointerReleased += (_, e) => { if (!dragging) return; dragging = false; e.Pointer.Capture(null); if (Math.Abs(pet.Position.X - start.X) < 4 && Math.Abs(pet.Position.Y - start.Y) < 4) ToggleQuickBar(); else { _config.PetX = pet.Position.X; _config.PetY = pet.Position.Y; SavePreferences(); } e.Handled = true; };
            _petStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _petStatusTimer.Tick += (_, _) => { status.Text = _busy ? "Working…" : "Ready"; status.Foreground = _busy ? Brushes.Gold : Brushes.LightGreen; };
            _petStatusTimer.Start();
            var screen = Screens.Primary;
            var area = screen?.WorkingArea ?? new PixelRect(0, 0, 1280, 720);
            var candidate = _config.PetX.HasValue && _config.PetY.HasValue ? new PixelPoint(_config.PetX.Value, _config.PetY.Value) : new PixelPoint(area.Right - 130, area.Bottom - 150);
            pet.Position = Screens.All.Any(s => s.WorkingArea.Contains(candidate)) ? candidate : new PixelPoint(area.Right - 130, area.Bottom - 150);
        }
        var diameter = Math.Clamp(_config.PetSize, 64, 180);
        _desktopPet.Width = _desktopPet.Height = diameter;
        _desktopPet.Opacity = Math.Clamp(_config.PetOpacity, .3, 1);
        if (_petMark is not null && _petImage is not null)
        {
            _petMark.Width = _petMark.Height = diameter * .68; _petMark.AnimationsEnabled = _config.PetAnimations;
            _petImage.Width = _petImage.Height = diameter * .68;
            _petImage.Source = null; _petBitmap?.Dispose(); _petBitmap = null;
            if (!string.IsNullOrWhiteSpace(_config.PetImagePath)) try { _petBitmap = new Bitmap(_config.PetImagePath); _petImage.Source = _petBitmap; } catch (Exception) { }
            _petImage.IsVisible = _petBitmap is not null; _petMark.IsVisible = _petBitmap is null;
        }
        if (!_desktopPet.IsVisible) _desktopPet.Show();
    }
}
