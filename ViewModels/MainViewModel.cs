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
    private readonly ModelCatalog _catalog;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly List<ChatViewModel> _all = [];

    public ObservableCollection<ChatViewModel> PinnedChats { get; } = [];
    public ObservableCollection<ChatViewModel> Chats { get; } = [];
    public SettingsViewModel Settings { get; }

    public bool HasPinned => PinnedChats.Count > 0;

    // The sidebar shows no selection while Settings is open, without losing the underlying SelectedChat.
    public ChatViewModel SidebarSelectedChat => IsSettingsOpen ? null : SelectedChat;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarSelectedChat))]
    public partial ChatViewModel SelectedChat { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarSelectedChat))]
    public partial bool IsSettingsOpen { get; set; }

    public MainViewModel(
        FoundryLocalProvider foundry,
        OllamaProvider ollama,
        LlamaCppProvider llama)
    {
        _catalog = new ModelCatalog([foundry, ollama, llama]);
        SearchText = "";

        Settings = new SettingsViewModel(foundry);
        Settings.ModelReady += name =>
        {
            NotificationService.Show("Model ready", $"{name} finished downloading.");
            _ = _catalog.Refresh();
        };
        Settings.ModelsChanged += () => _ = _catalog.Refresh();

        Settings.IsLoadingModels = true;
        _catalog.Refreshed += OnCatalogRefreshed;
    }

    // Project the shared provider snapshot into the view-models; the catalog owns all the polling.
    private void OnCatalogRefreshed(IReadOnlyDictionary<string, LocalProviderStatus> statuses)
    {
        Settings.ApplyProviderStatuses(statuses);
        Settings.IsLoadingModels = false;
        SelectedChat?.LoadSavedModel();
        SelectedChat?.NotifyModelStateChanged();
    }

    public void Initialize()
    {
        foreach (var chat in ChatStore.Load())
        {
            _all.Add(CreateChatViewModel(chat));
        }
        Refilter();

        SelectedChat = PinnedChats.FirstOrDefault() ?? Chats.FirstOrDefault();
        if (SelectedChat is null)
        {
            NewChat();
        }
        _catalog.Start();
        _ = _catalog.Refresh();
    }

    private ChatViewModel CreateChatViewModel(Chat chat)
    {
        var viewModel = new ChatViewModel(
            chat, _catalog, Settings);
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

    [RelayCommand]
    public void NewChat()
    {
        var previous = SelectedChat?.Model;
        var chat = CreateChatViewModel(new Chat
        {
            ProviderId = previous?.ProviderId ?? "",
            ModelId = previous?.ModelId ?? "",
        });
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
        await _catalog.Refresh();
    }

    [RelayCommand]
    private void DeleteChat(ChatViewModel vm)
    {
        if (vm is null)
        {
            return;
        }

        var wasSelected = SelectedChat == vm;
        ChatStore.Delete(vm.Model);
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
}
