using Microsoft.Win32;

namespace LocalMind.Services;

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LocalMind";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is not null;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            // Point at Velopack's Update.exe so the entry survives version updates; fall back to the exe when unpackaged.
            var updateExe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Update.exe"));
            var command = File.Exists(updateExe) ? $"\"{updateExe}\" start" : $"\"{Environment.ProcessPath}\"";
            key.SetValue(ValueName, command);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
