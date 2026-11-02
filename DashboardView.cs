using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Input;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;

namespace JARVIS;
public sealed class DashboardView : UserControl
{
    private readonly WrapPanel _cards = new();
    private readonly StackPanel _body = new() { Spacing = 14 };
    private readonly Dictionary<string, TextBlock> _values = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private AppConfig _config = new();
    private Action _save = () => { };
    private ulong _idle, _total;
    private string? _gpu;
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly string[] _types = { "Core", "System", "Storage", "Network", "Clock", "Suggestions", "Weather", "News", "Focus timer", "Recent chats", "Pinned apps", "Model status", "Notes" };
    public Func<string, string>? WidgetData { get; set; }
    private static readonly DataFormat<string> WidgetFormat = DataFormat.CreateStringApplicationFormat("jarvis-widget");
    public DashboardView()
    {
        Content = _body;
        SizeChanged += (_, _) => UpdateCardWidths();
        _timer.Tick += async (_, _) => { if (IsEffectivelyVisible) await SampleAsync(); };
        AttachedToVisualTree += (_, _) => _timer.Start(); DetachedFromVisualTree += (_, _) => _timer.Stop();
    }
    public void Configure(AppConfig config, Action save, Action welcome)
    {
        _config = config; _save = save; _body.Children.Clear();
        if (!config.SetupCompleted)
        {
            var setup = new Button { Content = "Complete your setup →" }; setup.Click += (_, _) => welcome(); _body.Children.Add(setup);
        }
        var options = new WrapPanel();
        foreach (var type in _types)
        {
            var id = type;
            var check = new CheckBox { Content = id, IsChecked = config.DashboardWidgets.Contains(id), Margin = new Thickness(0, 0, 12, 6) };
            check.IsCheckedChanged += (_, _) => { if (check.IsChecked == true) { if (!config.DashboardWidgets.Contains(id)) config.DashboardWidgets.Add(id); } else config.DashboardWidgets.Remove(id); save(); Render(); };
            options.Children.Add(check);
        }
        var city = new TextBox { Text = config.WeatherCity, Watermark = "Weather city", MaxLength = 100 };
        var weather = new CheckBox { Content = "Allow weather requests to Open-Meteo (sends city)", IsChecked = config.WeatherEnabled };
        var news = new CheckBox { Content = "Allow BBC news feed requests", IsChecked = config.NewsEnabled };
        weather.IsCheckedChanged += (_, _) => { config.WeatherEnabled = weather.IsChecked == true; save(); Render(); };
        news.IsCheckedChanged += (_, _) => { config.NewsEnabled = news.IsChecked == true; save(); Render(); };
        var fetch = new Button { Content = "Refresh web widgets" };
        fetch.Click += async (_, _) => { config.WeatherCity = city.Text?.Trim() ?? ""; save(); fetch.IsEnabled = false; try { await FetchAsync(); } finally { fetch.IsEnabled = true; } };
        _body.Children.Add(new Expander { Header = "Customize dashboard", Content = new StackPanel { Spacing = 9, Children = { options, city, weather, news, fetch } } });
        _body.Children.Add(_cards); Render(); _ = SampleAsync();
    }
    private void Render()
    {
        _cards.Children.Clear(); _values.Clear();
        foreach (var id in _config.DashboardWidgets.Distinct().Where(_types.Contains).ToArray())
        {
            var text = new TextBlock { Text = id is "Weather" or "News" ? "Enable web access and refresh in Customize dashboard." : "Updating…", TextWrapping = TextWrapping.Wrap, FontSize = 13 };
            _values[id] = text;
            var actions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4 };
            void Button(string label, Action action) { var b = new Button { Content = label, Padding = new Thickness(7, 3), FontSize = 11 }; b.Click += (_, _) => action(); actions.Children.Add(b); }
            Button("←", () => Move(id, -1)); Button("→", () => Move(id, 1));
            Button("Resize", () => { _config.WideWidgets[id] = !_config.WideWidgets.GetValueOrDefault(id); _save(); Render(); });
            var header = new TextBlock { Text = id.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeight.SemiBold, Cursor = new Cursor(StandardCursorType.SizeAll) };
            header.PointerPressed += async (_, e) => { if (!e.GetCurrentPoint(header).Properties.IsLeftButtonPressed) return; using var data = new DataTransfer(); data.Add(DataTransferItem.Create(WidgetFormat, id)); await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move); };
            var content = new StackPanel { Spacing = 12, Children = { header, text, actions } };
            if (id == "Core") content.Children.Insert(1, new AssistantMark { Width = 100, Height = 100 });
            var card = new Border { Width = _config.WideWidgets.GetValueOrDefault(id) ? 580 : 280, MinHeight = 170, Padding = new Thickness(18), Margin = new Thickness(0, 0, 12, 12), CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), Child = content };
            card.Tag = id;
            card.Bind(Border.BackgroundProperty, this.GetResourceObservable("RaisedBrush"));
            card.Bind(Border.BorderBrushProperty, this.GetResourceObservable("LineBrush"));
            DragDrop.SetAllowDrop(card, true);
            card.AddHandler(DragDrop.DropEvent, (_, e) => { if (e.DataTransfer.TryGetValue(WidgetFormat) is not string source || !_config.DashboardWidgets.Contains(source) || source == id) return; var target = _config.DashboardWidgets.IndexOf(id); _config.DashboardWidgets.Remove(source); _config.DashboardWidgets.Insert(Math.Min(target, _config.DashboardWidgets.Count), source); _save(); Render(); e.Handled = true; });
            _cards.Children.Add(card);
        }
        UpdateCardWidths(); _ = SampleAsync();
    }
    private void UpdateCardWidths()
    {
        if (Bounds.Width <= 0) return;
        foreach (var card in _cards.Children.OfType<Border>())
            if (card.Tag is string id) card.Width = Math.Max(120, Math.Min(_config.WideWidgets.GetValueOrDefault(id) ? 580 : 280, Bounds.Width - 12));
    }
    private void Move(string id, int delta) { var index = _config.DashboardWidgets.IndexOf(id); var next = Math.Clamp(index + delta, 0, _config.DashboardWidgets.Count - 1); _config.DashboardWidgets.RemoveAt(index); _config.DashboardWidgets.Insert(next, id); _save(); Render(); }
    private void Set(string id, string text) { if (_values.TryGetValue(id, out var label)) label.Text = text; }
    private bool _sampling;
    private async Task SampleAsync()
    {
        if (_body.Children.OfType<Button>().FirstOrDefault() is { } setup) setup.IsVisible = !_config.SetupCompleted;
        if (_sampling) return; _sampling = true;
        try
        {
            var sample = await Task.Run(() =>
            {
                var cpu = "CPU usage unavailable";
                var ram = "RAM reading unavailable";
                if (_gpu is null)
                {
                    _gpu = "GPU name unavailable";
                    if (OperatingSystem.IsWindows()) try
                    {
                        using var adapters = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Video");
                        foreach (var key in adapters?.GetSubKeyNames() ?? Array.Empty<string>())
                        {
                            using var adapter = adapters!.OpenSubKey(key + @"\0000");
                            if (adapter?.GetValue("DriverDesc") is string name && !name.Contains("Remote", StringComparison.OrdinalIgnoreCase)) { _gpu = name; break; }
                        }
                    }
                    catch (Exception) { }
                }
                if (OperatingSystem.IsWindows())
                {
                    if (GetSystemTimes(out var idle, out var kernel, out var user)) { var total = kernel + user; if (_total > 0 && total > _total) cpu = $"CPU {100.0 * (1 - (double)(idle - _idle) / (total - _total)):0}%"; _total = total; _idle = idle; }
                    var m = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() }; if (GlobalMemoryStatusEx(ref m)) ram = $"RAM {(m.Total - m.Available) / 1073741824.0:0.0} / {m.Total / 1073741824.0:0.0} GB";
                }
                var disks = string.Join("\n", DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).Select(d => $"{d.Name} {d.AvailableFreeSpace / 1073741824.0:0} GB free / {d.TotalSize / 1073741824.0:0} GB"));
                var network = string.Join("\n", NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback).Take(2).Select(n => n.Name + " · " + n.Speed / 1_000_000 + " Mbps link"));
                return (cpu, ram, disks, network);
            });
            Set("System", sample.cpu + "\n" + sample.ram + "\n" + _gpu + "\nGPU utilization: unavailable\n" + Environment.ProcessorCount + " logical processors");
            Set("Storage", sample.disks); Set("Network", sample.network + "\nLink status does not confirm internet access.");
            Set("Clock", DateTime.Now.ToString("HH:mm:ss\ndddd, d MMMM yyyy") + "\nUptime " + TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"d\.hh\:mm"));
            Set("Core", (_config.VerityMode ? "VERITY" : "J.A.R.V.I.S.") + "\n" + _config.ActiveProvider + " · " + _config.ResponseMode + "\nMemory " + (_config.MemoryEnabled ? "enabled" : "paused"));
            Set("Suggestions", _config.ActiveProvider == "Local" && string.IsNullOrEmpty(_config.PreferredLocalModel) ? "Choose or download a GGUF in Engine settings." : "Continue a recent chat, launch your work apps, or start a focus session.");
            foreach (var id in new[] { "Focus timer", "Recent chats", "Pinned apps", "Model status", "Notes" }) Set(id, WidgetData?.Invoke(id) ?? "Unavailable");
        }
        catch (Exception) { Set("System", "System readings are unavailable on this device."); }
        finally { _sampling = false; }
    }
    private async Task<string> GetAsync(string url)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token); response.EnsureSuccessStatusCode();
        return await HttpPayload.ReadTextAsync(response, 1_000_000, timeout.Token);
    }
    private async Task FetchAsync()
    {
        if (_config.WeatherEnabled) try
        {
            using var places = JsonDocument.Parse(await GetAsync("https://geocoding-api.open-meteo.com/v1/search?count=1&name=" + Uri.EscapeDataString(_config.WeatherCity)));
            var place = places.RootElement.GetProperty("results")[0];
            var lat = place.GetProperty("latitude").GetRawText(); var lon = place.GetProperty("longitude").GetRawText();
            using var forecast = JsonDocument.Parse(await GetAsync($"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,wind_speed_10m"));
            var now = forecast.RootElement.GetProperty("current");
            if (_config.WeatherEnabled) Set("Weather", place.GetProperty("name").GetString() + "\n" + now.GetProperty("temperature_2m") + " °C · wind " + now.GetProperty("wind_speed_10m") + " km/h\nOpen-Meteo · " + DateTime.Now.ToShortTimeString());
        }
        catch (Exception) { Set("Weather", "Weather unavailable. Check the city and connection."); }
        if (_config.NewsEnabled) try
        {
            var xml = XDocument.Parse(await GetAsync("https://feeds.bbci.co.uk/news/world/rss.xml"));
            if (_config.NewsEnabled) Set("News", string.Join("\n\n", xml.Descendants("item").Take(4).Select(i => (string?)i.Element("title"))) + "\n\nBBC World · " + DateTime.Now.ToShortTimeString());
        }
        catch (Exception) { Set("News", "News unavailable. Try refreshing later."); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus { public uint Length, Load; public ulong Total, Available, PageTotal, PageAvailable, VirtualTotal, VirtualAvailable, Extended; }
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
}
