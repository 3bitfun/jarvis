using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Diagnostics;

namespace JARVIS;

public partial class MainWindow
{
    private const string CustomModelOption = "Custom model ID…";
    private bool _loadingModelChoice;
    private IReadOnlyList<string> _availableModels = Array.Empty<string>();
    private IReadOnlyList<string> _availableImageModels = Array.Empty<string>();
    private bool _imageModelsVerified;
    private bool _updatingImageModels;
    private static bool LikelyImageModel(string id) => new[] { "image", "imagen", "flux", "dall-e", "sdxl", "stable-diffusion", "recraft", "ideogram" }.Any(part => id.Contains(part, StringComparison.OrdinalIgnoreCase));
    private bool ModelMatchesType(string id)
    {
        var type = CloudModelTypeFilter.SelectedItem as string ?? "All models";
        return type == "All models" || (type == "Likely image models") == LikelyImageModel(id);
    }
    private static readonly string[] SlashCommands =
    {
        "/model — choose a model, or /model slug",
        "/status — current engine and tools",
        "/reasoning — toggle deliberate mode; on/off accepted",
        "/search — toggle web search; on/off accepted",
        "/memory — toggle memory; on/off accepted",
        "/new — start a conversation", "/settings — engine settings",
        "/mcp — connected tools", "/skills — local skills",
        "/image — toggle image mode, or /image prompt", "/help — command reference"
    };

