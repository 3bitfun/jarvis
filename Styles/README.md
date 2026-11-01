# Editing the J.A.R.V.I.S. interface

Avalonia uses XAML styles instead of CSS. All files below are in the project source folder.

## Shared stylesheet

`Styles/Controls.axaml` is the CSS equivalent. It is loaded by `App.axaml` and styles both the full deck and quick bar.

Examples:

```xml
<Style Selector="Button.primary">
  <Setter Property="CornerRadius" Value="12"/>
  <Setter Property="Padding" Value="18,12"/>
</Style>

<Style Selector="ListBox.quickResults ListBoxItem">
  <Setter Property="MinHeight" Value="64"/>
</Style>
```

The selectors `Button.primary`, `Button.quiet`, `TextBlock.muted`, `TextBlock.label`, `ListBox.quickResults`, and `Border.keycap` work like CSS classes. Apply one with `Classes="primary"`. Pseudo classes such as `:pointerover`, `:selected`, and `:disabled` style interaction states.

Resource values must match the property type: use `<CornerRadius x:Key="RadiusSm">6</CornerRadius>` for a corner radius, `<Thickness>` for padding, and `<x:Double>` for numeric values such as font size or opacity. Avalonia does not convert a `DynamicResource` of type `Double` into `CornerRadius`; a mismatch can crash startup even when compilation succeeds.

## Colors and typography

`ThemeManager.cs` defines the live theme palettes. The resource names `AccentBrush`, `GlassBrush`, `PanelBrush`, `RaisedBrush`, `LineBrush`, `TextBrush`, and `MutedBrush` are used by the stylesheet. Runtime theme changes replace these resources, so edit this class to change a built-in palette. Custom theme colors and font selection can also be edited in the app's Settings.

The default theme is **Obsidian Glass**: low-tint desktop acrylic, translucent layers, soft lime accents, and bundled Inter typography. `GlassMaterial` controls acrylic tint and material opacity, `GlassHighlightBrush` supplies a faint gradient sheen, and `SettingsBrush` keeps the settings drawer readable. Both windows request `AcrylicBlur` with a transparent fallback. `Program.cs` explicitly enables the Windows composition backends that support blur, and `GlassBackdrop.cs` reports the actual active transparency mode in Settings → Style.

The curated picker is defined in `FontCatalog.cs`. Inter, Roboto, Manrope, Outfit, Plus Jakarta Sans, and DM Sans are embedded under `Assets/Fonts` and work without installation. Regular, Medium, SemiBold, and Bold instances are included. SF Pro and SF Pro Display use installed copies, and the UI reports when they are unavailable. Custom lets you enter an exact installed font family name. The first launch with the curated picker switches the prior font selection to Inter; subsequent choices persist.

Use `{DynamicResource AccentBrush}` in XAML for colors that follow theme changes. Local properties on a specific control override stylesheet setters.

## Layout and content

- `MiniLauncherWindow.axaml`: quick bar layout, icons, result rows, keyboard badges, and footer.
- `MainWindow.axaml`: full chat deck, sidebar, composer, orb, and Settings tabs.
- `MarkdownChat.cs`: generated Markdown text and code block styling.
- `AppIconCache.cs`: native Windows shell icon extraction; missing icons use the app's initial.

## Rebuild

Changes to source XAML require a rebuild; editing files beside a published executable does not update its compiled styles. Close running J.A.R.V.I.S. copies, use `manager.bat` → Build Windows EXE, then run `dist/win-x64/JARVIS.exe` from this project.

## Quick bar controls

- Alt+Space: show the top-edge pet pill; press again to expand or tuck it away. Typing into the idle pill opens its input bar.
- Alt+J: open or close the full deck.
- Alt+1 through Alt+5: launch that numbered visible app result (also supports Alt+numpad 1–5).
- Up/Down: select an app; Enter: launch it.
- Ctrl+Enter or Ask J.A.R.V.I.S.: send the search text as a chat message.
- Escape: tuck the input/reply into the idle capsule; press again to hide it.

Number shortcuts act only inside the quick bar while app results are visible; they never launch apps from the AI reply view.
