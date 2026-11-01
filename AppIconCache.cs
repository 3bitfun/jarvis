using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
namespace JARVIS;

/// <summary>Copies Windows shell icons into Avalonia bitmaps, releasing every GDI handle.</summary>
public sealed class AppIconCache : IDisposable
{
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _icons = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;
    private readonly List<Bitmap> _owned = new();
    public Task<Bitmap?> GetAsync(string path) => _icons.GetOrAdd(path, p => Task.Run(() => Extract(p)));
    private Bitmap? Extract(string path)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var initialized = CoInitializeEx(IntPtr.Zero, 0) >= 0;
        IntPtr icon = IntPtr.Zero, dc = IntPtr.Zero, dib = IntPtr.Zero, previous = IntPtr.Zero;
        try
        {
            if (SHGetFileInfo(path, 0, out var info, (uint)Marshal.SizeOf<ShellInfo>(), 0x100 | 0x0) == IntPtr.Zero) return null;
            icon = info.Icon;
            dc = CreateCompatibleDC(IntPtr.Zero);
            var bitmapInfo = new BitmapInfo { Size = 40, Width = 32, Height = -32, Planes = 1, BitCount = 32 };
            dib = CreateDIBSection(dc, ref bitmapInfo, 0, out var bits, IntPtr.Zero, 0);
            if (dc == IntPtr.Zero || dib == IntPtr.Zero || bits == IntPtr.Zero) return null;
            previous = SelectObject(dc, dib);
            Marshal.Copy(new byte[32 * 32 * 4], 0, bits, 32 * 32 * 4);
            if (!DrawIconEx(dc, 0, 0, icon, 32, 32, 0, IntPtr.Zero, 3)) return null;
            var pixels = new byte[32 * 32 * 4]; Marshal.Copy(bits, pixels, 0, pixels.Length);
            var bitmap = new WriteableBitmap(new PixelSize(32, 32), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (var target = bitmap.Lock())
                for (var row = 0; row < 32; row++) Marshal.Copy(pixels, row * 128, IntPtr.Add(target.Address, row * target.RowBytes), 128);
            lock (_icons)
            {
                if (_disposed) { bitmap.Dispose(); return null; }
                _owned.Add(bitmap);
                return bitmap;
            }
        }
        catch (Exception) { return null; }
        finally
        {
            if (previous != IntPtr.Zero && dc != IntPtr.Zero) SelectObject(dc, previous);
            if (dib != IntPtr.Zero) DeleteObject(dib);
            if (dc != IntPtr.Zero) DeleteDC(dc);
            if (icon != IntPtr.Zero) DestroyIcon(icon);
            if (initialized) CoUninitialize();
        }
    }
    public void Dispose()
    {
        lock (_icons)
        {
            _disposed = true;
            foreach (var bitmap in _owned) bitmap.Dispose();
            _owned.Clear();
        }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellInfo { public IntPtr Icon; public int IconIndex; public uint Attributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, ImageSize; public int XPels, YPels; public uint Used, Important; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfoW")] private static extern IntPtr SHGetFileInfo(string path, uint attributes, out ShellInfo info, uint size, uint flags);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("user32.dll")] private static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon, int width, int height, uint step, IntPtr brush, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
}
