using System.Text.Json;
using LocalMind.Models;
using Microsoft.Extensions.AI;

namespace LocalMind.Providers;

public sealed class LlamaCppProvider : ILocalModelProvider
{
    private static readonly Uri Endpoint = new("http://127.0.0.1:8080");
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public string Id => "llamacpp";
    public string DisplayName => "llama.cpp";

    public async Task<IReadOnlyList<LocalModel>> TryGetModels(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http.GetAsync(new Uri(Endpoint, "/v1/models"), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("data", out var data))
            {
                return [];
            }

            List<LocalModel> models = [];
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idProp))
                {
                    var id = idProp.GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        models.Add(new LocalModel(Id, DisplayName, id, ShortName(id)));
                    }
                }
            }
            return models;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<LocalModel>> GetModelsAsync(CancellationToken cancellationToken = default)
        => await TryGetModels(cancellationToken) ?? [];

    public Task<IChatClient> CreateChatClientAsync(string modelId, CancellationToken cancellationToken = default)
        => Task.FromResult(OpenAICompatible.CreateChatClient(new Uri(Endpoint, "/v1"), modelId));

    // llama.cpp reports the full model path or hf id; show just the trailing name for readability.
    private static string ShortName(string id)
    {
        var name = id.Replace('\\', '/');
        var slash = name.LastIndexOf('/');
        return slash >= 0 ? name[(slash + 1)..] : name;
    }
}
