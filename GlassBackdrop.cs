using Avalonia.Controls;
using Avalonia.Media;
namespace JARVIS;
public static class GlassBackdrop
{
    public static void Configure(Window window, Action<string>? report = null)
    {
        window.Background = Brushes.Transparent;
        window.TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Transparent };
        // This is used only when no transparency is available at all, not painted over a working blur.
        window.TransparencyBackgroundFallback = new SolidColorBrush(Color.Parse("#E6191B1D"));
        void Update() => report?.Invoke(window.ActualTransparencyLevel == WindowTransparencyLevel.AcrylicBlur
            ? "Desktop acrylic blur is active."
            : window.ActualTransparencyLevel == WindowTransparencyLevel.Transparent
                ? "Transparent glass is active; this desktop session did not provide blur."
                : "This desktop session is using the readable opaque fallback.");
        window.Opened += (_, _) => Update();
        window.PropertyChanged += (_, e) => { if (e.Property == TopLevel.ActualTransparencyLevelProperty) Update(); };
        Update();
    }
}
