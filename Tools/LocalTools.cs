using System.ComponentModel;

namespace LocalMind.Tools;

public static class LocalTools
{
    [Description("Gets the current local date and time, including the time zone.")]
    public static string GetCurrentDateTime()
    {
        var now = DateTimeOffset.Now;
        return $"Local date/time: {now:F} ({TimeZoneInfo.Local.DisplayName})";
    }
}
