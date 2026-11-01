using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Diagnostics;

namespace JARVIS;

public partial class MainWindow
{
    private const string CustomModelOption = "Custom model ID…";
    private bool _loadingModelChoice;
    private static readonly string[] SlashCommands =
    {
        "/model — choose a model, or /model slug",
        "/status — current engine and tools",
        "/reasoning — toggle deliberate mode; on/off accepted",
        "/search — toggle web search; on/off accepted",
        "/memory — toggle memory; on/off accepted",
        "/new — start a conversation", "/settings — engine settings",
        "/mcp — connected tools", "/skills — local skills",
        "/image — open image generation", "/help — command reference"
    };

    private void ResetProviderModelChoice()
    {
        _loadingModelChoice = true;
        var model = CloudModelBox.Text ?? "";
        CloudModelChoice.ItemsSource = string.IsNullOrWhiteSpace(model) ? new[] { CustomModelOption } : new[] { model, CustomModelOption };
        CloudModelChoice.SelectedItem = CustomModelOption;
        CloudModelBox.IsVisible = true;
        ProviderModelsStatus.Text = "Fetch uses this provider's endpoint and key. Choose a listed model or enter a custom slug, then save engine settings.";
        _loadingModelChoice = false;
    }
    private void CloudModel_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_loadingModelChoice || CloudModelChoice.SelectedItem is not string id) return;
        CloudModelBox.IsVisible = id == CustomModelOption;
        if (id != CustomModelOption) CloudModelBox.Text = id;
    }
    private async void FetchProviderModels_Click(object? sender, RoutedEventArgs e) => await FetchProviderModelsAsync();
    private async Task FetchProviderModelsAsync()
    {
        var provider = _config.ActiveProvider;
        if (provider == "Local") { RefreshModels(); return; }
        var endpoint = EndpointBox.Text ?? ""; var key = ApiKeyBox.Text ?? "";
        FetchModelsButton.IsEnabled = false; ProviderModelsStatus.Text = "Fetching available models…";
        try
        {
            var models = await ProviderModels.FetchAsync(provider, endpoint, key);
            if (provider != _config.ActiveProvider || endpoint != EndpointBox.Text) return;
            var selected = CloudModelBox.Text;
            _loadingModelChoice = true;
            CloudModelChoice.ItemsSource = models.Concat(new[] { CustomModelOption }).ToArray();
            CloudModelChoice.SelectedItem = selected is not null && models.Contains(selected) ? selected : CustomModelOption;
            CloudModelBox.IsVisible = Equals(CloudModelChoice.SelectedItem, CustomModelOption);
            ProviderModelsStatus.Text = $"{models.Count} models found. Choose one, then save engine settings. Custom model ID is always available.";
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
            File.Move(temporary, destination); temporary = null; RefreshModels();
            ShowNotice("Imported " + name + ". Choose Local as your provider, then select the model to load it.");
        }
        catch (Exception ex) { ShowNotice("GGUF import failed: " + ex.Message); }
        finally { if (temporary is not null) { try { File.Delete(temporary); } catch (IOException) { } } SetBusy(false); }
    }
}
