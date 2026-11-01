using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace JARVIS;

/// <summary>Registers both global overlay shortcuts on one Windows message loop.</summary>
internal sealed class GlobalHotkey : IDisposable
{
    private uint _threadId;
    private readonly Thread _thread;
    public GlobalHotkey(Action toggleQuickBar, Action toggleDeck, Action<string> error)
    {
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
            var petRegistered = RegisterHotKey(IntPtr.Zero, 1, 0x4001, 0x20);
            var petError = Marshal.GetLastWin32Error();
            var deckRegistered = RegisterHotKey(IntPtr.Zero, 2, 0x4001, 0x4A);
            var deckError = Marshal.GetLastWin32Error();
            if (!petRegistered || !deckRegistered)
            {
                var message = string.Join(" ", new[] {
                    petRegistered ? "Alt+Space is ready." : "Alt+Space is unavailable: " + new Win32Exception(petError).Message,
                    deckRegistered ? "Alt+J is ready." : "Alt+J is unavailable: " + new Win32Exception(deckError).Message });
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
    public void Dispose()
    {
        if (_threadId != 0) PostThreadMessage(_threadId, 0x0012, IntPtr.Zero, IntPtr.Zero);
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
