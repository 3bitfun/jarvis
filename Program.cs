using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace JARVIS;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--export-icons") { BuildAvaloniaApp().SetupWithoutStarting(); IconExport.Export(args[1]); return; }
        if (args.Length > 0 && args[0].StartsWith("--check-"))
            Environment.SetEnvironmentVariable("JARVIS_DATA_DIR", Path.Combine(Path.GetTempPath(), "JARVIS-check-run-" + Guid.NewGuid().ToString("N")));
        if (args.Length == 1 && args[0] == "--check-readiness") { ReadinessChecks.RunAsync().GetAwaiter().GetResult(); return; }
        if (args.Length == 1 && args[0] == "--check-fonts-glass")
        {
            BuildAvaloniaApp().SetupWithoutStarting();
            foreach (var font in FontCatalog.Create().Where(f => f.Available && f.Family.StartsWith("avares:")))
            {
                var face = new Avalonia.Media.Typeface(new Avalonia.Media.FontFamily(font.Family)).GlyphTypeface;
                Console.WriteLine(font.Name + " loaded as " + face.FamilyName);
                if (!face.FamilyName.Equals(font.Name, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Font fell back: " + font.Name);
            }
            var window = new Window { Width = 300, Height = 100, SystemDecorations = SystemDecorations.None, ShowInTaskbar = false };
            GlassBackdrop.Configure(window, Console.WriteLine);
            window.Show(); Dispatcher.UIThread.RunJobs();
            Console.WriteLine("Actual compositor transparency: " + window.ActualTransparencyLevel);
            window.TransparencyLevelHint = new[] { WindowTransparencyLevel.None }; window.Background = Avalonia.Media.Brushes.Black; Dispatcher.UIThread.RunJobs();
            if (window.ActualTransparencyLevel == WindowTransparencyLevel.AcrylicBlur || window.ActualTransparencyLevel == WindowTransparencyLevel.Blur || window.ActualTransparencyLevel == WindowTransparencyLevel.Mica) throw new InvalidOperationException("Solid mode did not disable the window backdrop.");
            Console.WriteLine("Solid window backdrop: " + window.ActualTransparencyLevel);
            window.Close(); return;
        }
        if (args.Length == 1 && args[0] == "--check-startup")
        {
            BuildAvaloniaApp().SetupWithoutStarting();
            var main = new MainWindow(false);
            main.CheckLayoutSizing();
            main.CheckPrivateDrafts();
            main.CheckComposerKeys();
            using var markdown = new MarkdownChat();
            var link = (StackPanel)markdown.Render("[Documentation](https://example.org/docs)");
            if (link.Children.OfType<TextBlock>().Single().Inlines!.OfType<Avalonia.Controls.Documents.InlineUIContainer>().Count() != 1) throw new InvalidOperationException("Markdown links are not rendered as native clickable links.");
            var table = (StackPanel)markdown.Render("| Name | Value |\n|---|---|\n| A | B |");
            if (!table.Children.OfType<Grid>().Any()) throw new InvalidOperationException("Markdown tables are not rendered as grids.");
            var mini = new MiniLauncherWindow();
            mini.SetGlass(false);
            if (mini.FindControl<ExperimentalAcrylicBorder>("PillAcrylic")!.IsVisible || mini.TransparencyLevelHint[0] == WindowTransparencyLevel.AcrylicBlur) throw new InvalidOperationException("Quick chat left blur enabled in solid mode.");
            mini.FindControl<TextBox>("IdleInput")!.Text = "hello";
            Dispatcher.UIThread.RunJobs();
            if (mini.FindControl<TextBox>("QueryBox")!.Text != "hello" || mini.Width <= 344)
                throw new InvalidOperationException("Typing into the idle pill did not open the input bar.");
            main.Close(); mini.Close();
            Console.WriteLine("Main window and pet pill initialized; typing into the idle pill opens the input bar.");
            return;
        }
        if (args.Length == 1 && args[0] == "--check-launcher")
        {
            BuildAvaloniaApp().SetupWithoutStarting();
            Task.Run(LauncherChecks.RunAsync).GetAwaiter().GetResult();
            return;
        }
        if (args.Length == 1 && args[0] == "--check-features")
        {
            FeatureChecks.RunAsync().GetAwaiter().GetResult();
            return;
        }
        if (args.Length == 1 && args[0] == "--check-memory")
        {
            var directory = Path.Combine(Path.GetTempPath(), "JARVIS-memory-check-" + Guid.NewGuid().ToString("N"));
            var memory = new ConversationMemory(directory);
            memory.Add("My name is Alex", "Hello Alex");
            memory.Compact(1, "The user's name is Alex.");
            var first = memory.ActiveId;
            memory.NewChat();
            memory.Add("Second chat", "Ready");
            memory = new ConversationMemory(directory);
            if (memory.Chats.Count != 2 || memory.Turns[0].User != "Second chat") throw new Exception("Chat restore failed");
            memory.SelectChat(first);
            if (memory.Turns.Count != 1 || memory.CompactedCount != 1 || !memory.Summary.Contains("Alex")) throw new Exception("Chat isolation failed");
            Console.WriteLine("Saved chats, active selection, and compact memory restored successfully.");
            return;
        }
        if (args.Length == 2 && args[0] == "--check-model")
        {
            using var engine = new LLamaEngine(Path.Combine(Path.GetTempPath(), "JARVIS-check-" + Guid.NewGuid().ToString("N")));
            engine.SwitchModel(args[1]);
            Console.WriteLine("Model loaded successfully.");
            var firstReply = engine.GenerateAsync("For this conversation, remember that my favorite color is teal. Reply briefly.").GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(firstReply)) throw new InvalidOperationException("The local model returned an empty reply.");
            Console.WriteLine(firstReply);
            engine.Config.DeliberateMode = false; // Verify preferences can change without reloading weights.
            var followUp = engine.GenerateAsync("What is my favorite color? Answer with only the color.").GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(followUp)) throw new InvalidOperationException("The local model returned an empty follow-up.");
            Console.WriteLine(followUp);
            using (var stop = new CancellationTokenSource())
            {
                try { engine.GenerateAsync("Write a long numbered list of animals.", stop.Token, progress: _ => stop.Cancel()).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { }
                if (!stop.IsCancellationRequested) throw new InvalidOperationException("No streaming output was received before cancellation.");
            }
            var afterStop = engine.GenerateAsync("Reply briefly with OK.").GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(afterStop)) throw new InvalidOperationException("The local model did not recover after cancellation.");
            var loadedPath = engine.ActiveModelPath;
            try { engine.SwitchModel(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N") + ".gguf")); throw new InvalidOperationException("A missing model was accepted."); }
            catch (FileNotFoundException) { }
            if (engine.ActiveModelPath != loadedPath) throw new InvalidOperationException("A missing model selection unloaded the working model.");
            Console.WriteLine("Cancellation and a subsequent reply succeeded.");
            return;
        }
        if (args.Length == 3 && args[0] == "--check-switch-model")
        {
            using var engine = new LLamaEngine(Path.Combine(Path.GetTempPath(), "JARVIS-switch-check-" + Guid.NewGuid().ToString("N")));
            engine.Config.MemoryEnabled = false; engine.Config.ResponseMode = "Instant";
            engine.SwitchModel(args[1]);
            var first = engine.GenerateAsync("Remember my favorite color is teal. Reply briefly.").GetAwaiter().GetResult();
            engine.SwitchModel(args[2]);
            var second = engine.GenerateAsync("What is my favorite color? Reply with the color.").GetAwaiter().GetResult();
            engine.SwitchModel(args[1]);
            var third = engine.GenerateAsync("Reply briefly with OK.").GetAwaiter().GetResult();
            if (new[] { first, second, third }.Any(string.IsNullOrWhiteSpace) || engine.Conversation.Count != 3)
                throw new InvalidOperationException("Chat failed across model switches.");
            Console.WriteLine("Model switching, conversation continuity, and switching back succeeded.");
            Console.WriteLine("Follow-up reply: " + second);
            return;
        }

        using var instance = new DesktopInstance();
        if (!instance.IsPrimary) { instance.ActivateExistingAsync().GetAwaiter().GetResult(); return; }
        DesktopInstance.Current = instance;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) AppDiagnostics.Write(ex); };
        TaskScheduler.UnobservedTaskException += (_, e) => AppDiagnostics.Write(e.Exception);
        try { BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        catch (Exception ex) { AppDiagnostics.Write(ex); throw; }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                RenderingMode = new[] { Win32RenderingMode.AngleEgl, Win32RenderingMode.Software },
                CompositionMode = new[] { Win32CompositionMode.WinUIComposition, Win32CompositionMode.DirectComposition, Win32CompositionMode.RedirectionSurface },
                WinUICompositionBackdropCornerRadius = 24
            }).LogToTrace();
}
