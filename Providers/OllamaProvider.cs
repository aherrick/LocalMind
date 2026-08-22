using LocalMind.Models;
using LocalMind.Services;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace LocalMind.Providers;

public sealed class OllamaProvider : ILocalModelProvider
{
    private static readonly Uri Endpoint = new("http://localhost:11434");

    public string Id => "ollama";
    public string DisplayName => "Ollama";

    public async Task<LocalProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var ollama = new OllamaApiClient(Endpoint);
            var models = await ollama.ListLocalModelsAsync(cancellationToken);
            return new LocalProviderStatus(true, [.. models.Select(m => new LocalModel(Id, DisplayName, m.Name, m.Name))]);
        }
        catch (Exception ex)
        {
            AppLog.Warning("Ollama model discovery failed.", ex);
            return new LocalProviderStatus(false, []);
        }
    }

    public Task<IChatClient> CreateChatClientAsync(string modelId, CancellationToken cancellationToken = default)
        => Task.FromResult(OpenAICompatible.CreateChatClient(new Uri(Endpoint, "/v1"), modelId));
}
