using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalMind.Models;
using LocalMind.Providers;
using LocalMind.Services;
using LocalMind.Tools;
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
    public HorizontalAlignment Alignment => IsUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public Brush? Background => IsUser ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 50, 50, 50)) : null;
    public Visibility CopyVisibility => !IsUser && !string.IsNullOrEmpty(Text) ? Visibility.Visible : Visibility.Collapsed;
    public bool IsThinking => !IsUser && string.IsNullOrEmpty(Text);
    public Visibility RegenerateVisibility => IsLast && !IsUser && !string.IsNullOrEmpty(Text) ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial bool IsLast { get; set; }

    partial void OnTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsThinking));
        OnPropertyChanged(nameof(CopyVisibility));
        OnPropertyChanged(nameof(RegenerateVisibility));
    }

    partial void OnIsLastChanged(bool value) => OnPropertyChanged(nameof(RegenerateVisibility));
}

public partial class ChatViewModel : ObservableObject
{
    private const int ContextWindow = 32768;

    private static readonly ChatOptions ToolOptions = new()
    {
        Tools = [AIFunctionFactory.Create(LocalTools.GetCurrentDateTime)],
    };

    private readonly IReadOnlyList<ILocalModelProvider> _providers;
    private readonly ChatStore _store;
    private readonly NotificationService _notifications;
    private readonly Func<bool> _isWindowVisible;

    private IChatClient? _client;
    private CancellationTokenSource? _cts;

    public Chat Model { get; }
    public ObservableCollection<ChatMessageVM> Messages { get; } = [];
    public ObservableCollection<LocalModel> ReadyModels { get; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial bool IsPinned { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Input { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsModelLoading { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial LocalModel? SelectedModel { get; set; }

    [ObservableProperty]
    public partial bool IsModelLocked { get; set; }

    [ObservableProperty]
    public partial string ModelDisplay { get; set; }

    [ObservableProperty]
    public partial string ContextDisplay { get; set; }

    public ChatViewModel(
        Chat chat,
        IReadOnlyList<ILocalModelProvider> providers,
        ChatStore store,
        NotificationService notifications,
        Func<bool> isWindowVisible,
        ObservableCollection<LocalModel> readyModels)
    {
        Model = chat;
        _providers = providers;
        _store = store;
        _notifications = notifications;
        _isWindowVisible = isWindowVisible;
        ReadyModels = readyModels;

        Title = chat.Title;
        Input = "";
        ModelDisplay = "";
        IsPinned = chat.IsPinned;
        IsModelLocked = chat.Messages.Count > 0;
        foreach (var m in chat.Messages)
            Messages.Add(new ChatMessageVM(ParseRole(m.Role), m.Text, m.Timestamp));

        UpdateModelDisplay();
        UpdateContext();
        UpdateLastFlags();
    }

    private bool CanSend =>
        !IsGenerating
        && !IsModelLoading
        && !string.IsNullOrWhiteSpace(Input)
        && (IsModelLocked || SelectedModel is not null);

    partial void OnSelectedModelChanged(LocalModel? value)
    {
        if (value is not null && !IsModelLocked)
            _ = LoadModelAsync(value.ProviderId, value.Id);
    }

    partial void OnIsPinnedChanged(bool value)
    {
        Model.IsPinned = value;
        _store.Save(Model);
    }

    public Task LoadSavedModelAsync()
        => IsModelLocked ? LoadModelAsync(Model.ProviderId, Model.ModelId) : Task.CompletedTask;

    public bool Matches(string query)
        => Title.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Messages.Any(m => m.Text.Contains(query, StringComparison.OrdinalIgnoreCase));

    private async Task LoadModelAsync(string providerId, string modelId)
    {
        if (_client is not null || IsModelLoading)
            return;

        IsModelLoading = true;
        try
        {
            var provider = _providers.First(p => p.Id == providerId);
            var client = await Task.Run(() => provider.CreateChatClientAsync(modelId));
            _client = client.AsBuilder().UseFunctionInvocation().Build();
        }
        catch
        {
            _client = null;
        }
        finally
        {
            IsModelLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = Input.Trim();
        if (text.Length == 0)
            return;

        if (!IsModelLocked)
        {
            if (SelectedModel is null)
                return;
            Model.ProviderId = SelectedModel.ProviderId;
            Model.ModelId = SelectedModel.Id;
            Model.ModelDisplayName = SelectedModel.DisplayName;
            Model.SupportsTools = SelectedModel.SupportsTools;
            IsModelLocked = true;
            UpdateModelDisplay();
        }

        Input = "";
        Messages.Add(new ChatMessageVM(MessageRole.User, text, DateTimeOffset.Now));

        if (Messages.Count == 1)
        {
            Title = MakeTitle(text);
            Model.Title = Title;
        }

        Persist();
        await GenerateAsync();
    }

    [RelayCommand]
    private async Task RegenerateAsync()
    {
        if (IsGenerating || IsModelLoading || Messages.Count == 0 || Messages[^1].IsUser)
            return;

        Messages.RemoveAt(Messages.Count - 1);
        UpdateLastFlags();
        Persist();
        await GenerateAsync();
    }

    private async Task GenerateAsync()
    {
        IsGenerating = true;
        UpdateLastFlags();
        _cts = new CancellationTokenSource();
        var assistant = new ChatMessageVM(MessageRole.Assistant, "", DateTimeOffset.Now);
        Messages.Add(assistant);
        try
        {
            if (_client is null)
                await LoadModelAsync(Model.ProviderId, Model.ModelId);
            if (_client is null)
                throw new InvalidOperationException("The selected model could not be loaded.");

            var history = Messages
                .Take(Messages.Count - 1)
                .Select(m => new ChatMessage(m.IsUser ? ChatRole.User : ChatRole.Assistant, m.Text))
                .ToList();

            var options = Model.SupportsTools ? ToolOptions : null;
            await foreach (var update in _client.GetStreamingResponseAsync(history, options, _cts.Token))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    assistant.Text += update.Text;
                    UpdateContext();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped by the user.
        }
        catch (Exception ex)
        {
            if (string.IsNullOrEmpty(assistant.Text))
                assistant.Text = $"[error] {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
            _cts?.Dispose();
            _cts = null;
            UpdateLastFlags();
            Persist();
            UpdateContext();
            if (!_isWindowVisible())
                _notifications.Show("Response ready", Title);
        }
    }

    private void UpdateLastFlags()
    {
        for (int i = 0; i < Messages.Count; i++)
            Messages[i].IsLast = i == Messages.Count - 1 && !IsGenerating;
    }

    [RelayCommand]
    private void Stop() => _cts?.Cancel();

    private void Persist()
    {
        Model.Messages = Messages
            .Select(m => new StoredMessage { Role = m.Role.ToString(), Text = m.Text, Timestamp = m.Timestamp })
            .ToList();
        Model.UpdatedAt = DateTimeOffset.Now;
        _store.Save(Model);
    }

    private static MessageRole ParseRole(string role)
        => Enum.TryParse<MessageRole>(role, ignoreCase: true, out var parsed) ? parsed : MessageRole.Assistant;

    private void UpdateModelDisplay()
    {
        if (!IsModelLocked)
        {
            ModelDisplay = "";
            return;
        }
        var provider = _providers.FirstOrDefault(p => p.Id == Model.ProviderId);
        ModelDisplay = provider is null ? Model.ModelDisplayName : $"{provider.DisplayName} · {Model.ModelDisplayName}";
    }

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
