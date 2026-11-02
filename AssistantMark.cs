using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace JARVIS;

public sealed class AssistantMark : Control
{
    public bool AnimationsEnabled { get; set; } = true;
    public string Activity { get; set; } = "Idle";
    public double AnimationSpeed { get; set; } = 1;
    public double AnimationIntensity { get; set; } = 1;
    private readonly DispatcherTimer _pulse = new() { Interval = TimeSpan.FromMilliseconds(50) };
    public AssistantMark()
    {
        Width = Height = 72;
        _pulse.Tick += (_, _) => { if (IsEffectivelyVisible) InvalidateVisual(); };
        AttachedToVisualTree += (_, _) => _pulse.Start();
        DetachedFromVisualTree += (_, _) => _pulse.Stop();
    }
    public override void Render(DrawingContext context)
    {
        var verity = Application.Current?.Resources.TryGetResource("VerityMode", null, out var value) == true && value is true;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = Math.Min(Bounds.Width, Bounds.Height) * .42;
        var accent = verity ? Brushes.Gold : (IBrush?)Application.Current?.Resources["AccentBrush"] ?? Brushes.Cyan;
        if (Activity == "Success") accent = Brushes.MediumSpringGreen;
        else if (Activity == "Error") accent = Brushes.Salmon;
        var speed = Activity switch { "Thinking" => 4.0, "Listening" => 3.0, "Speaking" => 3.5, "Success" => 5.0, "Error" => 1.0, _ => 2.0 };
        var intensity = Math.Clamp(AnimationIntensity, 0, 2);
        var pulse = AnimationsEnabled ? .96 + .035 * intensity * Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * speed * Math.Clamp(AnimationSpeed, .25, 3)) : .96;
        context.DrawEllipse(null, new Pen(accent, 1), center, radius, radius);
        context.DrawEllipse(verity ? Brushes.Gold : null, new Pen(accent, 2), center, radius * .8 * pulse, radius * .8 * pulse);
        if (verity)
        {
            var blink = AnimationsEnabled && DateTime.UtcNow.TimeOfDay.TotalSeconds % 5 < .16 ? .015 : .13;
            context.DrawEllipse(Brushes.Black, null, center + new Vector(-radius * .24, -radius * .13), radius * .075, radius * blink);
            context.DrawEllipse(Brushes.Black, null, center + new Vector(radius * .24, -radius * .13), radius * .075, radius * blink);
            var smile = new StreamGeometry();
            using (var g = smile.Open()) { g.BeginFigure(center + new Vector(-radius * .35, radius * .15), false); g.QuadraticBezierTo(center + new Vector(0, radius * .65), center + new Vector(radius * .35, radius * .15)); g.EndFigure(false); }
            context.DrawGeometry(null, new Pen(Brushes.Black, 2.5), smile);
        }
        else
        {
            for (var i = 0; i < 12; i++)
            {
                var a = i * Math.PI / 6;
                context.DrawLine(new Pen(accent, 2), center + new Vector(Math.Cos(a) * radius * .9, Math.Sin(a) * radius * .9), center + new Vector(Math.Cos(a) * radius, Math.Sin(a) * radius));
            }
            context.DrawEllipse(null, new Pen(accent, 2), center, radius * .42, radius * .42);
            context.DrawEllipse(accent, null, center, radius * .16, radius * .16);
        }
    }
}
