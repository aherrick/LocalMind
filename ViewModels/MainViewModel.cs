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
    private readonly List<ILocalModelProvider> _providers;
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
    public partial ChatViewModel SelectedChat { get; set; }

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
        SettingsStore settingsStore,
        Func<bool> isWindowVisible)
    {
        _providers = [foundry, ollama];
        _store = store;
        _notifications = notifications;
        _updates = updates;
        _isWindowVisible = isWindowVisible;
        SearchText = "";

        Settings = new SettingsViewModel(foundry, ollama, settingsStore);
        Settings.ModelReady += name =>
        {
            _notifications.Show("Model ready", $"{name} finished downloading.");
            _ = RefreshReadyModels();
        };
        _updates.UpdateReady += () => _dispatcher.TryEnqueue(() => UpdateReady = true);
        _updates.NoUpdateAvailable += () => _dispatcher.TryEnqueue(() =>
            _notifications.Show("You're on latest", "LocalMind is up to date."));
    }

    public void Initialize()
    {
        foreach (var chat in _store.Load())
        {
            _all.Add(CreateChatViewModel(chat));
        }
        Refilter();

        SelectedChat = PinnedChats.FirstOrDefault() ?? Chats.FirstOrDefault();
        if (SelectedChat is null)
        {
            NewChat();
        }
        _ = RefreshStartup();
    }

    private async Task RefreshStartup()
    {
        await RefreshReadyModels();
        await Settings.Refresh();
    }

    private ChatViewModel CreateChatViewModel(Chat chat)
    {
        var viewModel = new ChatViewModel(
            chat, _providers, _store, _notifications, _isWindowVisible, ReadyModels, Settings);
        viewModel.Started += AddStartedChat;
        return viewModel;
    }

    private void AddStartedChat(ChatViewModel chat)
    {
        if (_all.Contains(chat))
        {
            return;
        }

        _all.Insert(0, chat);
        Refilter();
        _dispatcher.TryEnqueue(() =>
        {
            if (ReferenceEquals(SelectedChat, chat))
            {
                OnPropertyChanged(nameof(SelectedChat));
            }
        });
    }

    public async Task RefreshReadyModels()
    {
        List<LocalModel> models = [];
        foreach (var provider in _providers)
        {
            models.AddRange(await provider.GetModelsAsync());
        }

        ReadyModels.Clear();
        foreach (var model in models)
        {
            ReadyModels.Add(model);
        }

        SelectedChat?.LoadSavedModel();

    }

    [RelayCommand]
    public void NewChat()
    {
        var chat = CreateChatViewModel(new Chat());
        SearchText = "";
        Refilter();
        IsSettingsOpen = false;
        SelectedChat = chat;
    }

    partial void OnSelectedChatChanged(ChatViewModel value)
    {
        if (value is not null)
        {
            IsSettingsOpen = false;
            value.LoadSavedModel();
        }
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        IsSettingsOpen = true;
        await Settings.Refresh();
    }

    [RelayCommand]
    private void DeleteChat(ChatViewModel vm)
    {
        if (vm is null)
        {
            return;
        }

        var wasSelected = SelectedChat == vm;
        _store.Delete(vm.Model);
        _all.Remove(vm);
        Refilter();

        // Deleting the open chat drops the user into a fresh chat instead of an existing one.
        if (wasSelected || _all.Count == 0)
        {
            NewChat();
        }
    }

    [RelayCommand]
    private void TogglePin(ChatViewModel vm)
    {
        if (vm is null)
        {
            return;
        }

        vm.IsPinned = !vm.IsPinned;
        Refilter();
    }

    public Task ExportChat(ChatViewModel chat, string path) => _store.Export(chat.Model, path);

    partial void OnSearchTextChanged(string value) => Refilter();

    private void Refilter()
    {
        var keep = SelectedChat;
        var query = SearchText.Trim();
        List<ChatViewModel> visible = query.Length > 0
            ? [.. _all.Where(c => c.Matches(query))]
            : [.. _all];
        Sync(PinnedChats, [.. visible.Where(c => c.IsPinned)]);
        Sync(Chats, [.. visible.Where(c => !c.IsPinned)]);
        OnPropertyChanged(nameof(HasPinned));

        if (keep is not null && _all.Contains(keep))
        {
            SelectedChat = keep;
        }
    }

    private static void Sync(ObservableCollection<ChatViewModel> target, List<ChatViewModel> desired)
    {
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(target[i]))
            {
                target.RemoveAt(i);
            }
        }

        for (int i = 0; i < desired.Count; i++)
        {
            var item = desired[i];
            var current = target.IndexOf(item);
            if (current < 0)
            {
                target.Insert(i, item);
            }
            else if (current != i)
            {
                target.Move(current, i);
            }
        }
    }

    [RelayCommand]
    private void RestartForUpdate() => _updates.ApplyAndRestart();

    [RelayCommand]
    private Task CheckForUpdates() => _updates.Check();
}
