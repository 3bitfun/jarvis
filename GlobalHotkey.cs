using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace JARVIS;

/// <summary>Registers both global overlay shortcuts on one Windows message loop.</summary>
internal sealed class GlobalHotkey : IDisposable
{
    private uint _threadId;
    private readonly Thread _thread;
    public GlobalHotkey(Action toggleQuickBar, Action toggleDeck, Action<string> error, string quick = "Alt+Space", string deck = "Alt+J")
    {
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
            var q = Parse(quick); var d = Parse(deck);
            var petRegistered = RegisterHotKey(IntPtr.Zero, 1, q.Modifiers, q.Key);
            var petError = Marshal.GetLastWin32Error();
            var deckRegistered = RegisterHotKey(IntPtr.Zero, 2, d.Modifiers, d.Key);
            var deckError = Marshal.GetLastWin32Error();
            if (!petRegistered || !deckRegistered)
            {
                var message = string.Join(" ", new[] {
                    petRegistered ? "" + quick + " is ready." : "" + quick + " is unavailable: " + new Win32Exception(petError).Message,
                    deckRegistered ? "" + deck + " is ready." : "" + deck + " is unavailable: " + new Win32Exception(deckError).Message });
                Dispatcher.UIThread.Post(() => error(message + " Use the J.A.R.V.I.S. tray menu if a shortcut is in use by another app."));
                if (!petRegistered && !deckRegistered) return;
            }
            try
            {
                while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                    if (message.Message == 0x0312)
                        Dispatcher.UIThread.Post(message.WParam.ToUInt64() == 1 ? toggleQuickBar : toggleDeck);
            }
            finally { UnregisterHotKey(IntPtr.Zero, 1); UnregisterHotKey(IntPtr.Zero, 2); }
        }) { IsBackground = true, Name = "JARVIS global hotkey" };
        _thread.Start();
    }
    internal static (uint Modifiers, uint Key) Parse(string value)
    {
        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        uint modifiers = 0x4000;
        foreach (var part in parts.SkipLast(1)) modifiers |= part.ToLowerInvariant() switch
        { "alt" => 1u, "ctrl" => 2u, "shift" => 4u, "win" => 8u, _ => throw new ArgumentException("Use Alt, Ctrl, Shift or Win modifiers.") };
        var key = parts.LastOrDefault()?.ToUpperInvariant() ?? "";
        uint code = key == "SPACE" ? 0x20u : key.Length == 1 && char.IsAsciiLetterOrDigit(key[0]) ? (uint)key[0] : 0u;
        if (code == 0 || modifiers == 0x4000) throw new ArgumentException("Use a modifier and a letter, number or Space.");
        return (modifiers, code);
    }
    public void Dispose()
    {
        if (_threadId != 0) PostThreadMessage(_threadId, 0x0012, IntPtr.Zero, IntPtr.Zero);
        if (_thread.IsAlive && Thread.CurrentThread != _thread) _thread.Join(1000);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Msg { public IntPtr Hwnd; public uint Message; public UIntPtr WParam; public IntPtr LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] private static extern int GetMessage(out Msg message, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Msg message, IntPtr hwnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
