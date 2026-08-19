using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalMind.Providers;
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

    public async Task RefreshAsync()
        => Status = await Task.Run(() => _foundry.IsReadyAsync(Alias)) ? "Ready" : "Download";

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (Status == "Ready" || IsBusy)
            return;

        IsBusy = true;
        try
        {
            await _foundry.DownloadAsync(Alias, p => _dispatcher.TryEnqueue(() => Status = $"{(int)p}%"));
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

    public ObservableCollection<FoundryModelVM> FoundryModels { get; } = [];
    public ObservableCollection<string> OllamaModels { get; } = [];

    [ObservableProperty]
    public partial string OllamaStatus { get; set; }

    public event Action<string>? ModelReady;

    private bool _refreshing;

    public SettingsViewModel(FoundryLocalProvider foundry, OllamaProvider ollama)
    {
        _ollama = ollama;
        OllamaStatus = "Checking…";
        foreach (var (alias, displayName) in FoundryLocalProvider.Curated)
            FoundryModels.Add(new FoundryModelVM(foundry, alias, displayName, name => ModelReady?.Invoke(name)));
    }

    public async Task RefreshAsync()
    {
        if (_refreshing)
            return;
        _refreshing = true;
        try
        {
            foreach (var vm in FoundryModels)
                await vm.RefreshAsync();

            var models = await Task.Run(() => _ollama.TryGetModelsAsync());
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
            return;

        OllamaModels.Clear();
        foreach (var name in incoming)
            OllamaModels.Add(name);
    }
}
