using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Tables;
using System.Diagnostics;
namespace JARVIS;
public sealed class MarkdownChat : IDisposable
{
    private readonly double _fontSize;
    public MarkdownChat(double fontSize = 15) { _fontSize = Math.Clamp(fontSize, 10, 24); }
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private readonly List<Bitmap> _images = new();
    private readonly CancellationTokenSource _cancel = new();
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectCallback = async (context, token) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
            var ip = addresses.FirstOrDefault(PublicAddress) ?? throw new IOException("Image host must use a public address.");
            var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try { await socket.ConnectAsync(ip, context.DnsEndPoint.Port, token); return new NetworkStream(socket, true); }
            catch { socket.Dispose(); throw; }
        }
    }) { Timeout = TimeSpan.FromSeconds(20) };
    private static bool PublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        var b = address.GetAddressBytes();
        if (b.Length == 16) return (b[0] & 0xE0) == 0x20; // global unicast only
        return b[0] != 0 && b[0] != 10 && b[0] != 127 && b[0] < 224 && !(b[0] == 169 && b[1] == 254) && !(b[0] == 172 && b[1] >= 16 && b[1] <= 31) && !(b[0] == 192 && b[1] == 168) && !(b[0] == 100 && b[1] >= 64 && b[1] <= 127);
    }
    public Control Render(string markdown)
    {
        // Standalone image URLs become native image blocks; ordinary prose URLs remain links.
        markdown = Regex.Replace(markdown, @"(?im)^\s*(https://[^\s]+\.(?:png|jpg|jpeg|webp)(?:\?[^\s]*)?)\s*$", "![]($1)");
        var panel = new StackPanel { Spacing = 10 };
        Blocks(Markdown.Parse(markdown, Pipeline), panel);
        return panel;
    }
    public Control RenderLocalImage(string path)
    {
        var bitmap = new Bitmap(path);
        _images.Add(bitmap);
        return new Image { Source = bitmap, MaxHeight = 520, Stretch = Stretch.Uniform };
    }
    private void Blocks(ContainerBlock blocks, StackPanel panel)
    {
        foreach (var block in blocks)
        {
            if (block is FencedCodeBlock code)
            {
                var source = code.Lines.ToString();
                var copy = new Button { Content = "Copy code", Padding = new(10, 4), FontSize = 11, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
                copy.Click += async (_, _) =>
                {
                    try { if (TopLevel.GetTopLevel(copy)?.Clipboard is { } clipboard) { await clipboard.SetTextAsync(source); copy.Content = "Copied"; } }
                    catch (Exception) { copy.Content = "Copy unavailable"; }
                };
                var header = new Grid { ColumnDefinitions = new("*,Auto") };
                header.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(code.Info) ? "Code" : code.Info, FontSize = 11, Opacity = .7 });
                Grid.SetColumn(copy, 1); header.Children.Add(copy);
                panel.Children.Add(new Border { Background = Brush.Parse("#60000000"), CornerRadius = new(12), Padding = new(14), Child = new StackPanel { Spacing = 10, Children =
                {
                    header, new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    Content = new SelectableTextBlock { Text = source, FontFamily = new("Cascadia Mono,Consolas,monospace"), FontSize = 13, TextWrapping = TextWrapping.NoWrap } }
                } } });
            }
            else if (block is Table table)
            {
                var columns = Math.Max(1, table.ColumnDefinitions.Count);
                var grid = new Grid { ColumnDefinitions = new(string.Join(",", Enumerable.Repeat("*", columns))) };
                var index = 0;
                foreach (var row in table.OfType<TableRow>())
                {
                    grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    foreach (var cell in row.OfType<TableCell>())
                    {
                        var content = new StackPanel { Spacing = 6 }; Blocks(cell, content);
                        var border = new Border { Padding = new(10), BorderBrush = Brush.Parse("#40808080"), BorderThickness = new(0, 0, 0, 1), Background = row.IsHeader ? Brush.Parse("#20808080") : Brushes.Transparent, Child = content };
                        Grid.SetRow(border, index); Grid.SetColumn(border, Math.Clamp(cell.ColumnIndex, 0, columns - 1)); Grid.SetColumnSpan(border, Math.Clamp(cell.ColumnSpan, 1, columns)); grid.Children.Add(border);
                    }
                    index++;
                }
                panel.Children.Add(grid);
            }
            else if (block is QuoteBlock quote)
            {
                var inner = new StackPanel { Spacing = 8 }; Blocks(quote, inner);
                panel.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new(3, 0, 0, 0), Padding = new(14, 4), Child = inner });
            }
            else if (block is LeafBlock leaf)
            {
                var text = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = block is HeadingBlock heading ? _fontSize + 11 - heading.Level * 2 : _fontSize };
                if (block is HeadingBlock) text.FontWeight = FontWeight.Bold;
                if (leaf.Inline is not null) Inlines(leaf.Inline, text.Inlines!, panel, false, false);
                panel.Children.Add(text);
            }
            else if (block is ListBlock list)
            {
                var i = 1;
                foreach (var item in list.OfType<ContainerBlock>())
                {
                    var row = new Grid { ColumnDefinitions = new("28,*"), Margin = new(8, 0, 0, 0) };
                    row.Children.Add(new TextBlock { Text = list.IsOrdered ? $"{i++}." : "•" });
                    var content = new StackPanel { Spacing = 5 }; Blocks(item, content);
                    Grid.SetColumn(content, 1); row.Children.Add(content); panel.Children.Add(row);
                }
            }
            else if (block is ContainerBlock nested) Blocks(nested, panel);
        }
    }
    private void Inlines(ContainerInline container, InlineCollection target, StackPanel panel, bool bold, bool italic)
    {
        foreach (var inline in container)
        {
            if (inline is LinkInline link && link.IsImage && link.Url is not null)
            {
                var host = new ContentControl { Content = new TextBlock { Text = "Loading image…" } };
                panel.Children.Add(host); _ = LoadImageAsync(link.Url, host);
            }
            else if (inline is LinkInline normalLink && normalLink.Url is not null)
            {
                var label = string.Concat(normalLink.OfType<LiteralInline>().Select(part => part.Content.ToString()));
                AddLink(target, normalLink.Url, string.IsNullOrWhiteSpace(label) ? normalLink.Url : label);
            }
            else if (inline is AutolinkInline auto && !auto.IsEmail) AddLink(target, auto.Url, auto.Url);
            else if (inline is LiteralInline literalUrl && Regex.IsMatch(literalUrl.Content.ToString(), @"https?://"))
            {
                var value = literalUrl.Content.ToString(); var offset = 0;
                foreach (Match match in Regex.Matches(value, @"https?://[^\s<>]+"))
                {
                    target.Add(new Run(value[offset..match.Index]));
                    AddLink(target, match.Value.TrimEnd('.', ',', ')'), match.Value);
                    offset = match.Index + match.Length;
                }
                target.Add(new Run(value[offset..]));
            }
            else if (inline is EmphasisInline emphasis) Inlines(emphasis, target, panel, emphasis.DelimiterCount == 2 || bold, emphasis.DelimiterCount == 1 || italic);
            else if (inline is ContainerInline nested) Inlines(nested, target, panel, bold, italic);
            else target.Add(new Run(inline switch { LiteralInline literal => literal.Content.ToString(), CodeInline code => code.Content, LineBreakInline => "\n", _ => inline.ToString() ?? "" }) { FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, FontStyle = italic ? FontStyle.Italic : FontStyle.Normal });
        }
    }
    private static void AddLink(InlineCollection target, string url, string label)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(uri.UserInfo))
        { target.Add(new Run(label)); return; }
        var button = new Button { Content = label, Padding = new(0, 1), FontSize = 14, Background = Brushes.Transparent, BorderThickness = new(0) };
        ToolTip.SetTip(button, uri.Host);
        button.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true }); } catch (Exception) { ToolTip.SetTip(button, "Could not open your browser"); } };
        target.Add(new InlineUIContainer(button));
    }
    private async Task LoadImageAsync(string url, ContentControl host)
    {
        try
        {
            var uri = new Uri(url);
            if (uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) throw new IOException("Unsupported image URL.");
            using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, _cancel.Token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentType?.MediaType?.StartsWith("image/") != true) throw new IOException("URL is not an image.");
            using var data = new MemoryStream();
            await using var stream = await response.Content.ReadAsStreamAsync(_cancel.Token);
            var buffer = new byte[8192]; int read;
            while ((read = await stream.ReadAsync(buffer, _cancel.Token)) > 0)
            { if (data.Length + read > 10_000_000) throw new IOException("Image exceeds 10 MB."); await data.WriteAsync(buffer.AsMemory(0, read), _cancel.Token); }
            data.Position = 0;
            var bitmap = Bitmap.DecodeToWidth(data, 1024);
            if (_cancel.IsCancellationRequested) { bitmap.Dispose(); return; }
            _images.Add(bitmap);
            host.Content = new Image { Source = bitmap, MaxHeight = 420, Stretch = Stretch.Uniform };
        }
        catch (Exception) { if (!_cancel.IsCancellationRequested) host.Content = new TextBlock { Text = "Image unavailable" }; }
    }
    public void Dispose() { _cancel.Cancel(); foreach (var image in _images) image.Dispose(); _images.Clear(); }
}
