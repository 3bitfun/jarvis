# J.A.R.V.I.S. icon library

46 original icons share a 24 × 24 viewbox, 1.7-unit strokes, rounded line caps, and rounded joins. Filled pin/star variants show active states. The app mark is a custom J silhouette with a small companion sparkle; it uses no font outlines or system font dependency.

- `icons.json`: editable source paths, also embedded by the app.
- `svg/`: individual SVGs using `currentColor`; change the root `color` attribute or inherit it from CSS.
- `png/dark/24`, `48`, `96`: transparent light-colored icons for dark backgrounds.
- `png/light/24`, `48`, `96`: transparent dark-colored icons for light backgrounds.
- `brand/`: full-color app mark at 16, 24, 32, 48, 64, 128, 256, 512, and 1024 pixels, plus editable `jarvis.svg` and a multi-resolution `jarvis.ico`.
- `icon-sheet.png`: visual catalog of the whole set.

Edit the path data in `icons.json`, then run this from the repository root:

```powershell
.\scripts\Export-Icons.ps1
```

The exporter creates every SVG and PNG from those paths and refreshes `Assets/Jarvis.png` and `Assets/Jarvis.ico`. It needs no SVG package, image service, or browser. Native Avalonia UI icons use `AppIcon` to draw the same geometry directly and adapt to theme colors:

```xml
<local:AppIcon Kind="settings" Width="18" Height="18" />
<local:AppIcon Kind="logo" Foreground="{DynamicResource AccentBrush}" />
```

`AppIcon.Label("image", "Generate image")` creates an icon-and-label button content in C#. Give icon-only buttons an accessible name and tooltip. Exported assets are checked into Git; the application embeds the source manifest and its brand icon rather than loading hundreds of PNGs at runtime.
