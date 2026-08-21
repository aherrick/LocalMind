using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace LocalMind.Services;

public static class NotificationService
{
    private static volatile bool _registered;

    public static void Register()
    {
        try
        {
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch (Exception ex)
        {
            _registered = false;
            AppLog.Warning("Notification registration failed.", ex);
        }
    }

    public static void Show(string title, string message)
    {
        if (!_registered)
        {
            return;
        }
        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(message)
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            AppLog.Warning("Showing notification failed.", ex);
        }
    }
}
