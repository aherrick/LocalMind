using System.ClientModel;
using LocalMind.Models;
using LocalMind.Services;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;

namespace LocalMind.Providers;

public sealed class FoundryLocalProvider : ILocalModelProvider
{
    public static readonly IReadOnlyList<(string Alias, string DisplayName)> Curated =
    [
        ("phi-4", "Phi-4"),
        ("phi-4-mini", "Phi-4 Mini"),
        ("qwen2.5-1.5b", "Qwen2.5 1.5B"),
        ("phi-3.5-mini", "Phi-3.5 Mini"),
        ("deepseek-r1-7b", "DeepSeek R1 7B"),
    ];

    private readonly SemaphoreSlim _initGate = new(1, 1);
    private ICatalog _catalog;
    private string _baseUrl;

    public string Id => "foundry";
    public string DisplayName => "Foundry Local";

    private async Task<ICatalog> GetCatalog(CancellationToken ct)
    {
        if (_catalog is not null)
        {
            return _catalog;
        }

        await _initGate.WaitAsync(ct);
        try
        {
            if (_catalog is null)
            {
                if (!FoundryLocalManager.IsInitialized)
                {
                    await FoundryLocalManager.CreateAsync(new Configuration
                    {
                        AppName = "LocalMind",
                        Web = new Configuration.WebService { Urls = "http://127.0.0.1:0" }
                    }, NullLogger.Instance, ct);
                }

                _catalog = await FoundryLocalManager.Instance.GetCatalogAsync(ct);
                await FoundryLocalManager.Instance.StartWebServiceAsync(ct);
                _baseUrl = (FoundryLocalManager.Instance.Urls ?? []).FirstOrDefault();
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
        List<LocalModel> ready = [];
        try
        {
            var catalog = await GetCatalog(cancellationToken);
            foreach (var (alias, displayName) in Curated)
            {
                var model = await catalog.GetModelAsync(alias, cancellationToken);
                if (model is not null && await model.IsCachedAsync(cancellationToken))
                {
                    ready.Add(new LocalModel(Id, DisplayName, alias, displayName));
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warning("Foundry Local model discovery failed.", ex);
        }
        return ready;
    }

    public async Task<bool> IsReady(string alias, CancellationToken cancellationToken = default)
    {
        try
        {
            var catalog = await GetCatalog(cancellationToken);
            var model = await catalog.GetModelAsync(alias, cancellationToken);
            return model is not null && await model.IsCachedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            AppLog.Warning($"Foundry Local readiness check failed for '{alias}'.", ex);
            return false;
        }
    }

    public async Task Download(string alias, Action<float> progress, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalog(cancellationToken);
        var model = await catalog.GetModelAsync(alias, cancellationToken)
            ?? throw new InvalidOperationException($"Model '{alias}' is not available in the Foundry catalog.");
        await model.DownloadAsync(progress, cancellationToken);
    }

    public async Task<IChatClient> CreateChatClientAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalog(cancellationToken);
        var model = await catalog.GetModelAsync(modelId, cancellationToken)
            ?? throw new InvalidOperationException($"Model '{modelId}' is not available in the Foundry catalog.");
        await model.LoadAsync(cancellationToken);

        if (_baseUrl is null)
        {
            throw new InvalidOperationException("Foundry Local web service is not available.");
        }

        // Foundry Local exposes an OpenAI-compatible endpoint; the local service needs no real API key.
        var client = new OpenAIClient(new ApiKeyCredential("not-needed"), new OpenAIClientOptions { Endpoint = new Uri(_baseUrl.TrimEnd('/') + "/v1") });
        return client.GetChatClient(model.Id).AsIChatClient();
    }
}
