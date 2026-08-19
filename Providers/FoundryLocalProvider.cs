using System.Runtime.CompilerServices;
using System.Text;
using LocalMind.Models;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using BChatMessage = Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage;

namespace LocalMind.Providers;

public sealed class FoundryLocalProvider : ILocalModelProvider
{
    public static readonly IReadOnlyList<(string Alias, string DisplayName)> Curated =
    [
        ("phi-4", "Phi-4"),
        ("phi-4-mini", "Phi-4 Mini"),
        ("qwen2.5-1.5b", "Qwen2.5 1.5B"),
        ("phi-3.5-mini", "Phi-3.5 Mini"),
    ];

    private readonly SemaphoreSlim _initGate = new(1, 1);
    private ICatalog? _catalog;

    public string Id => "foundry";
    public string DisplayName => "Foundry Local";

    private async Task<ICatalog> GetCatalogAsync(CancellationToken ct)
    {
        if (_catalog is not null)
            return _catalog;

        await _initGate.WaitAsync(ct);
        try
        {
            if (_catalog is null)
            {
                if (!FoundryLocalManager.IsInitialized)
                    await FoundryLocalManager.CreateAsync(new Configuration { AppName = "LocalMind" }, NullLogger.Instance, ct);
                _catalog = await FoundryLocalManager.Instance.GetCatalogAsync(ct);
            }
        }
        finally
        {
            _initGate.Release();
        }
        return _catalog;
    }

    public async Task<IReadOnlyList<LocalModel>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        var ready = new List<LocalModel>();
        try
        {
            var catalog = await GetCatalogAsync(cancellationToken);
            foreach (var (alias, displayName) in Curated)
            {
                var model = await catalog.GetModelAsync(alias, cancellationToken);
                if (model is not null && await model.IsCachedAsync(cancellationToken))
                    ready.Add(new LocalModel(Id, DisplayName, alias, displayName));
            }
        }
        catch
        {
        }
        return ready;
    }

    public async Task<bool> IsReadyAsync(string alias, CancellationToken cancellationToken = default)
    {
        try
        {
            var catalog = await GetCatalogAsync(cancellationToken);
            var model = await catalog.GetModelAsync(alias, cancellationToken);
            return model is not null && await model.IsCachedAsync(cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    public async Task DownloadAsync(string alias, Action<float> progress, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogAsync(cancellationToken);
        var model = await catalog.GetModelAsync(alias, cancellationToken)
            ?? throw new InvalidOperationException($"Model '{alias}' is not available in the Foundry catalog.");
        await model.DownloadAsync(progress, cancellationToken);
    }

    public async Task<IChatClient> CreateChatClientAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogAsync(cancellationToken);
        var model = await catalog.GetModelAsync(modelId, cancellationToken)
            ?? throw new InvalidOperationException($"Model '{modelId}' is not available in the Foundry catalog.");
        await model.LoadAsync(cancellationToken);
        var inner = await model.GetChatClientAsync(cancellationToken);
        return new FoundryChatClient(inner);
    }
}

internal sealed class FoundryChatClient(OpenAIChatClient inner) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<AIChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken))
            sb.Append(update.Text);
        return new ChatResponse(new AIChatMessage(ChatRole.Assistant, sb.ToString()));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<AIChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var mapped = messages.Select(Map).ToList();
        await foreach (var chunk in inner.CompleteChatStreamingAsync(mapped, cancellationToken))
        {
            var choice = chunk.Choices?.FirstOrDefault();
            var text = choice?.Delta?.Content ?? choice?.Message?.Content;
            if (!string.IsNullOrEmpty(text))
                yield return new ChatResponseUpdate(ChatRole.Assistant, text);
        }
    }

    private static BChatMessage Map(AIChatMessage m)
    {
        var text = m.Text ?? string.Empty;
        if (m.Role == ChatRole.System)
            return BChatMessage.FromSystem(text);
        if (m.Role == ChatRole.Assistant)
            return BChatMessage.FromAssistant(text);
        return BChatMessage.FromUser(text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceKey is null && serviceType?.IsInstanceOfType(this) == true ? this : null;

    public void Dispose() { }
}
