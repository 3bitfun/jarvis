using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Media;
using System.Diagnostics;

namespace JARVIS;

public partial class MainWindow
{
    private readonly ModelDownloadService _modelDownloads = new();
    private readonly AppUpdateService _updater = new();
    private CancellationTokenSource? _modelDownloadCancellation;
    private CancellationTokenSource? _updateCancellation;
    private CancellationTokenSource? _imageModelInstallCancellation;
    private AvailableUpdate? _availableUpdate;
    private string? _verifiedInstaller;
    private TextBlock? _releaseNotes;
    private Button? _cancelUpdate;
    private Button? _runUpdate;

    private void InitializeDownloads()
    {
        var updatePanel = (StackPanel)UpdateStatus.Parent!;
        _releaseNotes = new TextBlock { TextWrapping = TextWrapping.Wrap, IsVisible = false, MaxHeight = 240 };
        updatePanel.Children.Add(new ScrollViewer { MaxHeight = 240, Content = _releaseNotes });
        _cancelUpdate = new Button { Content = "Cancel update download", IsVisible = false };
        _cancelUpdate.Click += (_, _) => _updateCancellation?.Cancel(); updatePanel.Children.Add(_cancelUpdate);
        _runUpdate = new Button { Content = "Install verified update", IsVisible = false };
        _runUpdate.Click += async (_, _) =>
        {
            if (_busy || _verifiedInstaller is null || _availableUpdate is null) { UpdateStatus.Text = "Finish the current request before installing."; return; }
            _runUpdate.IsEnabled = false;
            try { if (!await AppUpdateService.MatchesAsync(_verifiedInstaller, _availableUpdate, CancellationToken.None)) { UpdateStatus.Text = "Installer verification failed. Download the update again."; _runUpdate.IsVisible = false; return; } Process.Start(new ProcessStartInfo(_verifiedInstaller) { UseShellExecute = true }); Close(); }
            catch (Exception ex) { UpdateStatus.Text = "Could not open installer: " + ex.Message; }
            finally { _runUpdate.IsEnabled = true; }
        };
        updatePanel.Children.Add(_runUpdate);
        InstallUpdateButton.Content = "Download update";
        ImageBackendChoice.ItemsSource = new[] { "Automatic", "Chat provider", "Separate service", "Local CPU" };
        ImageBackendChoice.SelectedItem = _config.ImageBackend;
        ImageBackendChoice.SelectionChanged += (_, _) => { if (ImageBackendChoice.SelectedItem is string value) { _config.ImageBackend = value; SavePreferences(); } };
        ImageChatModelInput.Text = _config.ImageChatModel;
        ImageChatModelInput.TextChanged += (_, _) => { if (_ready) _config.ImageChatModel = ImageChatModelInput.Text?.Trim() ?? ""; };
        ImageChatModelInput.LostFocus += (_, _) => { if (_ready) SavePreferences(); };
        InstallImageModelButton.IsEnabled = LocalImageService.Supported;
        LocalImageStatus.Text = !LocalImageService.Supported ? "Automatic installation is available on Windows x64. Connect a local image server on this platform." : LocalImageService.IsInstalled ? "Local image model installed." : "Local image model not installed.";
        CloudModelTypeFilter.ItemsSource = new[] { "All models", "Likely chat models", "Likely image models" }; CloudModelTypeFilter.SelectedIndex = 0;
        CuratedModelChoice.ItemsSource = ModelDownloadService.Curated;
        CuratedModelChoice.SelectionChanged += (_, _) =>
        {
            if (CuratedModelChoice.SelectedItem is ModelDownloadChoice)
                CustomGgufUrl.Text = "";
        };
        CustomGgufUrl.TextChanged += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(CustomGgufUrl.Text)) CuratedModelChoice.SelectedItem = null;
        };
    }

    private async void DownloadGguf_Click(object? sender, RoutedEventArgs e)
    {
        if (_modelDownloadCancellation is not null) return;
        var custom = CustomGgufUrl.Text?.Trim();
        var source = !string.IsNullOrWhiteSpace(custom) ? custom :
            (CuratedModelChoice.SelectedItem as ModelDownloadChoice)?.Source;
        if (source is null) { GgufDownloadStatus.Text = "Choose a recommended model or paste a direct GGUF URL."; return; }
        using var cancellation = new CancellationTokenSource();
        _modelDownloadCancellation = cancellation;
        DownloadGgufButton.IsEnabled = false;
        CancelGgufButton.IsVisible = true;
        GgufProgress.IsVisible = true;
        GgufProgress.IsIndeterminate = false;
        GgufProgress.Value = 0;
        GgufDownloadStatus.Text = "Connecting to model source…";
        try
        {
            var progress = new Progress<DownloadProgress>(p =>
            {
                if (cancellation.IsCancellationRequested || _closing || !ReferenceEquals(_modelDownloadCancellation, cancellation)) return;
                GgufProgress.IsIndeterminate = p.Total is null;
                GgufProgress.Value = p.Percent;
                GgufDownloadStatus.Text = p.Label;
            });
            var path = await _modelDownloads.DownloadAsync(source, _engine.ModelsDirectory, progress, cancellation.Token);
            if (_closing) return;
            RefreshModels();
            GgufDownloadStatus.Text = $"Ready: {Path.GetFileName(path)}. Choose Local and select it above to load.";
            ShowNotice("Model downloaded. Choose it from the local model list to load it.");
        }
        catch (OperationCanceledException) { if (!_closing) GgufDownloadStatus.Text = "Download cancelled. No partial model was kept."; }
        catch (Exception ex) { if (!_closing) GgufDownloadStatus.Text = "Download failed: " + ex.Message; }
        finally
        {
            _modelDownloadCancellation = null;
            if (!_closing) { DownloadGgufButton.IsEnabled = true; CancelGgufButton.IsVisible = false; GgufProgress.IsVisible = false; }
        }
    }

    private void CancelGguf_Click(object? sender, RoutedEventArgs e) => _modelDownloadCancellation?.Cancel();

    private async void InstallImageModel_Click(object? sender, RoutedEventArgs e)
    {
        if (_imageModelInstallCancellation is not null) return;
        using var cancellation = new CancellationTokenSource(); _imageModelInstallCancellation = cancellation;
        InstallImageModelButton.IsEnabled = false; CancelImageModelButton.IsVisible = true;
        LocalImageStatus.Text = "Preparing verified local image downloads…";
        try
        {
            var progress = new Progress<string>(value => { if (!_closing && !cancellation.IsCancellationRequested) LocalImageStatus.Text = value; });
            await LocalImageService.InstallAsync(progress, cancellation.Token);
            if (!_closing) { ImageBackendChoice.SelectedItem = "Local CPU"; LocalImageStatus.Text = "Installed. Local CPU is selected for image generation."; }
        }
        catch (OperationCanceledException) { if (!_closing) LocalImageStatus.Text = "Download cancelled; partial model removed."; }
        catch (Exception ex) { if (!_closing) LocalImageStatus.Text = "Local image setup failed: " + ex.Message; }
        finally { _imageModelInstallCancellation = null; if (!_closing) { InstallImageModelButton.IsEnabled = LocalImageService.Supported; CancelImageModelButton.IsVisible = false; } }
    }
    private void CancelImageModel_Click(object? sender, RoutedEventArgs e) => _imageModelInstallCancellation?.Cancel();

    private async void CheckUpdate_Click(object? sender, RoutedEventArgs e)
    {
        if (_updateCancellation is not null) return;
        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;
        CheckUpdateButton.IsEnabled = false;
        InstallUpdateButton.IsVisible = false;
        _availableUpdate = null;
        _verifiedInstaller = null; if (_runUpdate is not null) _runUpdate.IsVisible = false;
        if (_releaseNotes is not null) _releaseNotes.IsVisible = false;
        UpdateStatus.Text = "Checking published releases…";
        try
        {
            var current = typeof(App).Assembly.GetName().Version ?? new Version(0, 0);
            var update = await _updater.CheckAsync(current, cancellation.Token);
            if (_closing) return;
            _availableUpdate = update;
            if (update is null) UpdateStatus.Text = "No newer published Windows release is available.";
            else
            {
                UpdateStatus.Text = $"Version {update.Version} is available ({update.Bytes / 1_000_000.0:0.0} MB). The installer will be verified before it runs.";
                InstallUpdateButton.IsVisible = true;
                if (_releaseNotes is not null) { _releaseNotes.Text = "Release notes\n\n" + (string.IsNullOrWhiteSpace(update.Notes) ? "No release notes provided." : update.Notes); _releaseNotes.IsVisible = true; }
            }
        }
        catch (OperationCanceledException) { if (!_closing) UpdateStatus.Text = "Update check cancelled."; }
        catch (Exception ex) { if (!_closing) UpdateStatus.Text = "Update check failed: " + ex.Message; }
        finally { _updateCancellation = null; if (!_closing) CheckUpdateButton.IsEnabled = true; }
    }

    private async void InstallUpdate_Click(object? sender, RoutedEventArgs e)
    {
        if (_availableUpdate is not { } update || _updateCancellation is not null || _busy) return;
        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;
        CheckUpdateButton.IsEnabled = false;
        InstallUpdateButton.IsEnabled = false;
        UpdateProgress.IsVisible = true;
        if (_cancelUpdate is not null) _cancelUpdate.IsVisible = true;
        UpdateStatus.Text = "Downloading installer…";
        try
        {
            var progress = new Progress<DownloadProgress>(p =>
            {
                if (_closing || !ReferenceEquals(_updateCancellation, cancellation)) return;
                UpdateProgress.Value = p.Percent;
                UpdateStatus.Text = "Downloading installer: " + p.Label;
            });
            var installer = await _updater.DownloadAsync(update, progress, cancellation.Token);
            if (_closing) return;
            _verifiedInstaller = installer;
            UpdateStatus.Text = "Download verified. Install when you are ready.";
            if (_runUpdate is not null) _runUpdate.IsVisible = true;
        }
        catch (OperationCanceledException) { if (!_closing) UpdateStatus.Text = "Update download cancelled."; }
        catch (Exception ex) { if (!_closing) UpdateStatus.Text = "Update failed: " + ex.Message; }
        finally
        {
            _updateCancellation = null;
            if (!_closing) { CheckUpdateButton.IsEnabled = true; InstallUpdateButton.IsEnabled = true; UpdateProgress.IsVisible = false; if (_cancelUpdate is not null) _cancelUpdate.IsVisible = false; }
        }
    }
}
