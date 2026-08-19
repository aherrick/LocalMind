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
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public FoundryModelVM(FoundryLocalProvider foundry, string alias, string displayName, Action<string> onReady)
    {
        _foundry = foundry;
        _onReady = onReady;
        Alias = alias;
        DisplayName = displayName;
        Status = "…";
    }

    public async Task Refresh()
        => Status = await Task.Run(() => _foundry.IsReady(Alias)) ? "Ready" : "Download";

    [RelayCommand]
    private async Task Download()
    {
        if (Status == "Ready" || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _foundry.Download(Alias, p => _dispatcher.TryEnqueue(() => Status = $"{(int)p}%"));
            Status = "Ready";
            _onReady(DisplayName);
        }
        catch
        {
            Status = "Download";
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
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;

    public ObservableCollection<FoundryModelVM> FoundryModels { get; } = [];
    public ObservableCollection<string> OllamaModels { get; } = [];
    public IReadOnlyList<string> ThemeOptions { get; } = ["System", "Light", "Dark"];

    [ObservableProperty]
    public partial string OllamaStatus { get; set; }

    [ObservableProperty]
    public partial string SystemPrompt { get; set; }

    [ObservableProperty]
    public partial string Theme { get; set; }

    [ObservableProperty]
    public partial bool StartMinimized { get; set; }

    public event Action<string> ModelReady;
    public event Action<string> ThemeChanged;

    private bool _refreshing;

    public SettingsViewModel(FoundryLocalProvider foundry, OllamaProvider ollama, SettingsStore settingsStore)
    {
        _ollama = ollama;
        _settingsStore = settingsStore;
        _settings = settingsStore.Load();
        SystemPrompt = _settings.SystemPrompt;
        Theme = _settings.Theme;
        StartMinimized = _settings.StartMinimized;
        OllamaStatus = "Checking…";
        foreach (var (alias, displayName) in FoundryLocalProvider.Curated)
        {
            FoundryModels.Add(new FoundryModelVM(foundry, alias, displayName, name => ModelReady?.Invoke(name)));
        }
    }

    private void Save() => _settingsStore.Save(_settings);

    partial void OnSystemPromptChanged(string value)
    {
        _settings.SystemPrompt = value;
        Save();
    }

    partial void OnThemeChanged(string value)
    {
        _settings.Theme = value;
        Save();
        ThemeChanged?.Invoke(value);
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        _settings.StartMinimized = value;
        Save();
    }

    public async Task Refresh()
    {
        if (_refreshing)
        {
            return;
        }
        _refreshing = true;
        try
        {
            foreach (var vm in FoundryModels)
            {
                await vm.Refresh();
            }

            var models = await Task.Run(() => _ollama.TryGetModels());
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
        finally
        {
            _refreshing = false;
        }
    }

    // Only mutate the collection when it actually changed, to avoid rebuilding the list (flicker).
    private void SyncOllamaModels(IEnumerable<string> names)
    {
        var incoming = names.ToList();
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
