using Avalonia.Media;
namespace JARVIS;
public sealed record CuratedFont(string Name, string Description, string Family, bool Available = true)
{
    public override string ToString() => Name;
}
public static class FontCatalog
{
    public const string Inter = "avares://JARVIS/Assets/Fonts/inter#Inter";
    public static IReadOnlyList<CuratedFont> Create()
    {
        bool Installed(string family) => FontManager.Current.SystemFonts.Any(f => f.Name.Equals(family, StringComparison.OrdinalIgnoreCase));
        var sf = Installed("SF Pro") ? "SF Pro" : "SF Pro Text";
        return new CuratedFont[]
        {
            new("Inter", "Neutral and crisp · bundled", Inter),
            new("SF Pro", Installed(sf) ? "Apple system sans · installed" : "Apple system sans · not installed", sf, Installed(sf)),
            new("SF Pro Display", Installed("SF Pro Display") ? "Display sans · installed" : "Display sans · not installed", "SF Pro Display", Installed("SF Pro Display")),
            new("Roboto", "Familiar and balanced · bundled", "avares://JARVIS/Assets/Fonts/roboto#Roboto"),
            new("Manrope", "Geometric and open · bundled", "avares://JARVIS/Assets/Fonts/manrope#Manrope"),
            new("Outfit", "Soft geometric sans · bundled", "avares://JARVIS/Assets/Fonts/outfit#Outfit"),
            new("Plus Jakarta Sans", "Friendly geometric sans · bundled", "avares://JARVIS/Assets/Fonts/plusjakartasans#Plus Jakarta Sans"),
            new("DM Sans", "Warm, minimal sans · bundled", "avares://JARVIS/Assets/Fonts/dmsans#DM Sans"),
            new("System default", "Native desktop typography", FontFamily.Default.Name),
            new("Custom…", "Use an installed font by name", "custom")
        };
    }
}
