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

    public void SetReady(bool isReady)
    {
        if (!IsBusy)
        {
            Status = isReady ? "Ready" : "Download";
        }
    }

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
    private readonly AppSettings _settings;

    public ObservableCollection<FoundryModelVM> FoundryModels { get; } = [];
    public ObservableCollection<string> OllamaModels { get; } = [];
    public ObservableCollection<string> LlamaCppModels { get; } = [];
    public IReadOnlyList<string> ThemeOptions { get; } = ["System", "Light", "Dark"];
    public string AppVersion => $"LocalMind v{AppInfo.Version}";

    [ObservableProperty]
    public partial string OllamaStatus { get; set; }

    [ObservableProperty]
    public partial string LlamaCppStatus { get; set; }

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

    public SettingsViewModel(FoundryLocalProvider foundry)
    {
        _settings = SettingsStore.Load();
        SystemPrompt = _settings.SystemPrompt;
        Theme = _settings.Theme;
        StartMinimized = _settings.StartMinimized;
        MinimizeToTrayOnClose = _settings.MinimizeToTrayOnClose;
        RunAtStartup = StartupManager.IsEnabled();
        OllamaStatus = "Checking…";
        LlamaCppStatus = "Checking…";
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

    public void ApplyProviderStatuses(IReadOnlyDictionary<string, LocalProviderStatus> statuses)
    {
        var foundry = statuses["foundry"];
        foreach (var model in FoundryModels)
        {
            model.SetReady(foundry.Models.Any(ready => ready.Id == model.Alias));
        }

        var ollama = statuses["ollama"];
        OllamaStatus = ollama.IsAvailable ? "Connected" : "Not detected";
        Sync(OllamaModels, ollama.Models.Select(model => model.DisplayName));

        var llama = statuses["llamacpp"];
        LlamaCppStatus = llama.IsAvailable ? "Connected" : "Not detected";
        Sync(LlamaCppModels, llama.Models.Select(model => model.DisplayName));
    }

    // Only mutate the collection when it actually changed, to avoid rebuilding the list (flicker).
    private static void Sync(ObservableCollection<string> target, IEnumerable<string> names)
    {
        List<string> incoming = [.. names];
        if (target.SequenceEqual(incoming))
        {
            return;
        }

        target.Clear();
        foreach (var name in incoming)
        {
            target.Add(name);
        }
    }
}
