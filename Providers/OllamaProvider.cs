using LocalMind.Models;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace LocalMind.Providers;

public sealed class OllamaProvider : ILocalModelProvider
{
    private static readonly Uri Endpoint = new("http://localhost:11434");

    public string Id => "ollama";
    public string DisplayName => "Ollama";

    public async Task<IReadOnlyList<LocalModel>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var ollama = new OllamaApiClient(Endpoint);
            var models = await ollama.ListLocalModelsAsync(cancellationToken);
            return models
                .Select(m => new LocalModel(Id, DisplayName, m.Name, m.Name))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var ollama = new OllamaApiClient(Endpoint);
            await ollama.ListLocalModelsAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<IChatClient> CreateChatClientAsync(string modelId, CancellationToken cancellationToken = default)
        => Task.FromResult<IChatClient>(new OllamaApiClient(Endpoint, modelId));
}
