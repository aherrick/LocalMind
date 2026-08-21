using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalMind.Models;
using LocalMind.Providers;
using LocalMind.Services;
using Microsoft.Extensions.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace LocalMind.ViewModels;

public enum MessageRole
{
    User,
    Assistant,
}

public partial class ChatMessageVM : ObservableObject
{
    public ChatMessageVM(MessageRole role, string text, DateTimeOffset timestamp)
    {
        Role = role;
        Text = text;
        Timestamp = timestamp;
    }

    public MessageRole Role { get; }
    public DateTimeOffset Timestamp { get; }
    public bool IsUser => Role == MessageRole.User;
    public string Header => Role.ToString();
    public string TimeDisplay => Timestamp.ToLocalTime().ToString("MMM d, h:mm tt");
    public HorizontalAlignment Alignment => IsUser ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
    public Brush Background => IsUser ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 50, 50, 50)) : null;
    public Visibility CopyVisibility => !string.IsNullOrEmpty(Text) ? Visibility.Visible : Visibility.Collapsed;
    public bool IsThinking => !IsUser && string.IsNullOrEmpty(Text);
    public Visibility RegenerateVisibility => IsLast && !IsUser && !string.IsNullOrEmpty(Text) ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    public partial string EditText { get; set; }

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThinking), nameof(CopyVisibility), nameof(RegenerateVisibility))]
    public partial string Text { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegenerateVisibility))]
        EditText = text;
    public partial bool IsLast { get; set; }
}

public partial class ChatViewModel : ObservableObject
{
    private const int ContextWindow = 32768;

    private readonly IReadOnlyList<ILocalModelProvider> _providers;
    private readonly SettingsViewModel _settings;

    private IChatClient _client;
    private CancellationTokenSource _cts;

    public Chat Model { get; }
    public ObservableCollection<ChatMessageVM> Messages { get; } = [];
    public ObservableCollection<LocalModel> ReadyModels { get; }
    public event Action<ChatViewModel> Started;

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial bool IsPinned { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Input { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyPropertyChangedFor(nameof(IsModelSelectionEnabled))]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyPropertyChangedFor(nameof(IsModelSelectionEnabled))]
    public partial bool IsModelLoading { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyPropertyChangedFor(nameof(HasModel), nameof(EmptyStateText))]
    public partial LocalModel SelectedModel { get; set; }

    [ObservableProperty]
    public partial string ContextDisplay { get; set; }

    public bool IsModelSelectionEnabled => !IsGenerating && !IsModelLoading;

    public ChatViewModel(
        Chat chat,
        IReadOnlyList<ILocalModelProvider> providers,
        ObservableCollection<LocalModel> readyModels,
        SettingsViewModel settings)
    {
        Model = chat;
        _providers = providers;
        _settings = settings;
        ReadyModels = readyModels;

        Title = chat.Title;
        Input = "";
        IsPinned = chat.IsPinned;
        foreach (var m in chat.Messages)
        {
            var role = ParseRole(m.Role);
            var text = role == MessageRole.Assistant ? ThinkingTextFilter.Remove(m.Text) : m.Text;
            Messages.Add(new ChatMessageVM(role, text, m.Timestamp));
        }

        Messages.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
        UpdateContext();
        UpdateLastFlags();
    }

    public bool IsEmpty => Messages.Count == 0;

    public bool HasModel => SelectedModel is not null;

    public bool ShowModelPicker => !_settings.IsLoadingModels && ReadyModels.Count > 0;

    public string EmptyStateText => (_settings.IsLoadingModels, ReadyModels.Count, SelectedModel) switch
    {
        (true, _, _) => "Loading models…",
        (_, 0, _) => "Configure models in Settings to get started",
        (_, _, null) => "Select a model above, then start chatting",
        _ => "Ask anything to start the conversation",
    };

    private bool CanSend =>
        !IsGenerating
        && !string.IsNullOrWhiteSpace(Input)
        && SelectedModel is not null;

    partial void OnSelectedModelChanged(LocalModel value)
    {
        if (value is null
            || (Model.ProviderId == value.ProviderId && Model.ModelId == value.Id))
        {
            return;
        }

        Model.ProviderId = value.ProviderId;
        Model.ModelId = value.Id;
        Model.ModelDisplayName = value.DisplayName;
        _client = null;
        Persist();
    }

    partial void OnIsPinnedChanged(bool value)
    {
        if (Model.IsPinned == value)
        {
            return;
        }

        Model.IsPinned = value;
        ChatStore.Save(Model);
    }

    public void LoadSavedModel()
    {
        var savedModel = ReadyModels.FirstOrDefault(model =>
            model.ProviderId == Model.ProviderId && model.Id == Model.ModelId);
        SelectedModel = savedModel;
    }

    public void Rename(string title)
    {
        Title = title;
        Model.Title = title;
        ChatStore.Save(Model);
    }

    public void EditMessage(ChatMessageVM message)
    {
        if (IsGenerating || !message.IsUser)
        {
            return;
        }

        message.EditText = message.Text;
        message.IsEditing = true;
    }

