using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalMind.Models;
using LocalMind.Providers;
using LocalMind.Services;
using Microsoft.UI.Dispatching;

namespace LocalMind.ViewModels;

public partial class FoundryModelVM : ObservableObject
{
    private readonly FoundryLocalProvider _foundry;
    private readonly Action<string> _onReady;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();

    public string Alias { get; }
    public string DisplayName { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(CanDelete))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial string Status { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial bool IsBusy { get; set; }

    public bool IsReady => Status == "Ready";
    public bool CanDelete => IsReady && !IsBusy;
    private bool CanDownload => !IsReady && !IsBusy;

    public event Action Deleted;

    public FoundryModelVM(FoundryLocalProvider foundry, string alias, string displayName, Action<string> onReady)
    {
        _foundry = foundry;
        _onReady = onReady;
        Alias = alias;
        DisplayName = displayName;
        Status = "…";
    }

    public async Task Refresh()
        => Status = await _foundry.IsReady(Alias) ? "Ready" : "Download";

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task Download()
    {
        IsBusy = true;
        try
        {
            await _foundry.Download(Alias, p => _dispatcher.TryEnqueue(() => Status = $"{(int)p}%"));
            Status = "Ready";
            _onReady(DisplayName);
        }
        catch (Exception ex)
        {
            AppLog.Warning($"Foundry model download failed for '{Alias}'.", ex);
            Status = "Download";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Delete()
    {
        IsBusy = true;
        try
        {
            await _foundry.Delete(Alias);
            Status = "Download";
            Deleted?.Invoke();
        }
        catch (Exception ex)
        {
            AppLog.Warning($"Foundry model delete failed for '{Alias}'.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly OllamaProvider _ollama;
    private readonly AppSettings _settings;

    public ObservableCollection<FoundryModelVM> FoundryModels { get; } = [];
    public ObservableCollection<string> OllamaModels { get; } = [];
    public IReadOnlyList<string> ThemeOptions { get; } = ["System", "Light", "Dark"];
    public string AppVersion => $"LocalMind v{AppInfo.Version}";

    [ObservableProperty]
    public partial string OllamaStatus { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingModels { get; set; }

    [ObservableProperty]
    public partial string SystemPrompt { get; set; }

    [ObservableProperty]
    public partial string Theme { get; set; }

    [ObservableProperty]
    public partial bool StartMinimized { get; set; }

    [ObservableProperty]
    public partial bool MinimizeToTrayOnClose { get; set; }

    [ObservableProperty]
    public partial bool RunAtStartup { get; set; }

    public event Action<string> ModelReady;
    public event Action ModelsChanged;

    public SettingsViewModel(FoundryLocalProvider foundry, OllamaProvider ollama)
    {
        _ollama = ollama;
        _settings = SettingsStore.Load();
        SystemPrompt = _settings.SystemPrompt;
        Theme = _settings.Theme;
        StartMinimized = _settings.StartMinimized;
        MinimizeToTrayOnClose = _settings.MinimizeToTrayOnClose;
        RunAtStartup = StartupManager.IsEnabled();
        OllamaStatus = "Checking…";
        foreach (var (alias, displayName) in FoundryLocalProvider.Curated)
        {
            var model = new FoundryModelVM(foundry, alias, displayName, name => ModelReady?.Invoke(name));
            model.Deleted += () => ModelsChanged?.Invoke();
            FoundryModels.Add(model);
        }
    }

    private void Save() => SettingsStore.Save(_settings);

    partial void OnSystemPromptChanged(string value)
    {
        if (_settings.SystemPrompt == value)
        {
            return;
        }
        _settings.SystemPrompt = value;
        Save();
    }

    partial void OnThemeChanged(string value)
    {
        if (_settings.Theme == value)
        {
            return;
        }
        _settings.Theme = value;
        Save();
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        if (_settings.StartMinimized == value)
        {
            return;
        }
        _settings.StartMinimized = value;
        Save();
    }

    partial void OnMinimizeToTrayOnCloseChanged(bool value)
    {
        if (_settings.MinimizeToTrayOnClose == value)
        {
            return;
        }
        _settings.MinimizeToTrayOnClose = value;
        Save();
    }

    partial void OnRunAtStartupChanged(bool value)
    {
        if (StartupManager.IsEnabled() == value)
        {
            return;
        }

        try
        {
            StartupManager.Set(value);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to update run-at-startup setting.", ex);
            RunAtStartup = StartupManager.IsEnabled();
        }
    }

    [RelayCommand]
    private static void OpenLogs() => AppLog.OpenDirectory();

    [RelayCommand]
    private static Task CheckForUpdates() => UpdateService.CheckForUpdates();

    public async Task Refresh()
    {
        var foundryRefresh = Task.WhenAll(FoundryModels.Select(model => model.Refresh()));
        var ollamaRefresh = _ollama.TryGetModels();
        await Task.WhenAll(foundryRefresh, ollamaRefresh);

        var models = await ollamaRefresh;
        if (models is null)
        {
            OllamaStatus = "Not detected";
            OllamaModels.Clear();
        }
        else
        {
            OllamaStatus = "Connected";
            SyncOllamaModels(models.Select(m => m.DisplayName));
        }
    }

    // Only mutate the collection when it actually changed, to avoid rebuilding the list (flicker).
    private void SyncOllamaModels(IEnumerable<string> names)
    {
        List<string> incoming = [.. names];
        if (OllamaModels.SequenceEqual(incoming))
        {
            return;
        }

        OllamaModels.Clear();
        foreach (var name in incoming)
        {
            OllamaModels.Add(name);
        }
    }
}
