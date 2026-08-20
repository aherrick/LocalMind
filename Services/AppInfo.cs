using System.Diagnostics;

namespace LocalMind.Services;

public static class AppInfo
{
    public const string RepositoryUrl = "https://github.com/aherrick/LocalMind";

    public static string Version => typeof(AppInfo).Assembly.GetName().Version.ToString(3);

    public static void OpenRepository()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = RepositoryUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to open the LocalMind repository.", ex);
        }
    }
}