using System.Runtime.InteropServices;

namespace NetSpeedBar.App;

internal static class ForegroundWindowDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint handle, out Rect rect);

    public static bool IsForegroundWindowFullscreen()
    {
        var handle = GetForegroundWindow();
        if (handle == nint.Zero || !GetWindowRect(handle, out var rect)) return false;
        var screen = System.Windows.Forms.Screen.FromHandle(handle).Bounds;
        const int tolerance = 2;
        return rect.Left <= screen.Left + tolerance && rect.Top <= screen.Top + tolerance &&
               rect.Right >= screen.Right - tolerance && rect.Bottom >= screen.Bottom - tolerance;
    }
}
