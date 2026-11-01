using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Globalization;
using System.Text;

namespace JARVIS;

internal static class IconExport
{
    private sealed class Canvas(Action<DrawingContext, Size> draw) : Control
    {
        public override void Render(DrawingContext context) => draw(context, Bounds.Size);
    }
    public static void Export(string directory)
    {
        directory = Path.GetFullPath(directory);
        var svgFolder = Path.Combine(directory, "svg"); Directory.CreateDirectory(svgFolder);
        foreach (var icon in AppIcon.Definitions)
        {
            var paths = string.Join("\n  ", icon.Paths.Select(path => path.Fill ? $"<path d=\"{path.D}\" fill=\"currentColor\" stroke=\"none\"/>" : $"<path d=\"{path.D}\"/>"));
            File.WriteAllText(Path.Combine(svgFolder, icon.Name + ".svg"), $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" width=\"24\" height=\"24\" fill=\"none\" stroke=\"currentColor\" color=\"#E6ECE4\" stroke-width=\"1.7\" stroke-linecap=\"round\" stroke-linejoin=\"round\" role=\"img\" aria-label=\"{icon.Name}\">\n  {paths}\n</svg>\n", new UTF8Encoding(false));
            foreach (var theme in new[] { "dark", "light" })
            foreach (var size in new[] { 24, 48, 96 })
            {
                var folder = Path.Combine(directory, "png", theme, size.ToString()); Directory.CreateDirectory(folder);
                Save(Path.Combine(folder, icon.Name + ".png"), size, size, (context, bounds) => AppIcon.Draw(context, icon.Name, new Rect(bounds), Brush.Parse(theme == "dark" ? "#E6ECE4" : "#22342D")));
            }
        }
        var brandFolder = Path.Combine(directory, "brand"); Directory.CreateDirectory(brandFolder);
        foreach (var size in new[] { 16, 24, 32, 48, 64, 128, 256, 512, 1024 })
            Save(Path.Combine(brandFolder, $"jarvis-{size}.png"), size, size, DrawBrand);
        var logo = AppIcon.Definitions.Single(icon => icon.Name == "logo");
        var logoPaths = string.Join("", logo.Paths.Select(path => path.Fill ? $"<path d=\"{path.D}\" fill=\"#D5E9A4\"/>" : $"<path d=\"{path.D}\" fill=\"none\" stroke=\"#D5E9A4\" stroke-width=\"1.7\" stroke-linecap=\"round\"/>"));
        File.WriteAllText(Path.Combine(brandFolder, "jarvis.svg"), $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 128 128\"><defs><linearGradient id=\"glass\" x2=\"1\" y2=\"1\"><stop stop-color=\"#34483D\"/><stop offset=\"1\" stop-color=\"#101916\"/></linearGradient></defs><rect x=\"2\" y=\"2\" width=\"124\" height=\"124\" rx=\"34\" fill=\"url(#glass)\" stroke=\"#72866A\" stroke-width=\"1\"/><g transform=\"translate(23.04 23.04) scale(3.413333)\">{logoPaths}</g></svg>\n");
        WriteIco(Path.Combine(brandFolder, "jarvis.ico"), brandFolder);
        var assets = Path.GetDirectoryName(directory)!;
        File.Copy(Path.Combine(brandFolder, "jarvis-512.png"), Path.Combine(assets, "Jarvis.png"), true);
        File.Copy(Path.Combine(brandFolder, "jarvis.ico"), Path.Combine(assets, "Jarvis.ico"), true);
        Save(Path.Combine(directory, "icon-sheet.png"), 1200, 970, DrawSheet);
        Console.WriteLine($"Exported {AppIcon.Definitions.Count} SVG icons, {AppIcon.Definitions.Count * 6} transparent PNGs, nine brand sizes, multi-resolution ICO, and icon-sheet.png.");
    }
    private static void DrawBrand(DrawingContext context, Size size)
    {
        var s = size.Width;
        var gradient = new LinearGradientBrush { StartPoint = new(0, 0, RelativeUnit.Relative), EndPoint = new(1, 1, RelativeUnit.Relative), GradientStops = new() { new(Color.Parse("#34483D"), 0), new(Color.Parse("#101916"), 1) } };
        context.DrawRectangle(gradient, new Pen(Brush.Parse("#72866A"), s / 128), new Rect(s / 64, s / 64, s * 31 / 32, s * 31 / 32), s * .265, s * .265);
        AppIcon.Draw(context, "logo", new Rect(s * .18, s * .18, s * .64, s * .64), Brush.Parse("#D5E9A4"));
    }
    private static void DrawSheet(DrawingContext context, Size bounds)
    {
        context.FillRectangle(Brush.Parse("#111A17"), new Rect(bounds));
        DrawBrand(context, new Size(84, 84));
        Text(context, "JARVIS / ICON LIBRARY", 114, 28, 26, "#E6ECE4");
        Text(context, $"{AppIcon.Definitions.Count} original vectors · SVG + transparent PNG · 24 px grid", 114, 68, 14, "#A6B5AD");
        for (var i = 0; i < AppIcon.Definitions.Count; i++)
        {
            var x = 28 + i % 8 * 146; var y = 144 + i / 8 * 130;
            context.DrawRectangle(Brush.Parse("#1B2822"), new Pen(Brush.Parse("#34463B"), 1), new Rect(x, y, 132, 112), 15, 15);
            AppIcon.Draw(context, AppIcon.Definitions[i].Name, new Rect(x + 48, y + 19, 36, 36), Brush.Parse("#D5E9A4"));
            var label = new FormattedText(AppIcon.Definitions[i].Name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Brush.Parse("#C5D1C8"));
            context.DrawText(label, new Point(x + (132 - label.Width) / 2, y + 78));
        }
        Text(context, "Same source paths in Avalonia, SVG, and PNG. Theme-aware in the app.", 28, 944, 12, "#A6B5AD");
    }
    private static void Text(DrawingContext context, string value, double x, double y, double size, string color) =>
        context.DrawText(new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, Brush.Parse(color)), new Point(x, y));
    private static void Save(string path, int width, int height, Action<DrawingContext, Size> draw)
    {
        var control = new Canvas(draw); control.Measure(new Size(width, height)); control.Arrange(new Rect(0, 0, width, height));
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height)); bitmap.Render(control); bitmap.Save(path);
    }
    private static void WriteIco(string path, string brandFolder)
    {
        var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
        var images = sizes.Select(size => File.ReadAllBytes(Path.Combine(brandFolder, $"jarvis-{size}.png"))).ToArray();
        using var writer = new BinaryWriter(File.Create(path)); writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
        var offset = 6 + sizes.Length * 16;
        for (var i = 0; i < sizes.Length; i++)
        {
            writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)0); writer.Write((byte)0);
            writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
        }
        foreach (var image in images) writer.Write(image);
    }
}
