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

    public ObservableCollection<ChatViewModel> Chats { get; } = [];
    public ObservableCollection<LocalModel> ReadyModels { get; } = [];
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    public partial ChatViewModel? SelectedChat { get; set; }

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
            Chats.Add(CreateChatViewModel(chat));

        if (Chats.Count > 0)
        {
            SelectedChat = Chats[0];
            _ = SelectedChat.LoadSavedModelAsync();
        }
        else
            NewChat();

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
        Chats.Insert(0, vm);
        SelectedChat = vm;
        IsSettingsOpen = false;
    }

    partial void OnSelectedChatChanged(ChatViewModel? value)
    {
        if (value is not null)
            _ = value.LoadSavedModelAsync();
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
        Chats.Remove(vm);

        if (SelectedChat == vm)
            SelectedChat = Chats.FirstOrDefault();
        if (Chats.Count == 0)
            NewChat();
    }

    [RelayCommand]
    private void RestartForUpdate() => _updates.ApplyAndRestart();
}
