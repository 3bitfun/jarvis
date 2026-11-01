using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using System.Text.Json;

namespace JARVIS;

public sealed record IconStroke(string D, bool Fill);
public sealed record IconDefinition(string Name, List<IconStroke> Paths);

/// <summary>Native theme-aware vectors drawn from the same paths as the exported SVG/PNG assets.</summary>
public sealed class AppIcon : Control
{
    public static readonly StyledProperty<string> KindProperty = AvaloniaProperty.Register<AppIcon, string>(nameof(Kind), "sparkles");
    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<AppIcon, IBrush?>(nameof(Foreground), Brushes.White);
    public string Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    internal static IReadOnlyList<IconDefinition> Definitions { get; } = Load();
    private static readonly Dictionary<string, (Geometry Geometry, bool Fill)[]> Shapes = Definitions.ToDictionary(icon => icon.Name,
        icon => icon.Paths.Select(path => (Geometry.Parse(path.D), path.Fill)).ToArray(), StringComparer.Ordinal);
    static AppIcon() => AffectsRender<AppIcon>(KindProperty, ForegroundProperty);
    public AppIcon() { Width = Height = 18; IsHitTestVisible = false; VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center; }
    private static IReadOnlyList<IconDefinition> Load()
    {
        using var stream = AssetLoader.Open(new Uri("avares://JARVIS/Assets/Icons/icons.json"));
        return JsonSerializer.Deserialize<List<IconDefinition>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
    internal static void Draw(DrawingContext context, string name, Rect target, IBrush brush)
    {
        if (!Shapes.TryGetValue(name, out var paths)) return;
        var size = Math.Min(target.Width, target.Height);
        using (context.PushTransform(Matrix.CreateScale(size / 24, size / 24) * Matrix.CreateTranslation(target.X + (target.Width - size) / 2, target.Y + (target.Height - size) / 2)))
        {
            var pen = new Pen(brush, 1.7, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            foreach (var (geometry, fill) in paths) context.DrawGeometry(fill ? brush : null, fill ? null : pen, geometry);
        }
    }
    public override void Render(DrawingContext context) => Draw(context, Kind, new Rect(Bounds.Size), Foreground ?? Brushes.White);
    public static Control Label(string icon, string text) => new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8,
        Children = { new AppIcon { Kind = icon }, new TextBlock { Text = text, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center } } };
}
