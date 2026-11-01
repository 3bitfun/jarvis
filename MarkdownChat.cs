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
namespace JARVIS;
public sealed class MarkdownChat : IDisposable
{
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
        markdown = Regex.Replace(markdown, @"(?m)^\s*(https?://[^\s]+)\s*$", "![]($1)");
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
                panel.Children.Add(new Border { Background = Brush.Parse("#60000000"), CornerRadius = new(8), Padding = new(12), Child = new SelectableTextBlock { Text = code.Lines.ToString(), FontFamily = new("Consolas,monospace"), TextWrapping = TextWrapping.Wrap } });
            else if (block is LeafBlock leaf)
            {
                var text = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = block is HeadingBlock heading ? 26 - heading.Level * 2 : 15 };
                if (block is HeadingBlock) text.FontWeight = FontWeight.Bold;
                if (leaf.Inline is not null) Inlines(leaf.Inline, text.Inlines!, panel, false, false);
                panel.Children.Add(text);
            }
            else if (block is ListBlock list)
            {
                var i = 1;
                foreach (var item in list.OfType<ContainerBlock>())
                {
                    var row = new StackPanel { Spacing = 5, Margin = new(12, 0, 0, 0) };
                    row.Children.Add(new TextBlock { Text = list.IsOrdered ? $"{i++}." : "•" });
                    Blocks(item, row); panel.Children.Add(row);
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
            else if (inline is AutolinkInline auto && !auto.IsEmail)
            {
                var host = new ContentControl(); panel.Children.Add(host); _ = LoadImageAsync(auto.Url, host);
            }
            else if (inline is LiteralInline literalUrl && Regex.IsMatch(literalUrl.Content.ToString(), @"https?://"))
            {
                var value = literalUrl.Content.ToString(); var offset = 0;
                foreach (Match match in Regex.Matches(value, @"https?://[^\s<>]+"))
                {
                    target.Add(new Run(value[offset..match.Index]));
                    var host = new ContentControl { Content = new TextBlock { Text = "Loading image…" } };
                    panel.Children.Add(host); _ = LoadImageAsync(match.Value.TrimEnd('.', ',', ')'), host);
                    offset = match.Index + match.Length;
                }
                target.Add(new Run(value[offset..]));
            }
            else if (inline is EmphasisInline emphasis) Inlines(emphasis, target, panel, emphasis.DelimiterCount == 2 || bold, emphasis.DelimiterCount == 1 || italic);
            else if (inline is ContainerInline nested) Inlines(nested, target, panel, bold, italic);
            else target.Add(new Run(inline switch { LiteralInline literal => literal.Content.ToString(), CodeInline code => code.Content, LineBreakInline => "\n", _ => inline.ToString() ?? "" }) { FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, FontStyle = italic ? FontStyle.Italic : FontStyle.Normal });
        }
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
