using Avalonia.Input;
namespace JARVIS;
internal static class LauncherChecks
{
    public static async Task RunAsync()
    {
        var keys = new[] { Key.D1, Key.D2, Key.D3, Key.D4, Key.D5 };
        var pad = new[] { Key.NumPad1, Key.NumPad2, Key.NumPad3, Key.NumPad4, Key.NumPad5 };
        for (var i = 0; i < 5; i++)
            if (MiniLauncherWindow.ShortcutIndex(keys[i]) != i || MiniLauncherWindow.ShortcutIndex(pad[i]) != i) throw new Exception("Shortcut mapping failed.");
        if (MiniLauncherWindow.ShortcutIndex(Key.D6) != -1) throw new Exception("Unexpected shortcut mapping.");
        var visible = new[] { new LauncherResult(new LaunchableApp("Assigned", "assigned"), 3), new LauncherResult(new LaunchableApp("Other", "other"), 2) };
        if (MiniLauncherWindow.VisibleShortcutResult(visible, 0) is not null || MiniLauncherWindow.VisibleShortcutResult(visible, 1)?.App.Name != "Other") throw new Exception("Visible shortcut did not match its displayed number.");
        if (AppLauncher.MatchScore("Google Chrome", "gch") <= 0 || AppLauncher.MatchScore("Google Chrome", "Chrome") <= AppLauncher.MatchScore("Google Chrome", "gch")) throw new Exception("Search ranking failed.");
        if (PetQuickAction.Parse("start a focus timer for 25 minutes") is not { Kind: PetActionKind.Timer, Minutes: 25 } ||
            PetQuickAction.Parse("start timer for 0 minutes") is not { Kind: PetActionKind.Invalid } ||
            PetQuickAction.Parse("start a 15 minute timer") is not { Kind: PetActionKind.Timer, Minutes: 15 } ||
            PetQuickAction.Parse("stop my timer") is not { Kind: PetActionKind.StopTimer } ||
            PetQuickAction.Parse("timer status") is not { Kind: PetActionKind.TimerStatus } ||
            PetQuickAction.Parse("create a note: desktop ideas") is not { Kind: PetActionKind.Note, Desktop: false, Value: "desktop ideas" } ||
            PetQuickAction.Parse("make a note on my desktop: Hello") is not { Kind: PetActionKind.Note, Desktop: true, Value: "Hello" } ||
            PetQuickAction.Parse("tell me how to open Notepad") is not null)
            throw new Exception("Pet action recognition failed.");
        Console.WriteLine("Shortcut mappings and search ranking passed. Indexing apps…");
        var launcher = new AppLauncher(); await launcher.RefreshAsync();
        var results = launcher.Search("");
        if (OperatingSystem.IsWindows() && results.Count == 0) throw new Exception("No apps indexed.");
        using var cache = new AppIconCache();
        var icons = 0;
        foreach (var app in results.Take(5))
        {
            Console.WriteLine("Extracting icon: " + app.Name);
            if (await cache.GetAsync(app.Path) is not null) icons++;
        }
        if (OperatingSystem.IsWindows() && icons == 0) throw new Exception("Native app icon extraction failed.");
        Console.WriteLine($"Passed Alt+1–5 and numpad mappings, fuzzy ranking, app indexing, and native icon extraction ({icons} icons).");
    }
}
