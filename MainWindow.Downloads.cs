using Avalonia.Interactivity;
using System.Diagnostics;

namespace JARVIS;

public partial class MainWindow
{
    private readonly ModelDownloadService _modelDownloads = new();
    private readonly AppUpdateService _updater = new();
    private CancellationTokenSource? _modelDownloadCancellation;
    private CancellationTokenSource? _updateCancellation;
    private AvailableUpdate? _availableUpdate;

    private void InitializeDownloads()
    {
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

    private async void CheckUpdate_Click(object? sender, RoutedEventArgs e)
    {
        if (_updateCancellation is not null) return;
        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;
        CheckUpdateButton.IsEnabled = false;
        InstallUpdateButton.IsVisible = false;
        _availableUpdate = null;
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
            UpdateStatus.Text = "Verified installer. Opening setup…";
            Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true });
            Close();
        }
        catch (OperationCanceledException) { if (!_closing) UpdateStatus.Text = "Update download cancelled."; }
        catch (Exception ex) { if (!_closing) UpdateStatus.Text = "Update failed: " + ex.Message; }
        finally
        {
            _updateCancellation = null;
            if (!_closing) { CheckUpdateButton.IsEnabled = true; InstallUpdateButton.IsEnabled = true; UpdateProgress.IsVisible = false; }
        }
    }
}
