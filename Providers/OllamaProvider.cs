using LocalMind.Models;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace LocalMind.Providers;

public sealed class OllamaProvider : ILocalModelProvider
{
    private static readonly Uri Endpoint = new("http://localhost:11434");

    public string Id => "ollama";
    public string DisplayName => "Ollama";

    public async Task<IReadOnlyList<LocalModel>> TryGetModels(CancellationToken cancellationToken = default)
    {
        try
        {
            var ollama = new OllamaApiClient(Endpoint);
            var models = await ollama.ListLocalModelsAsync(cancellationToken);
            var mapped = await Task.WhenAll(models.Select(async m =>
            {
                var supportsTools = false;
                try
                {
                    var info = await ollama.ShowModelAsync(m.Name, cancellationToken);
                    supportsTools = info.Capabilities?.Any(c => c.Equals("tools", StringComparison.OrdinalIgnoreCase)) ?? false;
                }
                catch
                {
                }
                return new LocalModel(Id, DisplayName, m.Name, m.Name) { SupportsTools = supportsTools };
            }));
            return mapped;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<LocalModel>> GetModelsAsync(CancellationToken cancellationToken = default)
        => await TryGetModels(cancellationToken) ?? [];

    public Task<IChatClient> CreateChatClientAsync(string modelId, CancellationToken cancellationToken = default)
        => Task.FromResult<IChatClient>(new OllamaApiClient(Endpoint, modelId));
}
