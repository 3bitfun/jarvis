using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace JARVIS;

/// <summary>Second launches activate the existing window instead of competing for hotkeys.</summary>
internal sealed class DesktopInstance : IDisposable
{
    public static DesktopInstance? Current { get; set; }
    private readonly Mutex _mutex;
    private readonly string _name;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _sync = new();
    private Action? _activate;
    private bool _pending;
    public bool IsPrimary { get; }
    public DesktopInstance()
    {
        var identity = AppPaths.DataDirectory;
        _name = "JARVIS-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..20];
        _mutex = new Mutex(true, _name, out var created); IsPrimary = created;
        if (created) _ = Task.Run(ListenAsync);
    }
    public void Attach(Action activate)
    {
        bool pending;
        lock (_sync) { _activate = activate; pending = _pending; _pending = false; }
        if (pending) activate();
    }
    public async Task ActivateExistingAsync()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _name, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(timeout.Token); await client.WriteAsync(new byte[] { 1 }, timeout.Token);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException) { AppDiagnostics.Write(ex); }
    }
    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(_name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_stop.Token);
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token); requestTimeout.CancelAfter(TimeSpan.FromSeconds(3));
                var signal = new byte[1];
                if (await server.ReadAsync(signal, requestTimeout.Token) == 0) continue;
                Action? action;
                lock (_sync) { action = _activate; if (action is null) _pending = true; }
                action?.Invoke();
            }
            catch (OperationCanceledException) { }
            catch (IOException ex) { AppDiagnostics.Write(ex); await Task.Delay(250); }
        }
    }
    public void Dispose()
    {
        _stop.Cancel();
        if (IsPrimary) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}

internal static class AppDiagnostics
{
    public static string DataDirectory => AppPaths.DataDirectory;
    public static void Write(Exception exception)
    {
        try
        {
            var directory = Path.Combine(DataDirectory, "logs"); Directory.CreateDirectory(directory);
            // Deliberately omit exception messages, which can contain provider responses or credentials.
            File.AppendAllText(Path.Combine(directory, $"app-{DateTime.UtcNow:yyyyMMdd}.log"), $"{DateTime.UtcNow:O} {exception.GetType().FullName}\n{exception.StackTrace}\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
