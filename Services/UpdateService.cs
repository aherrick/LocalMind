using Velopack;
using Velopack.Sources;

namespace LocalMind.Services;

public static class UpdateService
{
    public static async Task CheckForUpdates()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(AppInfo.RepositoryUrl, null, prerelease: false));
            if (!manager.IsInstalled)
            {
                // Velopack can only update an installed build, so dev/debug runs no-op here.
                AppLog.Info("Update check skipped: app is not installed via Velopack.");
                return;
            }

            var update = await manager.CheckForUpdatesAsync();
            if (update is null)
            {
                AppLog.Info("No updates available.");
                await Dialogs.Message("You're up to date", "LocalMind is running the latest version.");
                return;
            }

            AppLog.Info($"Update {update.TargetFullRelease.Version} is available.");
            if (!await Dialogs.Confirm(
                    "Update available",
                    "A new version of LocalMind is available. Download and install it now?",
                    "Update"))
            {
                return;
            }

            AppLog.Info($"Downloading update {update.TargetFullRelease.Version}.");
            await manager.DownloadUpdatesAsync(update);
            AppLog.Info($"Applying update {update.TargetFullRelease.Version}.");
            manager.ApplyUpdatesAndRestart(update);
        }
        catch (Exception ex)
        {
            AppLog.Warning("Update failed.", ex);
        }
    }
}
