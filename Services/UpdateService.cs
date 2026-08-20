using Velopack;
using Velopack.Sources;

namespace LocalMind.Services;

public sealed class UpdateService
{
    private UpdateManager _manager;
    private UpdateInfo _pending;

    public event Action UpdateReady;
    public event Action NoUpdateAvailable;

    public async Task Check()
    {
        try
        {
            _manager = new UpdateManager(new GithubSource(AppInfo.RepositoryUrl, null, prerelease: false));
            if (!_manager.IsInstalled)
            {
                // Velopack can only update an installed build, so dev/debug runs no-op here.
                AppLog.Info("Update check skipped: app is not installed via Velopack.");
                return;
            }

            _pending = await _manager.CheckForUpdatesAsync();
            if (_pending is null)
            {
                AppLog.Info("No updates available.");
                NoUpdateAvailable?.Invoke();
                return;
            }

            await _manager.DownloadUpdatesAsync(_pending);
            UpdateReady?.Invoke();
        }
        catch (Exception ex)
        {
            AppLog.Warning("Update check failed.", ex);
        }
    }

    public void ApplyAndRestart()
    {
        if (_manager is not null && _pending is not null)
        {
            try
            {
                AppLog.Info($"Applying update to {_pending.TargetFullRelease.Version}.");
                _manager.ApplyUpdatesAndRestart(_pending);
            }
            catch (Exception ex)
            {
                AppLog.Error("Failed to apply update and restart.", ex);
            }
        }
    }
}
