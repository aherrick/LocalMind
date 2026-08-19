using Velopack;

namespace LocalMind.Services;

public sealed class UpdateService
{
    private const string UpdateUrl = "";

    private UpdateManager? _manager;
    private UpdateInfo? _pending;

    public event Action? UpdateReady;

    public async Task CheckAsync()
    {
        if (string.IsNullOrWhiteSpace(UpdateUrl))
            return;

        try
        {
            _manager = new UpdateManager(UpdateUrl);
            if (!_manager.IsInstalled)
                return;

            _pending = await _manager.CheckForUpdatesAsync();
            if (_pending is null)
                return;

            await _manager.DownloadUpdatesAsync(_pending);
            UpdateReady?.Invoke();
        }
        catch
        {
        }
    }

    public void ApplyAndRestart()
    {
        if (_manager is not null && _pending is not null)
            _manager.ApplyUpdatesAndRestart(_pending);
    }
}