    private void ResetProviderModelChoice()
    {
        _loadingModelChoice = true;
        _availableModels = Array.Empty<string>(); CloudModelFilter.Text = "";
        _availableImageModels = Array.Empty<string>(); _imageModelsVerified = false;
        var model = CloudModelBox.Text ?? "";
        CloudModelChoice.ItemsSource = string.IsNullOrWhiteSpace(model) ? new[] { CustomModelOption } : new[] { model, CustomModelOption };
        CloudModelChoice.SelectedItem = CustomModelOption;
        CloudModelBox.IsVisible = true;
        ProviderModelsStatus.Text = "Fetch uses this provider's endpoint and key. Choose a listed model or enter a custom slug, then save engine settings.";
        _loadingModelChoice = false;
        if (_ready) RefreshImageModelChoices();
    }
    private void CloudModel_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_loadingModelChoice || CloudModelChoice.SelectedItem is not string id) return;
        CloudModelBox.IsVisible = id == CustomModelOption;
        if (id != CustomModelOption) CloudModelBox.Text = id;
    }
    private async void FetchProviderModels_Click(object? sender, RoutedEventArgs e) => await FetchProviderModelsAsync();
    private void CloudModelFilter_Changed(object? sender, TextChangedEventArgs e)
    {
        if (_loadingModelChoice || _availableModels.Count == 0) return;
        var query = CloudModelFilter.Text?.Trim() ?? "";
        var choices = _availableModels.Where(id => id.Contains(query, StringComparison.OrdinalIgnoreCase) && ModelMatchesType(id)).ToArray();
        _loadingModelChoice = true;
        CloudModelChoice.ItemsSource = choices.Concat(new[] { CustomModelOption }).ToArray();
        CloudModelChoice.SelectedItem = choices.Contains(CloudModelBox.Text ?? "") ? CloudModelBox.Text : CustomModelOption;
        CloudModelBox.IsVisible = Equals(CloudModelChoice.SelectedItem, CustomModelOption);
        _loadingModelChoice = false;
    }
    private void CloudModelTypeFilter_Changed(object? sender, SelectionChangedEventArgs e) { if (_ready) CloudModelFilter_Changed(sender, null!); }
    private async void FetchImageModels_Click(object? sender, RoutedEventArgs e)
    {
        if (_config.ActiveProvider == "Local") { ImageModelsStatus.Text = "Select a cloud chat provider to search its models, or install Local CPU below."; return; }
        var provider = _config.ActiveProvider; var endpoint = EndpointBox.Text ?? ""; var key = ApiKeyBox.Text ?? "";
        ImageModelsStatus.Text = "Fetching image models…";
        try
        {
            var models = await ProviderModels.FetchImageModelsAsync(provider, endpoint, key);
            if (provider != _config.ActiveProvider || endpoint != EndpointBox.Text || key != ApiKeyBox.Text) return;
            _availableImageModels = models; _imageModelsVerified = provider == "OpenRouter"; RefreshImageModelChoices();
        }
        catch (Exception ex) { if (provider == _config.ActiveProvider) ImageModelsStatus.Text = "Could not fetch image models: " + ex.Message; }
    }
    private void ImageModelFilter_Changed(object? sender, TextChangedEventArgs e) { if (_ready) RefreshImageModelChoices(); }
    private void ImageModelChoice_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingImageModels || ImageModelChoice.SelectedItem is not string id) return;
        ImageChatModelInput.Text = id; _config.ImageChatModel = id; SavePreferences();
    }
    private void RefreshImageModelChoices()
    {
        var query = ImageModelFilter.Text?.Trim() ?? "";
        var choices = _availableImageModels.Where(id => (_imageModelsVerified || LikelyImageModel(id)) && id.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        _updatingImageModels = true; ImageModelChoice.ItemsSource = choices; ImageModelChoice.SelectedItem = choices.Contains(ImageChatModelInput.Text ?? "") ? ImageChatModelInput.Text : null; _updatingImageModels = false;
        ImageModelsStatus.Text = _imageModelsVerified ? $"{choices.Length} image models from the provider. You can also enter a custom ID." : $"{choices.Length} likely image models found by name. Capability varies; you can enter any model ID.";
    }
    private async Task FetchProviderModelsAsync()
    {
        var provider = _config.ActiveProvider;
        if (provider == "Local") { RefreshModels(); return; }
        var endpoint = EndpointBox.Text ?? ""; var key = ApiKeyBox.Text ?? "";
        FetchModelsButton.IsEnabled = false; ProviderModelsStatus.Text = "Fetching available models…";
        try
        {
            var models = await ProviderModels.FetchAsync(provider, endpoint, key);
            if (provider != _config.ActiveProvider || endpoint != EndpointBox.Text || key != ApiKeyBox.Text) return;
            var selected = CloudModelBox.Text;
            _loadingModelChoice = true;
            _availableModels = models; CloudModelFilter.Text = "";
            var choices = models.Where(ModelMatchesType).ToArray();
            CloudModelChoice.ItemsSource = choices.Concat(new[] { CustomModelOption }).ToArray();
            CloudModelChoice.SelectedItem = selected is not null && choices.Contains(selected) ? selected : CustomModelOption;
            CloudModelBox.IsVisible = Equals(CloudModelChoice.SelectedItem, CustomModelOption);
            ProviderModelsStatus.Text = $"{models.Count} models found. Choose one, then save engine settings. Custom model ID is always available.";
            RefreshImageModelChoices();
        }
        catch (Exception ex) { if (provider == _config.ActiveProvider) ProviderModelsStatus.Text = ex.Message; }
        finally { _loadingModelChoice = false; FetchModelsButton.IsEnabled = true; }
    }
    private void PromptCommand_Changed(object? sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        var text = PromptBox.Text ?? "";
        var matches = text.StartsWith('/') && !text.Contains(' ') && !text.Contains('\n')
            ? SlashCommands.Where(command => command.Split(' ')[0].StartsWith(text, StringComparison.OrdinalIgnoreCase)).ToArray() : Array.Empty<string>();
        CommandSuggestions.ItemsSource = matches;
        CommandSuggestions.IsVisible = matches.Length > 0;
    }
    private void CommandSuggestion_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (CommandSuggestions.SelectedItem is not string command) return;
        PromptBox.Text = command.Split(' ')[0] + " "; PromptBox.CaretIndex = PromptBox.Text.Length;
        CommandSuggestions.IsVisible = false; PromptBox.Focus();
    }
    private async Task<bool> HandleSlashCommandAsync(string input)
    {
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant(); var argument = parts.Length > 1 ? parts[1].Trim() : "";
        bool? enabled = argument.ToLowerInvariant() switch { "on" => true, "off" => false, "" => null, _ => null };
        string reply;
        switch (command)
        {
            case "/model":
                if (argument.Length == 0) { OpenSettingsTab(0); await FetchProviderModelsAsync(); return true; }
                if (_config.ActiveProvider == "Local")
                {
                    var match = _engine.LocalModels.FirstOrDefault(name => name.Equals(argument, StringComparison.OrdinalIgnoreCase));
                    if (match is null) { ShowNotice("Local model not found. Use /model to import or select a GGUF file."); return true; }
                    SetBusy(true, "Loading model…");
                    try { await Task.Run(() => _engine.SwitchModel(Path.Combine(_engine.ModelsDirectory, match))); _config.PreferredLocalModel = match; RefreshModels(); }
                    catch (Exception ex) { ShowNotice(ex.Message); return true; }
                    finally { SetBusy(false); }
                }
                else { _config.Providers[_config.ActiveProvider].Model = argument; CloudModelBox.Text = argument; ResetProviderModelChoice(); }
                SavePreferences(); UpdateEngineBadge(); reply = "Model selected: " + argument; break;
            case "/status":
                reply = $"Provider: {_config.ActiveProvider}\nModel: {(_config.ActiveProvider == "Local" ? Path.GetFileName(_engine.ActiveModelPath) ?? "not loaded" : _config.Providers[_config.ActiveProvider].Model)}\nDeliberate mode: {_config.DeliberateMode}\nWeb search: {_config.WebSearchEnabled}\nMemory: {_config.MemoryEnabled}\nMCP: {_engine.Mcp.ConnectedSummary}\nEnabled tools: {_engine.Mcp.GetEnabledTools().Count}\nSkills enabled: {_engine.Skills.Skills.Count(skill => skill.Enabled)}"; break;
            case "/reasoning": case "/reasonnig": case "/reasoningmode":
                if (argument.Length > 0 && argument != "on" && argument != "off") { ShowNotice("Use /reasoning, /reasoning on, or /reasoning off."); return true; }
                _config.DeliberateMode = enabled ?? !_config.DeliberateMode; DeliberateCheck.IsChecked = _config.DeliberateMode;
                SavePreferences(); reply = "Deliberate mode " + (_config.DeliberateMode ? "on" : "off") + ". This guides response quality; capabilities depend on your model."; break;
            case "/search":
                if (argument.Length > 0 && enabled is null) { ShowNotice("Use /search, /search on, or /search off."); return true; }
                if ((enabled ?? !_config.WebSearchEnabled) != _config.WebSearchEnabled) SearchToggle_Click(this, new RoutedEventArgs());
                reply = "Web search " + (_config.WebSearchEnabled ? "on" : "off"); break;
            case "/memory":
                if (argument.Length > 0 && enabled is null) { ShowNotice("Use /memory, /memory on, or /memory off."); return true; }
                _config.MemoryEnabled = enabled ?? !_config.MemoryEnabled; MemoryCheck.IsChecked = _config.MemoryEnabled;
                SavePreferences(); reply = "Memory " + (_config.MemoryEnabled ? "on" : "paused"); break;
            case "/new": NewChat_Click(this, new RoutedEventArgs()); return true;
            case "/settings": OpenSettingsTab(0); return true;
            case "/mcp": OpenSettingsTab(3); return true;
            case "/skills": OpenSettingsTab(4); return true;
            case "/image": GenerateImage_Click(this, new RoutedEventArgs()); return true;
            case "/verity":
                _config.VerityMode = !_config.VerityMode;
                if (_petCharacter is not null) _petCharacter.SelectedItem = _config.VerityMode ? "Verity" : "J.A.R.V.I.S.";
                Avalonia.Application.Current!.Resources["VerityMode"] = _config.VerityMode;
                ThemeManager.Apply(_config.Theme, _config.Accent, _config.Tint); ApplyAppearance(); SavePreferences();
                reply = _config.VerityMode ? "Verity mode enabled. Use /verity again to return to J.A.R.V.I.S." : "J.A.R.V.I.S. restored.";
                break;
            case "/help": reply = string.Join("\n", SlashCommands) + "\n\n/reasonnig is also accepted as an alias for /reasoning."; break;
            default: ShowNotice("Unknown command. Type /help for available commands."); return true;
        }
        _engine.RecordAction(input, reply); NavigateConversation(); ShowConversation(); RefreshChats(); ShowNotice(reply);
        return true;
    }
    private void OpenModelsFolder_Click(object? sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(_engine.ModelsDirectory); Process.Start(new ProcessStartInfo(_engine.ModelsDirectory) { UseShellExecute = true }); }
        catch (Exception ex) { ShowNotice("Could not open models folder: " + ex.Message); }
    }
    private async void ImportGguf_Click(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Import a local GGUF model", AllowMultiple = false, FileTypeFilter = new[] { new FilePickerFileType("GGUF models") { Patterns = new[] { "*.gguf" } } } });
        if (files.FirstOrDefault() is not { } file) return;
        string? temporary = null;
        SetBusy(true, "Copying GGUF model into the models folder…");
        try
        {
            var name = Path.GetFileName(file.Name);
            if (!name.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Choose a .gguf model file.");
            Directory.CreateDirectory(_engine.ModelsDirectory);
            var destination = Path.Combine(_engine.ModelsDirectory, name);
            if (File.Exists(destination)) throw new IOException("A model with this name already exists. Select it from the local model list, or rename your file before importing.");
            temporary = destination + "." + Guid.NewGuid().ToString("N") + ".import";
            await using (var source = await file.OpenReadAsync())
            await using (var target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            { await source.CopyToAsync(target); }
            await using (var check = File.OpenRead(temporary))
            {
                var header = new byte[4];
                if (check.Length < 1024 || await check.ReadAsync(header) != 4 || !header.AsSpan().SequenceEqual("GGUF"u8))
                    throw new InvalidDataException("The selected file is not a complete GGUF model.");
            }
            File.Move(temporary, destination); temporary = null; RefreshModels();
            ShowNotice("Imported " + name + ". Choose Local as your provider, then select the model to load it.");
        }
        catch (Exception ex) { ShowNotice("GGUF import failed: " + ex.Message); }
        finally { if (temporary is not null) { try { File.Delete(temporary); } catch (IOException) { } } SetBusy(false); }
    }
}
