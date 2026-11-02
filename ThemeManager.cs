using Avalonia;
using Avalonia.Media;
namespace JARVIS;
public static class ThemeManager
{
    public static void ApplyFont(string family) => Application.Current!.Resources["AppFont"] = new FontFamily(family);
    public static readonly string[] Profiles = { "Minimalist", "Terminal", "ChatGPT Style", "GitHub Style", "Neon", "Obsidian Glass", "Neon Arc", "Gritty Stone", "Industrial Rust", "Stark HUD", "Ocean", "Violet", "Amber", "Custom" };
    public static void Apply(string profile, string accent, string tint)
    {
        var colors = profile switch
        {
            "Minimalist" => ("#E4E4E7", "#18181B"),
            "Terminal" => ("#C9A7EB", "#111113"),
            "ChatGPT Style" => ("#7ED0B1", "#212121"),
            "GitHub Style" => ("#58A6FF", "#0D1117"),
            "Neon" => ("#FC5EDB", "#100C22"),
            "Neon Arc" => ("#43DCEE", "#151C24"),
            "Gritty Stone" => ("#B98B87", "#202022"),
            "Industrial Rust" => ("#D6A275", "#241F1D"),
            "Stark HUD" => ("#48DDF5", "#071523"),
            "Ocean" => ("#75D5FF", "#101D2B"),
            "Violet" => ("#C8A3FF", "#1D1729"),
            "Amber" => ("#F3BC66", "#251D16"),
            "Custom" => (accent, tint),
            _ => ("#D5E9A4", "#17191B")
        };
        var tintColor = Color.Parse(colors.Item2);
        var solidTint = Color.FromRgb(tintColor.R, tintColor.G, tintColor.B);
        var resources = Application.Current!.Resources;
        resources["AccentBrush"] = new SolidColorBrush(Color.Parse(colors.Item1));
        resources["GlassTintColor"] = solidTint;
        resources["GlassMaterial"] = new ExperimentalAcrylicMaterial
        {
            BackgroundSource = AcrylicBackgroundSource.Digger,
            TintColor = solidTint, TintOpacity = 0.22, MaterialOpacity = 0.18,
            FallbackColor = Color.FromArgb(90, tintColor.R, tintColor.G, tintColor.B)
        };
        // Alpha surfaces allow the acrylic to remain visible underneath the content.
        resources["GlassBrush"] = new SolidColorBrush(Color.FromArgb(70, tintColor.R, tintColor.G, tintColor.B));
        resources["GlassHighlightBrush"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops { new(Color.Parse("#0CFFFFFF"), 0), new(Color.Parse("#00FFFFFF"), 0.6), new(Color.Parse("#05000000"), 1) }
        };
        resources["PanelBrush"] = new SolidColorBrush(Color.Parse("#09000000"));
        resources["RaisedBrush"] = new SolidColorBrush(Color.Parse("#0EFFFFFF"));
        resources["SettingsBrush"] = new SolidColorBrush(Color.FromArgb(250, tintColor.R, tintColor.G, tintColor.B));
        resources["LineBrush"] = new SolidColorBrush(Color.Parse("#1CFFFFFF"));
        resources["TextBrush"] = new SolidColorBrush(Color.Parse("#F2F3EF"));
        resources["MutedBrush"] = new SolidColorBrush(Color.Parse("#A5ACA8"));
        resources["SecondaryBrush"] = resources["MutedBrush"];
    }
}
