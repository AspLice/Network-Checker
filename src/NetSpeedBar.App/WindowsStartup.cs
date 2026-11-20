using Microsoft.Win32;

namespace NetSpeedBar.App;

internal static class WindowsStartup
{
    private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "NetSpeedBar";
    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled && Environment.ProcessPath is { } executable) key.SetValue(ValueName, $"\"{executable}\"");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
