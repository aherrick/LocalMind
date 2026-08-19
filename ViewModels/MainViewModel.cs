using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalMind.Models;
using LocalMind.Providers;
using LocalMind.Services;
using Microsoft.UI.Dispatching;

namespace LocalMind.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IReadOnlyList<ILocalModelProvider> _providers;
    private readonly ChatStore _store;
    private readonly NotificationService _notifications;
    private readonly UpdateService _updates;
    private readonly Func<bool> _isWindowVisible;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly List<ChatViewModel> _all = [];

    public ObservableCollection<ChatViewModel> PinnedChats { get; } = [];
    public ObservableCollection<ChatViewModel> Chats { get; } = [];
    public ObservableCollection<LocalModel> ReadyModels { get; } = [];
    public SettingsViewModel Settings { get; }

    public bool HasPinned => PinnedChats.Count > 0;

    [ObservableProperty]
    public partial ChatViewModel? SelectedChat { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial bool IsSettingsOpen { get; set; }

    [ObservableProperty]
    public partial bool UpdateReady { get; set; }

    public MainViewModel(
        FoundryLocalProvider foundry,
        OllamaProvider ollama,
        ChatStore store,
        NotificationService notifications,
        UpdateService updates,
        Func<bool> isWindowVisible)
    {
        _providers = [foundry, ollama];
        _store = store;
        _notifications = notifications;
        _updates = updates;
        _isWindowVisible = isWindowVisible;
        SearchText = "";

        Settings = new SettingsViewModel(foundry, ollama);
        Settings.ModelReady += name =>
        {
            _notifications.Show("Model ready", $"{name} finished downloading.");
            _ = RefreshReadyModelsAsync();
        };
        _updates.UpdateReady += () => _dispatcher.TryEnqueue(() => UpdateReady = true);
    }

    public async Task InitializeAsync()
    {
        foreach (var chat in _store.Load())
            _all.Add(CreateChatViewModel(chat));
        Refilter();

        SelectedChat = PinnedChats.FirstOrDefault() ?? Chats.FirstOrDefault();
        if (SelectedChat is null)
            NewChat();
        else
            _ = SelectedChat.LoadSavedModelAsync();

        _ = RefreshStartupAsync();
        _ = _updates.CheckAsync();
    }

    private async Task RefreshStartupAsync()
    {
        await RefreshReadyModelsAsync();
        await Settings.RefreshAsync();
    }

    private ChatViewModel CreateChatViewModel(Chat chat)
        => new(chat, _providers, _store, _notifications, _isWindowVisible, ReadyModels);

    public async Task RefreshReadyModelsAsync()
    {
        var models = new List<LocalModel>();
        foreach (var provider in _providers)
            models.AddRange(await Task.Run(() => provider.GetModelsAsync()));

        ReadyModels.Clear();
        foreach (var model in models)
            ReadyModels.Add(model);
    }

    [RelayCommand]
    public void NewChat()
    {
        var vm = CreateChatViewModel(new Chat());
        _all.Insert(0, vm);
        SearchText = "";
        Refilter();
        SelectedChat = vm;
        IsSettingsOpen = false;
    }

    partial void OnSelectedChatChanged(ChatViewModel? value)
    {
        if (value is not null)
        {
            IsSettingsOpen = false;
            _ = value.LoadSavedModelAsync();
        }
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        IsSettingsOpen = true;
        await Settings.RefreshAsync();
    }

    [RelayCommand]
    private void DeleteChat(ChatViewModel? vm)
    {
        if (vm is null)
            return;

        _store.Delete(vm.Model);
        _all.Remove(vm);
        Refilter();

        if (SelectedChat == vm)
            SelectedChat = PinnedChats.FirstOrDefault() ?? Chats.FirstOrDefault();
        if (_all.Count == 0)
            NewChat();
    }

    [RelayCommand]
    private void TogglePin(ChatViewModel? vm)
    {
        if (vm is null)
            return;
        vm.IsPinned = !vm.IsPinned;
        Refilter();
    }

    partial void OnSearchTextChanged(string value) => Refilter();

    private void Refilter()
    {
        var keep = SelectedChat;
        var query = SearchText?.Trim() ?? "";
        IEnumerable<ChatViewModel> match = _all;
        if (query.Length > 0)
            match = _all.Where(c => c.Matches(query));

        var visible = match.ToList();
        Sync(PinnedChats, visible.Where(c => c.IsPinned).ToList());
        Sync(Chats, visible.Where(c => !c.IsPinned).ToList());
        OnPropertyChanged(nameof(HasPinned));

        if (keep is not null && _all.Contains(keep))
            SelectedChat = keep;
    }

    private static void Sync(ObservableCollection<ChatViewModel> target, IList<ChatViewModel> desired)
    {
        for (int i = target.Count - 1; i >= 0; i--)
            if (!desired.Contains(target[i]))
                target.RemoveAt(i);
        for (int i = 0; i < desired.Count; i++)
        {
            var item = desired[i];
            var current = target.IndexOf(item);
            if (current < 0)
                target.Insert(i, item);
            else if (current != i)
                target.Move(current, i);
        }
    }

    [RelayCommand]
    private void RestartForUpdate() => _updates.ApplyAndRestart();
}