    public void SaveMessage(ChatMessageVM message)
    {
        if (!message.IsEditing)
        {
            return;
        }

        message.Text = message.EditText;
        message.IsEditing = false;
        Persist();
        UpdateContext();
    }

    public static void CancelEditing(ChatMessageVM message)
    {
        message.EditText = message.Text;
        message.IsEditing = false;
    }

    public Task Export(string path) => ChatStore.Export(Model, path);

    internal void NotifyModelStateChanged()
    {
        OnPropertyChanged(nameof(ShowModelPicker));
        OnPropertyChanged(nameof(EmptyStateText));
    }

    public bool Matches(string query)
        => Title.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Messages.Any(m => m.Text.Contains(query, StringComparison.OrdinalIgnoreCase));

    private async Task LoadModel(string providerId, string modelId)
    {
        if (_client is not null || IsModelLoading)
        {
            return;
        }

        IsModelLoading = true;
        try
        {
            var provider = _providers.First(p => p.Id == providerId);
            _client = await provider.CreateChatClientAsync(modelId);
        }
        catch (Exception ex)
        {
            AppLog.Warning($"Failed to load model '{modelId}' from provider '{providerId}'.", ex);
            _client = null;
        }
        finally
        {
            IsModelLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task Send()
    {
        var text = Input.Trim();
        if (text.Length == 0)
        {
            return;
        }

        Input = "";
        Messages.Add(new ChatMessageVM(MessageRole.User, text, DateTimeOffset.Now));

        var isFirstMessage = Messages.Count == 1;
        if (isFirstMessage)
        {
            Title = MakeTitle(text);
            Model.Title = Title;
        }

        Persist();
        if (isFirstMessage)
        {
            Started?.Invoke(this);
        }
        await Generate();
    }

    [RelayCommand]
    private async Task Regenerate()
    {
        if (IsGenerating || IsModelLoading || Messages.Count == 0 || Messages[^1].IsUser)
        {
            return;
        }

        Messages.RemoveAt(Messages.Count - 1);
        UpdateLastFlags();
        Persist();
        await Generate();
    }

    private async Task Generate()
    {
        IsGenerating = true;
        UpdateLastFlags();
        _cts = new CancellationTokenSource();
        var assistant = new ChatMessageVM(MessageRole.Assistant, "", DateTimeOffset.Now);
        var thinkingFilter = new ThinkingTextFilter();
        Messages.Add(assistant);
        try
        {
            if (_client is null)
            {
                await LoadModel(Model.ProviderId, Model.ModelId);
            }
            if (_client is null)
            {
                throw new InvalidOperationException("The selected model could not be loaded.");
            }

            List<ChatMessage> history = [.. Messages
                .Take(Messages.Count - 1)
                .Select(m => new ChatMessage(m.IsUser ? ChatRole.User : ChatRole.Assistant, m.Text))];

            var systemPrompt = _settings.SystemPrompt;
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                history.Insert(0, new ChatMessage(ChatRole.System, systemPrompt));
            }

            await foreach (var update in _client.GetStreamingResponseAsync(history, cancellationToken: _cts.Token))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    var visibleText = thinkingFilter.Append(update.Text);
                    if (visibleText.Length > 0)
                    {
                        assistant.Text += visibleText;
                        UpdateContext();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped by the user.
        }
        catch (Exception ex)
        {
            AppLog.Error($"Chat generation failed for '{Title}' using '{Model.ProviderId}/{Model.ModelId}'.", ex);
            if (string.IsNullOrEmpty(assistant.Text))
            {
                assistant.Text = $"[error] {ex.Message}";
            }
        }
        finally
        {
            IsGenerating = false;
            _cts?.Dispose();
            _cts = null;
            UpdateLastFlags();
            Persist();
            UpdateContext();
            if (!App.IsWindowVisible)
            {
                NotificationService.Show("Response ready", Title);
            }
        }
    }

    private void UpdateLastFlags()
    {
        for (int i = 0; i < Messages.Count; i++)
        {
            Messages[i].IsLast = i == Messages.Count - 1 && !IsGenerating;
        }
    }

    [RelayCommand]
    private void Stop() => _cts?.Cancel();

    private void Persist()
    {
        Model.Messages = [.. Messages
            .Select(m => new StoredMessage { Role = m.Role.ToString(), Text = m.Text, Timestamp = m.Timestamp })];
        Model.UpdatedAt = DateTimeOffset.Now;
        ChatStore.Save(Model);
    }

    private static MessageRole ParseRole(string role)
        => Enum.TryParse<MessageRole>(role, ignoreCase: true, out var parsed) ? parsed : MessageRole.Assistant;

    private void UpdateContext()
    {
        var chars = Messages.Sum(m => m.Text.Length);
        var usedK = (int)Math.Round(chars / 4.0 / 1000.0);
        ContextDisplay = $"{usedK}K / {ContextWindow / 1000}K";
    }

    private static string MakeTitle(string text)
    {
        var line = text.ReplaceLineEndings(" ").Trim();
        return line.Length <= 40 ? line : line[..40] + "…";
    }
}
